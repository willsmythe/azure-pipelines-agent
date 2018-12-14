## Having an issue with YAML?
Open an issue at [Microsoft/azure-pipelines-yaml](https://github.com/Microsoft/azure-pipelines-yaml). This repo contains YAML templates and samples for Azure Pipelines. It is also a place for the community to share best practices, ideas, etc. Open suggestions and issues here if they're specific to YAML pipelines.

## Having an issue with a task?
Open an issue at [Microsoft/azure-pipelines-tasks](https://github.com/Microsoft/azure-pipelines-tasks). This repo contains all tasks shipped in Azure Pipelines and TFS. 

## Having an issue with software on a hosted agent?
Open an issue at [Microsoft/azure-pipelines-image-generation](https://github.com/Microsoft/azure-pipelines-image-generation). This repo contains the VM image used in the Azure Pipelines Hosted Agent Pool. Issues here are for build/release failures related to software installed on the hosted agent (e.g. the .NET SDK is missing or the Azure SDK is not on the latest version).

## Having a generic issue with Azure Pipelines or TFS?
Report it on the [Developer Community](https://developercommunity.visualstudio.com/spaces/21/index.html).

## Have you tried troubleshooting?
See the [troubleshooting docs](https://docs.microsoft.com/azure/devops/pipelines/troubleshooting).

## Agent version and platform
Version of your agent? 2.102.0/2.100.1/...

OS of the machine running the agent? OSX/Windows/Linux/...

## Product

Azure DevOps / Azure Pipelines or on-premises TFS?

* If on-premises, which release? 2015 RTM/QU1/QU2, 2017, etc
* If Azure DevOps / Azure Pipelines, what is the name of your organization (`dev.azure.com/{organization}`)?

## What's not working?
Please include error messages and screenshots.

## Agent and worker diagnostic logs
Logs are located in the agent's `_diag` folder. The agent logs are prefixed with `Agent_` and the worker logs are prefixed with `Worker_`. 

> All sensitive information should already be masked out, please double check before including in the issue.