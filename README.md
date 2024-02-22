<p align="left">
  <img src="https://github.com/microsoft/contributorlicenseagreement/actions/workflows/build_net_core.yml/badge.svg?branch=main&event=push"></a>
  <img src="https://github.com/microsoft/contributorlicenseagreement/actions/workflows/publish_all.yml/badge.svg?branch=main&event=push"></a>
  <img src="https://github.com/microsoft/contributorlicenseagreement/blob/coverage/docs/images/linecoverage.svg"></a>
</p>

 [Count Lines Of Code](https://github.com/microsoft/ContributorLicenseAgreement/blob/cloc/docs/cloc/cloc.txt)

# Contributor License Agreement - CLA 

## What is CLA?

CLA is a tool that allows outside contributors to sign a contribution license agreement (cla), an important license protection for Microsoft and our contributors. Signing this agreement allows external contributors to contribute code to Microsoft open-source repos. It is built on the Microsoft GitHub Policy Service platform.

## Installation

- Install [Microsoft GitHub Policy Service](https://github.com/apps/microsoft-github-policy-service)
- If you run on GH Enterprise Cloud, you have to give us(by creating an issue on this repo) the name of your enterprise.
- Create a .github repo.
- Add [platformcontext.yml](https://github.com/microsoft/.github/blob/main/policies/platformcontext.yml) under policies folder. You can push this directly.
- Add [cla.yml](https://github.com/microsoft/.github/blob/main/policies/cla.yml) under policies folder. Create a seperate PR for this, the policy service will create a comment example bellow. After you merge the PR, CLA policy will be activated across the entire org.
![image](https://user-images.githubusercontent.com/19934057/197821627-3933c109-bbba-4714-b16c-8b457ad2084d.png)
- For checks on branch protection make sure you select "any source" or "Microsoft GitHub Policy Service".
![image](https://user-images.githubusercontent.com/19934057/198332238-66781732-8b4c-4b04-8f05-e7571caec999.png)


## Usage

To use CLA, you need to define a [cla.yml](src/ContributorLicenseAgreement.Core.Tests/Data/cla.yml)/[Microsoft GitHub CLA](https://github.com/microsoft/.github/blob/main/policies/cla.yml) file on org level (example YAML file). This YAML file should define how the CLA should act, the content of the license agreement, and which accounts are exempt from signing.
In addition, the *Microsoft GitHub Policy Service* needs to be installed for your organization.

### cla.yml - required properties
- **content**: the contribution licence agreement the author should sign.
- **minimalChangeRequired**: defines the minumum changes in files or codelines required to make the policy enforce signing a cla first.
- **--files**: defines the minimum number of files changed for cla to act.
- **--codeLines**: defines the minimum number of code lines changed for cla to act.

### cla.yml - optional properties
- **bypassUsers**: defines the users for which the cla check is omitted.
- **bypassOrgs**: defines the orgs for which the cla check is omitted.
- **prohibitedCompanies**: defines the companies for which users cannot sign a cla.
- **autoSignMsftEmployee**: if set to true, Microsoft employees will not be asked to sign a cla.
- **checkSummary**: defines the check summary text shown.
- **signRepos**:	repoName, companyName, & fileName (this section is relevant only for the list of partners that have signed the CLA for their employees)
- **--repoName**:	repository that lives in same organization as the policy and contains approvedUsers.csv
- **--companyName**:	name of the company the CLA is for (stored in our CLA database)
- **--fileName**: approvedUsers.csv	(links to list of users allowed to use CLA, more info below)

### List of Approved Users
If your company has an agreement with Microsoft where only certain users are allowed to make contributions on behalf of your company, then you can specify the users via a CSV file titled approvedUsers.csv which should be located inside the company's repo. The list is global per CLA content link and has to be specified only once, [example here](https://github.com/microsoft/.github/blob/main/policies/cla.yml).

For each user that you want to allow making contributions, add the github username as a line in the csv file (no commas).

### List of Approved Bots
In order to allow bots to create and merge pull requests, they must be pre-approved. Pre-approving bots is done by adding the bot name to the *approvedBos.csv* file located in the [cla-approved-bots](https://github.com/microsoft/cla-approved-bots) repo.


## Commands

Whenever a pull request is created, the CLA check will confirm whether or not the user who opened the PR has 
already signed an agreement. If not, it will output a comment prompting the user to accept the agreement and the CLA check on the PR will not pass until that is done.

### Accepting

To accept the agreement, the user can issue one of the following two commands as a comment on the pull request.

```
If you are contributing on behalf of yourself:
@microsoft-github-policy-service agree

If you are contributing on behalf of a company:
@microsoft-github-policy-service agree company="your company"
```

### Terminating

A user can choose to terminate the signed agreement by issuing the following command by commenting under a pull
request that was opened by the same user.

```
@microsoft-github-policy-service terminate
```

### Re-running

In case the CLA app failed to post a status check, users can request a re-run by issuing the following command under a 
pull request. In this case, it does not matter if the user issuing the command is the pull request author
or not.

```
@microsoft-github-policy-service rerun
```

## Contributing

This project welcomes contributions and suggestions.  Most contributions require you to agree to a
Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us
the rights to use your contribution. For details, visit https://cla.opensource.microsoft.com.

When you submit a pull request, a CLA bot will automatically determine whether you need to provide
a CLA and decorate the PR appropriately (e.g., status check, comment). Simply follow the instructions
provided by the bot. You will only need to do this once across all repos using our CLA.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or
contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.

## Trademarks

This project may contain trademarks or logos for projects, products, or services. Authorized use of Microsoft 
trademarks or logos is subject to and must follow 
[Microsoft's Trademark & Brand Guidelines](https://www.microsoft.com/en-us/legal/intellectualproperty/trademarks/usage/general).
Use of Microsoft trademarks or logos in modified versions of this project must not cause confusion or imply Microsoft sponsorship.
Any use of third-party trademarks or logos are subject to those third-party's policies.
