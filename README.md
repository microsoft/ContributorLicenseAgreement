# Contributor License Agreement - CLA 

## What is CLA?

CLA is a tool that allows outside contributors to sign a contribution license agreement (cla), an important license protection for Microsoft and our contributors. Signing this agreement allows external contributors to contribute code to Microsoft open-source repos. It is built on the [Microsoft GitHub Policy Service](https://github.com/microsoft/gitops) platform.

## Usage

To use CLA, you need to define a [cla.yml](src/ContributorLicenseAgreement.Core.Tests/Data/cla.yml) file on org level (example YAML file). This YAML file should define how the CLA should act, the content of the license agreement, and which accounts are exempt from signing.
In addition, the *Microsoft GitHub Policy Service* needs to be installed for your organization. For details, visit https://github.com/microsoft/gitops.

### cla.yml - required properties
- **content**: the contribution licence agreement the author should sign.
- **minimalChangeRequired**: files & codeLines
- **--files**: defines the minimum number of files changed for cla to act.
- **--codeLines**: defines the minimum number of code lines changed for cla to act.

### cla.yml - optional properties
- **bypassUsers**: defines the users for which the cla check is omitted.
- **bypassOrgs**: defines the orgs for which the cla check is omitted.
- **prohibitedCompanies**: defines the companies for which users cannot sign a cla.
- **autoSignMsftEmployee**: if set to true, Microsoft employees will not be asked to sign a cla.
- **signRepos**:	repoName, companyName, & fileName (this section is relevant only for the list of partners that have signed the CLA for their employees)
- **--repoName**:	repository that lives in same organization as the policy and contains approvedUsers.csv
- **--companyName**:	name of the company the CLA is for (stored in our CLA database)
- **--fileName**: approvedUsers.csv	(links to list of users allowed to use CLA, more info below)

### List of Approved Users
If your company has an agreement with Microsoft where only certain users are allowed to make contributions on behalf of your company, then you can specify the users via a CSV file titled approvedUsers.csv which should be located inside the company's repo.

For each user that you want to allow making contributions, add the github username as a line in the csv file (no commas).

## Commands

Whenever a pull request is created, the CLA check will confirm whether or not the user who opened the PR has 
already signed an agreement. If not, it will output a comment prompting the user to accept the agreement and the CLA check on the PR will not pass until that is done.

### Accepting

To accept the agreement, the user can issue one of the following two commands as a comment on the pull request.

```
If you are contributing on behalf of yourself:
@bot agree

If you are contributing on behalf of a company:
@bot agree company="your company"
```

### Terminating

A user can choose to terminate the signed agreement by issuing the following command by commenting under a pull
request that was opened by the same user.

```
@bot terminate
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
