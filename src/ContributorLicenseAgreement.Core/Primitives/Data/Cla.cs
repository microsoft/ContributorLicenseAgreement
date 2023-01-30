/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the Microsoft License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

namespace ContributorLicenseAgreement.Core.Primitives.Data
{
    using System.Collections.Generic;
    using GitOps.Primitives.Abstractions;
    using GitOps.Primitives.Yml;

    /// <summary>
    /// Use this to define the primitive.
    /// The primitive is the unit of computation that this app will act on.
    /// It can be thought of as a configuration for the app, that will be
    /// checked in by the user in source code as a policy configuration.
    ///
    /// The app can define any kind of model here and then implement a
    /// corresponding behavior of this primitive.
    /// </summary>
    [YmlFieldAlias("claPrimitive", YmlFieldTag.Obsolete)]
    public sealed class Cla : IPrimitive
    {
        [YmlFieldAlias("claContent", YmlFieldTag.Obsolete)]
        [YmlFieldAlias("content")]
        public string Content { get; set; }

        public MinimalChangeRequired MinimalChangeRequired { get; set; }

        public List<string> BypassUsers { get; set; } = new List<string>();

        public List<string> BypassOrgs { get; set; } = new List<string>();

        public List<string> ProhibitedCompanies { get; set; } = new List<string>();

        public List<CompanyRepo> SignRepos { get; set; } = new List<CompanyRepo>();

        public string CheckSummary { get; set; } = Constants.CheckSummary;

        public bool AutoSignMsftEmployee { get; set; }
    }
}
