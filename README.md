# dotnet-az-snapshot-tool

Command line tool that creates Azure managed disk snapshots.

Main motivation was to be able to automate the creation of snapshots **without** using Runbooks and/or having to use Azure Backup with a Virtual Machine.

## Installation

`dotnet tool install --global dotnet-az-snapshot-tool`

## Usage

Available arguments:

- **--tenantId (-t)**: (Required) Azure Tenant ID of the user credentials used to create the snapshot.
- **--subscriptionId (-s)**: (Required) Azure subscription ID of the source disk for the snapshot.
- **--resourceGroup (-g)**: (Required) Resource group of the source disk for the snapshot. 
- **--diskName (-n)**: (Required) Name of the source managed disk name. 
- **--snapshotNameFormat (-f)**: Defines the name of the snapshot resource.  Default is 'diskName-snapshot-yy-MM-dd.hh.mm.ss'.
- **--retainLimit (-l)**: Limits the retained snapshots to specified count.  Default is unlimited (0).
- **--skuType (-k)**: Snapshot sku type.  Available values are 'Standard_LRS' or 'Premium_LRS'. Default is 'Standard_LRS'..

### Example

```shell script
az-snapshot-tool create --tenantId xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx --subscriptionId xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx --resourceGroup myRg --diskName myDisk
```

or

```shell script
az-snapshot-tool create -t xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx -s xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx -g myRg -n myDisk
```

To retain 7 latest snapshot values (including latest):

```shell script
az-snapshot-tool create -t xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx -s xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx -g myRg -n myDisk --retainLimit 7
```

### Details on authentication

This tool uses `AzureDefaultCredentials` which tries multiple credentials types in order, including environment variables, managed identity and az cli.
See [here](https://docs.microsoft.com/en-us/dotnet/api/azure.identity.defaultazurecredential?view=azure-dotnet) for more details.

### Using with a CronJob in Kubernetes

To easily automate the creation on snapshot in Kubernetes, use the [CronJob](https://kubernetes.io/docs/tasks/job/automated-tasks-with-cron-jobs/) resource.
Here's an example using environment variables to provide the Azure Active Directory Application credentials.  It retains the last 7 days of snapshots:

_note: you can also use [aad-pod-identity](https://github.com/Azure/aad-pod-identity)_

```yaml
apiVersion: batch/v1beta1
kind: CronJob
metadata:
  name: snapshot-job
spec:
  schedule: "@daily"
  jobTemplate:
    spec:
      template:
        spec:
          containers:
          - name: snapshot
            image: mcr.microsoft.com/dotnet/core/sdk:3.1
            args:
            - /bin/sh
            - -c
            - >-
                dotnet tool install --tool-path . dotnet-az-snapshot-tool;
                ./az-snapshot-tool run
                -t xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
                -s xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx 
                -g myRg
                -n myDisk
                -l 7
            env:
            - name: AZURE_TENANT_ID
              value: xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
            - name: AZURE_CLIENT_ID
              value: xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
            - name: AZURE_CLIENT_SECRET
              valueFrom:
                secretKeyRef:
                  name: mySecret
                  key: mySecretKey
          restartPolicy: OnFailure
```

## License

Copyright © 2020, GSoft inc. This code is licensed under the Apache License, Version 2.0. You may obtain a copy of this license at https://github.com/gsoft-inc/gsoft-license/blob/master/LICENSE.
