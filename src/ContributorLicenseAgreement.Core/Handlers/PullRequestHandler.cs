/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the Microsoft License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

namespace ContributorLicenseAgreement.Core.Handlers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using ContributorLicenseAgreement.Core.Handlers.Helpers;
    using ContributorLicenseAgreement.Core.Handlers.Model;
    using ContributorLicenseAgreement.Core.Primitives.Data;
    using GitOps.Abstractions;
    using GitOps.Apps.Abstractions.AppEventHandler;
    using GitOps.Apps.Abstractions.AppStates;
    using GitOps.Apps.Abstractions.Models;
    using GitOps.Clients.Aad;
    using GitOps.Clients.Ospo;
    using Microsoft.Extensions.Logging;
    using Polly;
    using Check = ContributorLicenseAgreement.Core.Handlers.Model.Check;
    using PullRequest = GitOps.Abstractions.PullRequest;

    internal class PullRequestHandler : IAppEventHandler
    {
        private readonly AppState appState;
        private readonly IAadRequestClient aadRequestClient;
        private readonly IOSPOGitHubLinkRestClient gitHubLinkClient;
        private readonly ClaHelper claHelper;
        private readonly CheckHelper checkHelper;
        private readonly CommentHelper commentHelper;
        private readonly LoggingHelper loggingHelper;
        private readonly ILogger<CLA> logger;

        public PullRequestHandler(
            AppState appState,
            IAadRequestClient aadRequestClient,
            IOSPOGitHubLinkRestClient gitHubLinkClient,
            ClaHelper claHelper,
            CheckHelper checkHelper,
            CommentHelper commentHelper,
            LoggingHelper loggingHelper,
            ILogger<CLA> logger)
        {
            this.appState = appState;
            this.aadRequestClient = aadRequestClient;
            this.gitHubLinkClient = gitHubLinkClient;
            this.claHelper = claHelper;
            this.checkHelper = checkHelper;
            this.commentHelper = commentHelper;
            this.loggingHelper = loggingHelper;
            this.logger = logger;
        }

        public PlatformEventActions EventType => PlatformEventActions.Pull_Request;

        public async Task<object> HandleEvent(GitOpsPayload gitOpsPayload, AppOutput appOutput, params object[] parameters)
        {
            if (parameters.Length == 0)
            {
                logger.LogInformation("No primitive available");
                return appOutput;
            }

            var primitivesData = (IEnumerable<Cla>)parameters[0];
            if (!primitivesData.Any())
            {
                return appOutput;
            }

            var primitive = primitivesData.First();

            if (gitOpsPayload.PlatformContext.ActionType == PlatformEventActions.Closed)
            {
                appOutput.States = await checkHelper.CleanUpChecks(gitOpsPayload, primitive.Content);
                logger.LogInformation("Checks cleaned up");
                return appOutput;
            }

            if (NeedsLicense(primitive, gitOpsPayload.PullRequest))
            {
                logger.LogInformation("License needed for {Sender}", gitOpsPayload.PullRequest.User);

                var hasCla = await HasSignedClaAsync(appOutput, gitOpsPayload, primitive.AutoSignMsftEmployee, primitive.Content);

                appOutput.Comment = await commentHelper.GenerateClaCommentAsync(primitive, gitOpsPayload, hasCla, gitOpsPayload.PullRequest.User);

                var check = new Check
                {
                    Sha = gitOpsPayload.PullRequest.Sha,
                    RepoId = long.Parse(gitOpsPayload.PullRequest.RepositoryId),
                    InstallationId = gitOpsPayload.PlatformContext.InstallationId
                };

                await checkHelper.CreateCheckAsync(
                    gitOpsPayload,
                    hasCla,
                    check,
                    primitive.CheckSummary);

                appOutput.States ??= new States
                {
                    StateCollection = new Dictionary<string, object>()
                };

                appOutput.States.StateCollection.Add(
                    $"{Constants.Check}-{ClaHelper.GenerateKey(gitOpsPayload.PullRequest.User, primitive.Content)}",
                    await checkHelper.AddCheckToStatesAsync(gitOpsPayload, check, primitive.Content));
            }
            else
            {
                logger.LogInformation("No license needed for {Sender}", gitOpsPayload.PullRequest.User);
                await checkHelper.CreateCheckAsync(
                    gitOpsPayload,
                    true,
                    new Check { Sha = gitOpsPayload.PullRequest.Sha, RepoId = long.Parse(gitOpsPayload.PullRequest.RepositoryId), InstallationId = gitOpsPayload.PlatformContext.InstallationId },
                    primitive.CheckSummary);
            }

            appOutput.Conclusion = Conclusion.Success;

            return appOutput;
        }

        private static bool NeedsLicense(Cla primitive, PullRequest pullRequest)
        {
            return !primitive.BypassUsers.Contains(pullRequest.User)
                   && !primitive.BypassOrgs.Contains(pullRequest.OrganizationName)
                   && (pullRequest.Files.Sum(f => f.Changes) >= primitive.MinimalChangeRequired.CodeLines
                       || pullRequest.Files.Count >= primitive.MinimalChangeRequired.Files);
        }

        private async Task<bool> HasSignedClaAsync(AppOutput appOutput, GitOpsPayload gitOpsPayload, bool autoSignMsftEmployee, string claLink)
        {
            var cla = await appState.ReadState<SignedCla>(ClaHelper.GenerateRetrievalKey(gitOpsPayload.PullRequest.User, claLink));

            if (cla == null || (cla.Employee && cla.MsftMail == null))
            {
                var newCla = await TryCreateCla(appOutput, gitOpsPayload, autoSignMsftEmployee, claLink);
                if (newCla == null)
                {
                    if (cla != null)
                    {
                        cla = await claHelper.ExpireCla(gitOpsPayload.PullRequest.User, claLink);
                        appOutput.States = claHelper.GenerateStates(gitOpsPayload.PullRequest.User, claLink, cla);
                    }

                    return false;
                }

                cla = newCla;
            }

            if (!cla.Employee)
            {
                var timestamp = System.DateTimeOffset.Now.ToUnixTimeMilliseconds();
                return cla.Expires == null || cla.Expires > timestamp;
            }

            if (await IsStillEmployed(cla))
            {
                return true;
            }

            logger.LogInformation("Unable to resolve {Sender} with aad. Trying to re-create cla", gitOpsPayload.PullRequest.User);

            cla = await TryCreateCla(appOutput, gitOpsPayload, autoSignMsftEmployee, claLink);

            if (cla == null || !(await IsStillEmployed(cla)))
            {
                cla = await claHelper.ExpireCla(gitOpsPayload.PullRequest.User, claLink);
                appOutput.States = claHelper.GenerateStates(gitOpsPayload.PullRequest.User, claLink, cla);
                return false;
            }

            return await IsStillEmployed(cla);
        }

        private async Task<bool> IsStillEmployed(SignedCla cla)
        {
            ResolvedUser user = null;
            await Policy
                .Handle<Exception>()
                .OrResult<ResolvedUser>(r => r == null)
                .WaitAndRetryAsync(
                    3,
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)))
                .ExecuteAsync(async () =>
                {
                    user = await aadRequestClient.ResolveUserAsync(cla.MsftMail);
                    return user;
                });

            return user is { WasResolved: true };
        }

        private async Task<SignedCla> TryCreateCla(AppOutput appOutput, GitOpsPayload gitOpsPayload, bool autoSignMsftEmployee, string claLink)
        {
            var gitHubUser = gitOpsPayload.PullRequest.User;
            if (!autoSignMsftEmployee)
            {
                return null;
            }

            var gitHubLink = await gitHubLinkClient.GetLink(gitHubUser);
            if (gitHubLink?.GitHub == null)
            {
                return null;
            }

            var cla = claHelper.CreateCla(true, gitHubUser, appOutput, "Microsoft", claLink, msftMail: gitHubLink.Aad.UserPrincipalName);
            logger.LogInformation("CLA signed for GitHub-user: {Cla}", cla);
            loggingHelper.LogClaSigned(
                cla,
                gitOpsPayload.PullRequest.User,
                gitOpsPayload.PlatformContext.OrganizationName,
                gitOpsPayload.PlatformContext.RepositoryName,
                gitOpsPayload.PullRequest.Number);
            return cla;
        }
    }
}
