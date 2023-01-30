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
    using GitOps.Abstractions;
    using GitOps.Apps.Abstractions.AppStates;
    using GitOps.Apps.Abstractions.Models;
    using GitOps.Clients.GitHub;
    using Microsoft.Extensions.Logging;
    using Octokit;
    using Check = ContributorLicenseAgreement.Core.Handlers.Model.Check;

    public class CheckHelper
    {
        private readonly AppState appState;
        private readonly IGitHubClientAdapterFactory clientAdapterFactory;
        private readonly ILogger<CLA> logger;

        public CheckHelper(IGitHubClientAdapterFactory clientAdapterFactory, AppState appState, ILogger<CLA> logger)
        {
            this.appState = appState;
            this.clientAdapterFactory = clientAdapterFactory;
            this.logger = logger;
        }

        internal async Task<List<Check>> UpdateChecksAsync(
            GitOpsPayload gitOpsPayload,
            bool hasCla,
            string gitHubUser,
            string claLink,
            string checkSummary)
        {
            var shas = await appState.ReadState<List<Check>>($"{Constants.Check}-{ClaHelper.GenerateRetrievalKey(gitHubUser, claLink)}");

            if (shas == null)
            {
                return null;
            }

            var checks = new List<Check>();
            foreach (var check in shas)
            {
                if (await CreateCheckAsync(gitOpsPayload, hasCla, check, checkSummary))
                {
                    checks.Add(check);
                }
            }

            return checks;
        }

        internal async Task<bool> CreateCheckAsync(
            GitOpsPayload gitOpsPayload,
            bool hasCla,
            Check check,
            string checkSummary)
        {
            var client = await clientAdapterFactory.GetGitHubClientAdapterAsync(
                check.InstallationId,
                gitOpsPayload.PlatformContext.Dns);

            var checkRun = new NewCheckRun(Constants.CheckName, check.Sha)
            {
                Status = hasCla ? CheckStatus.Completed : CheckStatus.Queued,
                Output = new NewCheckRunOutput(hasCla ? Constants.CheckSuccessTitle : Constants.CheckInProgressTitle, checkSummary),
                DetailsUrl = "https://github.com/microsoft/contributorlicenseagreement"
            };

            if (hasCla)
            {
                checkRun.Conclusion = Enum.Parse<CheckConclusion>(Conclusion.Success.ToString(), true);
            }

            try
            {
                await client.CreateCheckRunAsync(check.RepoId, checkRun);
                return true;
            }
            catch
            {
                logger.LogInformation("Unable to create check with sha: {Sha}", check.Sha);
                return false;
            }
        }

        internal async Task<States> CleanUpChecks(GitOpsPayload payload, string claLink)
        {
            var key = $"{Constants.Check}-{ClaHelper.GenerateRetrievalKey(payload.PullRequest.User, claLink)}";
            var checks = await appState.ReadState<List<Check>>(key);
            if (checks == null)
            {
                return null;
            }

            key = $"{Constants.Check}-{ClaHelper.GenerateKey(payload.PullRequest.User, claLink)}";

            return new States
            {
                StateCollection = new Dictionary<string, object>
                {
                    {
                        key,
                        checks.Where(s => !s.Sha.Equals(payload.PullRequest.Sha)).ToList()
                    }
                }
            };
        }

        internal async Task<List<Check>> AddCheckToStatesAsync(GitOpsPayload payload, Check check, string claLink)
        {
            var key = $"{Constants.Check}-{ClaHelper.GenerateKey(payload.PullRequest.User, claLink)}";

            var checks = await appState.ReadState<List<Check>>(key);

            checks = checks ?? new List<Check>();

            checks.Add(check);

            return checks;
        }
    }
}