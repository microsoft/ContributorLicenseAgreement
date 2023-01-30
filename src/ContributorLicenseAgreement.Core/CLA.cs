/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the Microsoft License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

namespace ContributorLicenseAgreement.Core
{
    using System.Linq;
    using System.Threading.Tasks;
    using ContributorLicenseAgreement.Core.Primitives.Data;
    using GitOps.Abstractions;
    using GitOps.Apps.Abstractions;
    using GitOps.Apps.Abstractions.AppEventHandler;
    using GitOps.Apps.Abstractions.Models;
    using GitOps.Primitives;

    public sealed class CLA : AppBase
    {
        private readonly PrimitiveCollection primitiveCollection;
        private readonly AppEventHandlerOrchestrator appEventHandlerOrchestrator;

        public CLA(
            PrimitiveCollection primitiveCollection,
            AppEventHandlerOrchestrator appEventHandlerOrchestrator)
        {
            this.primitiveCollection = primitiveCollection;
            this.appEventHandlerOrchestrator = appEventHandlerOrchestrator;
        }

        public override string Id { get; protected set; } = nameof(ContributorLicenseAgreement);

        public override async Task<AppOutput> Run(GitOpsPayload gitOpsPayload)
        {
            var appOutput = new AppOutput
            {
                Conclusion = Conclusion.Neutral
            };

            var primitives = (await primitiveCollection.GetOrgPolicies(gitOpsPayload))
                .Where(p => p is Cla)
                .Cast<Cla>();

            await appEventHandlerOrchestrator.HandleEvent(gitOpsPayload, appOutput, primitives, Id);

            return appOutput;
        }
    }
}
