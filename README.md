# Service Bus Testing Tool

.Net CLI Application to perform operations on Azure Service Bus such as creating Subscription, Sending, and Receiving messages

## Based on

- [System.CommandLine](https://docs.microsoft.com/en-us/dotnet/standard/commandline/get-started-tutorial#install-the-systemcommandline-package)

## Setup

1. Populate fields in the appsettings.json file

	```
    "FullyQualifiedNamespace": "<your service bus name>.servicebus.windows.net",
    "SharedAccessKeyName": "RootManageSharedAccessKey",
    "TopicName": "myTopic",
    "SubscriptionName":  "mySub"
	```

1. Set ServiceBus:SharedAccessKey in UserSecrets

	```dotnetcli
	dotnet user-secrets set ServiceBus:SharedAccessKey <key>
	```

# Running Full Sequence

```dotnetcli
dotnet run full
```

Will execute the following sequence

1. Create connection string from options
1. Create Admin Client from Connection String
1. Create Topic if it does not exist
1. Create Subscription if it does not exist
1. Create SAS for Topic
1. Create Client from FQN and AzureSasCredential
1. Send Messages to Topic using SAS
1. Receive Message from Subscription using SAS
