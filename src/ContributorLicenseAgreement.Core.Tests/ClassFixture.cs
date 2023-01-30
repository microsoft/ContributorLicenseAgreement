/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the Microsoft License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

namespace ContributorLicenseAgreement.Core.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net.Http;
    using ContributorLicenseAgreement.Core.Handlers;
    using ContributorLicenseAgreement.Core.Handlers.Helpers;
    using ContributorLicenseAgreement.Core.Handlers.Model;
    using GitOps.Apps.Abstractions;
    using GitOps.Apps.Abstractions.AppStates;
    using GitOps.Apps.Abstractions.Extensions;
    using GitOps.Clients.Aad;
    using GitOps.Clients.Azure.BlobStorage;
    using GitOps.Clients.GitHub;
    using GitOps.Clients.GitHub.Configuration;
    using GitOps.Clients.Ospo;
    using GitOps.Clients.Ospo.Models;
    using GitOps.Primitives;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Moq;
    using Octokit;
    using RichardSzalay.MockHttp;

    public sealed class ClassFixture : IDisposable
    {
        public ClassFixture()
        {
            var mockBlobStorage = new Mock<IBlobStorage>();
            mockBlobStorage.Setup(f =>
                    f.ReadTableEntityAsync<AppStateTableEntity<SignedCla>>("AppStates", It.IsAny<string>(), "user0-httpstest3.yml"))
                .ReturnsAsync(new AppStateTableEntity<SignedCla> { State = new SignedCla { Employee = true, Expires = null, Signed = 1, GitHubUser = "user0", MsftMail = "user0@microsoft.com", CanSelfTerminate = true } });
            mockBlobStorage.Setup(f =>
                    f.ReadTableEntityAsync<AppStateTableEntity<SignedCla>>("AppStates", It.IsAny<string>(), "formerUser0-httpstest3.yml"))
                .ReturnsAsync(new AppStateTableEntity<SignedCla> { State = new SignedCla { Employee = true, Expires = null, Signed = 1, GitHubUser = "formerUser0", MsftMail = "formerUser0@microsoft.com" } });
            mockBlobStorage.Setup(f =>
                    f.ReadTableEntityAsync<AppStateTableEntity<SignedCla>>("AppStates", It.IsAny<string>(), "externalUser0-httpstest3.yml"))
                .ReturnsAsync(new AppStateTableEntity<SignedCla> { State = new SignedCla { Employee = false, Expires = null, Signed = 1, GitHubUser = "externalUser0", MsftMail = null } });
            mockBlobStorage.Setup(f =>
                    f.ReadTableEntityAsync<AppStateTableEntity<SignedCla>>("AppStates", It.IsAny<string>(), "test-ex-employee-httpstest3.yml"))
                .ReturnsAsync(new AppStateTableEntity<SignedCla> { State = new SignedCla { Employee = false, Expires = null, Signed = 1, GitHubUser = "test-ex-employee", MsftMail = null, Company = "test" } });
            mockBlobStorage.Setup(f =>
                    f.ReadTableEntityAsync<AppStateTableEntity<SignedCla>>("AppStates", It.IsAny<string>(), "user1-httpstest3.yml"))
                .ReturnsAsync(new AppStateTableEntity<SignedCla> { State = null });
            mockBlobStorage.Setup(f =>
                    f.ReadTableEntityAsync<AppStateTableEntity<List<Check>>>("AppStates", It.IsAny<string>(), $"{Constants.Check}-externalUser0-httpstest3.yml"))
                .ReturnsAsync(new AppStateTableEntity<List<Check>> { State = new List<Check> { new Check { Sha = "sha", InstallationId = 1, RepoId = 1 } } });
            mockBlobStorage.Setup(f =>
                    f.ReadTableEntityAsync<AppStateTableEntity<List<Check>>>("AppStates", It.IsAny<string>(), $"{Constants.Check}-user0-httpstest3.yml"))
                .ReturnsAsync(new AppStateTableEntity<List<Check>> { State = new List<Check> { new Check { Sha = "sha", InstallationId = 1, RepoId = 1 } } });
            mockBlobStorage.Setup(f =>
                    f.ReadTableEntityAsync<AppStateTableEntity<List<Check>>>("AppStates", It.IsAny<string>(), $"{Constants.Check}-formerUser0-httpstest3.yml"))
                .ReturnsAsync(new AppStateTableEntity<List<Check>> { State = new List<Check> { new Check { Sha = "sha", InstallationId = 1, RepoId = 1 } } });
            mockBlobStorage.Setup(f =>
                    f.ReadTableEntityAsync<AppStateTableEntity<List<Check>>>("AppStates", It.IsAny<string>(), $"{Constants.Check}-user1-httpstest3.yml"))
                .ReturnsAsync(new AppStateTableEntity<List<Check>> { State = null });
            mockBlobStorage.Setup(f =>
                    f.ReadTableEntityAsync<AppStateTableEntity<List<Check>>>("AppStates", It.IsAny<string>(), $"{Constants.Check}-test-employee-httpstest3.yml"))
                .ReturnsAsync(new AppStateTableEntity<List<Check>> { State = null });
            mockBlobStorage.Setup(f =>
                    f.ReadTableEntityAsync<AppStateTableEntity<List<Check>>>("AppStates", It.IsAny<string>(), $"{Constants.Check}-test-ex-employee-httpstest3.yml"))
                .ReturnsAsync(new AppStateTableEntity<List<Check>> { State = null });
            mockBlobStorage.Setup(f => f.DownloadBlob(It.IsAny<string>(), It.IsAny<Uri>()))
                .ReturnsAsync(File.ReadAllText("Data/cla2.yml"));
            mockBlobStorage.Setup(f => f.ListBlobs(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new List<Uri> { new Uri("https://test.com/policies/microsoft.githubenterprise.com/startclean-test/gitopstest-donotdelete/orgpolicies"), new Uri("https://test.com/policies/microsoft.githubenterprise.com//test/orgpolicies") });
            var mockGitHubLinkClient = new Mock<IOSPOGitHubLinkRestClient>();
            mockGitHubLinkClient.Setup(f => f.GetLink("user1"))
                .ReturnsAsync(new GitHubLink { GitHub = new GitHubUser { Id = 1, Login = "user1" }, Aad = new AadUser { Alias = "user1", UserPrincipalName = "user1@microsoft.com" } });

            var mockAadClient = new Mock<IAadRequestClient>();
            mockAadClient.Setup(f => f.ResolveUserAsync("user0@microsoft.com")).ReturnsAsync(new ResolvedUser("test"));
            mockAadClient.Setup(f => f.ResolveUserAsync("user1@microsoft.com")).ReturnsAsync(new ResolvedUser("test"));
            mockAadClient.Setup(f => f.ResolveUserAsync("formerUser0@microsoft.com")).ReturnsAsync(new ResolvedUser());

            var mockLogger = new Mock<ILogger<CLA>>();
            mockLogger.Setup(x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()));

            var checkRun = new CheckRun(
                1,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                CheckStatus.Completed,
                null,
                new DateTimeOffset(1994, 3, 6, 12, 01, 13, TimeSpan.Zero),
                null,
                null,
                null,
                null,
                null,
                null);

            var mockMessageHandler = new MockHttpMessageHandler();
            mockMessageHandler.When("https://test3.yml").Respond("text/plain", File.ReadAllText("Data/claContent.yml"));

            var mockIHttpClientFactory = new Mock<IHttpClientFactory>();
            mockIHttpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(new HttpClient(mockMessageHandler));

            var mockClient = new Mock<IGitHubClientAdapter>();
            mockClient.Setup(f =>
                f.UpdateCheckRunAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<CheckRunUpdate>()));
            mockClient.Setup(f =>
                f.CreateCheckRunAsync(It.IsAny<long>(), It.IsAny<NewCheckRun>())).ReturnsAsync(checkRun);
            mockClient.Setup(f => f.GetPullRequestAsync(It.IsAny<long>(), It.IsAny<int>()))
                .ReturnsAsync(new PullRequest(
                    1,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    1,
                    ItemState.Open,
                    string.Empty,
                    string.Empty,
                    DateTimeOffset.Now,
                    DateTimeOffset.Now,
                    null,
                    null,
                    null,
                    null,
                    new User(
                        string.Empty,
                        string.Empty,
                        string.Empty,
                        1,
                        string.Empty,
                        DateTimeOffset.Now,
                        DateTimeOffset.Now,
                        1,
                        string.Empty,
                        1,
                        1,
                        false,
                        string.Empty,
                        1,
                        1,
                        string.Empty,
                        "user0",
                        string.Empty,
                        string.Empty,
                        1,
                        null,
                        1,
                        1,
                        1,
                        string.Empty,
                        null,
                        false,
                        string.Empty,
                        null),
                    null,
                    null,
                    false,
                    false,
                    null,
                    null,
                    string.Empty,
                    1,
                    1,
                    1,
                    1,
                    1,
                    null,
                    false,
                    null,
                    null,
                    null,
                    null));

            var mockFactory = new Mock<IGitHubClientAdapterFactory>();
            mockFactory.Setup(f => f.GetGitHubClientAdapterAsync(It.IsAny<long>(), It.IsAny<string>()))
                .ReturnsAsync(mockClient.Object);

            var platformAppFlavorSettings = new PlatformAppFlavorSettings
            {
                PlatformAppsSettings = new Dictionary<string, PlatformAppSettings>
                {
                    {
                        "microsoft.githubenterprise.com",
                        new PlatformAppSettings
                        {
                            Name = "gitops-ppe"
                        }
                    }
                }
            };

            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton<PrimitiveCollection>();
            serviceCollection.RegisterAppEventHandlerOrchestrator();
            serviceCollection.AddSingleton(mockLogger.Object);
            serviceCollection.AddSingleton(mockAadClient.Object);
            serviceCollection.AddSingleton(mockFactory.Object);
            serviceCollection.AddSingleton(mockGitHubLinkClient.Object);
            serviceCollection.AddSingleton(mockBlobStorage.Object);
            serviceCollection.AddSingleton(platformAppFlavorSettings);
            serviceCollection.AddSingleton(mockIHttpClientFactory.Object);

            ServiceProvider = serviceCollection.BuildServiceProvider();
            var appState = new AppState(mockBlobStorage.Object, new Lazy<AppBase>(() => ServiceProvider.GetRequiredService<CLA>()));

            serviceCollection.AddSingleton(appState);
            serviceCollection.AddSingleton<PullRequestHandler>();
            serviceCollection.AddSingleton<IssueCommentHandler>();
            serviceCollection.AddSingleton<ClaHelper>();
            serviceCollection.AddSingleton<CommentHelper>();
            serviceCollection.AddSingleton<CheckHelper>();
            serviceCollection.AddSingleton<LoggingHelper>();
            serviceCollection.AddSingleton<CLA>();

            ServiceProvider = serviceCollection.BuildServiceProvider();
        }

        internal ServiceProvider ServiceProvider { get; }

        public void Dispose()
        {
            ServiceProvider?.Dispose();
        }
    }
}