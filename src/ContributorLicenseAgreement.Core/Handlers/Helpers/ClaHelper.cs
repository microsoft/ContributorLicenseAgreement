/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the Microsoft License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

namespace ContributorLicenseAgreement.Core.Handlers.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using ContributorLicenseAgreement.Core.Handlers.Model;
    using ContributorLicenseAgreement.Core.Primitives.Data;
    using GitOps.Abstractions;
    using GitOps.Apps.Abstractions.AppStates;
    using GitOps.Apps.Abstractions.Models;
    using GitOps.Clients.Aad;
    using GitOps.Clients.GitHub;
    using GitOps.Clients.Ospo;
    using Microsoft.Extensions.Logging;
    using Polly;
    using Check = ContributorLicenseAgreement.Core.Handlers.Model.Check;

    public class ClaHelper
    {
        private readonly AppState appState;
        private readonly ILogger<CLA> logger;
        private readonly LoggingHelper loggingHelper;
        private readonly IAadRequestClient aadRequestClient;
        private readonly IOSPOGitHubLinkRestClient gitHubLinkClient;
        private readonly CheckHelper checkHelper;
        private readonly CommentHelper commentHelper;
        private readonly IGitHubClientAdapterFactory factory;

        public ClaHelper(
            AppState appState,
            IAadRequestClient aadRequestClient,
            IOSPOGitHubLinkRestClient gitHubLinkClient,
            CheckHelper checkHelper,
            CommentHelper commentHelper,
            ILogger<CLA> logger,
            LoggingHelper loggingHelper,
            IGitHubClientAdapterFactory factory)
        {
            this.appState = appState;
            this.logger = logger;
            this.loggingHelper = loggingHelper;
            this.aadRequestClient = aadRequestClient;
            this.gitHubLinkClient = gitHubLinkClient;
            this.checkHelper = checkHelper;
            this.commentHelper = commentHelper;
            this.factory = factory;
        }

        internal static string GenerateKey(string gitHubUser, string claLink)
        {
            claLink = claLink.Replace("/", string.Empty).Replace("?", string.Empty).Replace(":", string.Empty);
            return $"{gitHubUser}-{claLink}";
        }

        internal static string GenerateRetrievalKey(string gitHubUser, string claLink)
        {
            var user = gitHubUser.Replace("[", "%5b").Replace("]", "%5d");
            return GenerateKey(user, claLink);
        }

        internal static bool NeedsLicense(Cla primitive, PullRequest pullRequest, string origin)
        {
            return !primitive.BypassUsers.Contains(pullRequest.User)
                   && !primitive.BypassOrgs.Contains(origin)
                   && (pullRequest.Files.Sum(f => f.Changes) >= primitive.MinimalChangeRequired.CodeLines
                       || pullRequest.Files.Count >= primitive.MinimalChangeRequired.Files);
        }

        internal async Task RunCheck(GitOpsPayload gitOpsPayload, Cla primitive, AppOutput appOutput)
        {
            using var client = await factory.GetGitHubClientAdapterAsync(gitOpsPayload.PlatformContext.OrganizationName, gitOpsPayload.PlatformContext.Dns);
            var pr = await client.GetPullRequestAsync(long.Parse(gitOpsPayload.PullRequest.RepositoryId), gitOpsPayload.PullRequest.Number);
            if (NeedsLicense(primitive, gitOpsPayload.PullRequest, pr.Head.Repository.Owner.Login))
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
        }

        internal SignedCla CreateCla(bool isEmployee, string gitHubUser, AppOutput appOutput, string company, string claLink, string msftMail = null)
        {
            var cla = new SignedCla
            {
                Employee = isEmployee,
                Signed = System.DateTimeOffset.Now.ToUnixTimeMilliseconds(),
                Expires = null,
                GitHubUser = gitHubUser,
                Company = company,
                MsftMail = msftMail,
                CanSelfTerminate = true
            };

            appOutput.States = GenerateStates(gitHubUser, claLink, cla);

            return cla;
        }

        internal (States, IEnumerable<SignedCla>) CreateClas(List<string> gitHubUsers, string company, string claLink)
        {
            var dict = new Dictionary<string, object>();
            var clas = new List<SignedCla>();

            foreach (var gitHubUser in gitHubUsers)
            {
                var cla = new SignedCla
                {
                    Signed = System.DateTimeOffset.Now.ToUnixTimeMilliseconds(),
                    Expires = null,
                    GitHubUser = gitHubUser,
                    Company = company,
                    CanSelfTerminate = false
                };
                dict.Add(GenerateKey(gitHubUser, claLink), cla);
                clas.Add(cla);
            }

            return (new States
            {
                StateCollection = dict
            }, clas);
        }

        internal async Task<SignedCla> ExpireCla(string gitHubUser, string claLink, bool user = true)
        {
            var cla = await appState.ReadState<SignedCla>(GenerateRetrievalKey(gitHubUser, claLink));
            if (cla == null)
            {
                logger.LogError("No cla to terminate");
                return null;
            }

            if (!cla.CanSelfTerminate && user)
            {
                logger.LogError("This cla cannot be terminated by user {User}", gitHubUser);
                return null;
            }

            cla.Expires = System.DateTimeOffset.Now.ToUnixTimeMilliseconds();

            return cla;
        }

        internal States GenerateStates(string gitHubUser, string claLink, SignedCla cla)
        {
            {
                return new States
                {
                    StateCollection = new Dictionary<string, object> { { GenerateKey(gitHubUser, claLink), cla } }
                };
            }
        }

        internal async Task<bool> HasSignedClaAsync(AppOutput appOutput, GitOpsPayload gitOpsPayload, bool autoSignMsftEmployee, string claLink)
        {
            var cla = await appState.ReadState<SignedCla>(ClaHelper.GenerateRetrievalKey(gitOpsPayload.PullRequest.User, claLink));

            if (cla == null || (cla.Employee && cla.MsftMail == null))
            {
                var newCla = await TryCreateCla(appOutput, gitOpsPayload, autoSignMsftEmployee, claLink);
                if (newCla == null)
                {
                    if (cla != null)
                    {
                        cla = await ExpireCla(gitOpsPayload.PullRequest.User, claLink);
                        appOutput.States = GenerateStates(gitOpsPayload.PullRequest.User, claLink, cla);
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
                cla = await ExpireCla(gitOpsPayload.PullRequest.User, claLink);
                appOutput.States = GenerateStates(gitOpsPayload.PullRequest.User, claLink, cla);
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

            var cla = CreateCla(true, gitHubUser, appOutput, "Microsoft", claLink, msftMail: gitHubLink.Aad.UserPrincipalName);
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