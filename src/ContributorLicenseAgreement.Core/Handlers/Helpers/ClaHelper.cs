/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the Microsoft License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

namespace ContributorLicenseAgreement.Core.Handlers.Helpers
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using ContributorLicenseAgreement.Core.Handlers.Model;
    using GitOps.Apps.Abstractions.AppStates;
    using GitOps.Apps.Abstractions.Models;
    using Microsoft.Extensions.Logging;

    public class ClaHelper
    {
        private readonly AppState appState;
        private readonly ILogger<CLA> logger;

        public ClaHelper(
            AppState appState,
            ILogger<CLA> logger)
        {
            this.appState = appState;
            this.logger = logger;
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
    }
}