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


## Useful Commands

The source and receiver are determined by which connection string or FQN you have set

### Sending Events directly to Topic

Send 5 messages with agentSubscriptionFilterValue = '6bf3997b-99b4-4177-bf86-592e96d2d969' and source = AgentService

```dotnetcli
dotnet run s -m 5 -ag 6bf3997b-99b4-4177-bf86-592e96d2d969 -so AgentService
```

### Receiving Events

Verify you can receive events using SAS from Agent subscription

From an Agent subcription on topic mattm-games for 3 seconds

```dotnetcli
dotnet run r agent-31ca002b-ee33-46f6-b51f-c0d60113ec3f -t mattm-games -d 3
```

### Running Latency Test

```dotnetcli
dotnet run lat -m 1000 -cg agent1 -st 10
```