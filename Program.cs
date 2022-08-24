using System.CommandLine;
using Microsoft.Extensions.Configuration;
using dotnet_servicebus.Configuration;
using Microsoft.Azure.ServiceBus;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace dotnet_servicebus;

public class Program
{
    static async Task<int> Main(string[] args)
    {
        // https://docs.microsoft.com/en-us/dotnet/core/extensions/configuration#basic-example
        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .AddUserSecrets<Program>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        var eventHubConfiguration = config.GetRequiredSection(EventHubConfiguration.SectionName).Get<EventHubConfiguration>();
        var serviceBusConfiguration = config.GetRequiredSection(ServiceBusConfiguration.SectionName).Get<ServiceBusConfiguration>();
        var storageConfiguration = config.GetRequiredSection(StorageConfiguration.SectionName).Get<StorageConfiguration>();

        // Set default serializer settings to camelCase
        JsonConvert.DefaultSettings = () => new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver()
        };
        var blobStorageConnectionStringOption = new Option<string>(
            name: "--blob-storage-connection-string",
            description: "Blob Storage Connection String",
            getDefaultValue: () => storageConfiguration.ConnectionString ?? "");
        blobStorageConnectionStringOption.AddAlias("-bscs");

        var blobStorageContainerNameOption = new Option<string>(
            name: "--blob-storage-container-name",
            description: "Blob Storage Container Name",
            getDefaultValue: () => storageConfiguration.ContainerName ?? "");
        blobStorageContainerNameOption.AddAlias("-bscn");

        var eventHubConnectionStringOption = new Option<string>(
            name: "--eventhub-connection-string",
            description: "Event Hub: Connection string.",
            getDefaultValue: () => eventHubConfiguration.ConnectionString ?? "");
        eventHubConnectionStringOption.AddAlias("-ehc");

        var eventHubFqnOption = new Option<string>(
            name: "--eventhub-fully-qualified-name",
            description: "Event Hub: Fully Qualified Name",
            getDefaultValue: () => eventHubConfiguration.FullyQualifiedNamespace ?? "");
        eventHubFqnOption.AddAlias("-ehfqn");

        var eventHubKeyNameOption = new Option<string>(
            name: "--eventhub-manage-key-name",
            description: "Event Hub: SendListen Shared Access Key Name",
            getDefaultValue: () => eventHubConfiguration.SendListenSharedAccessKeyName ?? "");
        eventHubKeyNameOption.AddAlias("-ehmkn");

        var eventHubKeyOption = new Option<string>(
            name: "--eventhub-manage-key",
            description: "Event Hub: SendListen Shared Access Key",
            getDefaultValue: () => eventHubConfiguration.SendListenSharedAccessKey ?? "");
        eventHubKeyOption.AddAlias("-ehmk");

        var eventHubNameOption = new Option<string>(
            name: "--eventhub-name",
            description: "Event Hub: Name",
            getDefaultValue: () => eventHubConfiguration.EventHubName ?? "");
        eventHubNameOption.AddAlias("-eht");

        var connectionStringOption = new Option<string>(
            name: "--connection-string",
            description: "Connection string to Service Bus instance.",
            getDefaultValue: () => serviceBusConfiguration.ConnectionString ?? "");
        connectionStringOption.AddAlias("-c");

        var fqnOption = new Option<string>(
            name: "--fully-qualified-name",
            description: "Fully Qualified Name",
            getDefaultValue: () => serviceBusConfiguration.FullyQualifiedNamespace ?? "");
        fqnOption.AddAlias("-fqn");

        var manageKeyNameOption = new Option<string>(
            name: "--manage-key-name",
            description: "Manage Shared Access Key Name",
            getDefaultValue: () => serviceBusConfiguration.ManageSharedAccessKeyName ?? "");
        manageKeyNameOption.AddAlias("-mkn");

        var manageKeyOption = new Option<string>(
            name: "--manage-key",
            description: "Manage Shared Access Key",
            getDefaultValue: () => serviceBusConfiguration.ManageSharedAccessKey ?? "");
        manageKeyOption.AddAlias("-mk");

        var listenKeyNameOption = new Option<string>(
            name: "--listen-key-name",
            description: "Listen Shared Access Key Name",
            getDefaultValue: () => serviceBusConfiguration.ListenSharedAccessKeyName ?? "");
        listenKeyNameOption.AddAlias("-lkn");

        var listenKeyOption = new Option<string>(
            name: "--listen-key",
            description: "Listen Shared Access Key",
            getDefaultValue: () => serviceBusConfiguration.ListenSharedAccessKey ?? "");
        listenKeyOption.AddAlias("-lk");

        var topicNameOption = new Option<string>(
            name: "--topic-name",
            description: "Topic Name",
            getDefaultValue: () => serviceBusConfiguration.TopicName ?? "");
        topicNameOption.AddAlias("-t");

        var subscriptionNameOption = new Option<string>(
            name: "--subscription-name",
            description: "Subscription Name",
            getDefaultValue: () => serviceBusConfiguration.SubscriptionName ?? "");
        subscriptionNameOption.AddAlias("-sn");

        var rootCommand = new RootCommand(".Net CLI Application to perform operations on Azure Service Bus such as creating Subscription, Sending, and Receiving messages");
        rootCommand.AddGlobalOption(blobStorageConnectionStringOption);
        rootCommand.AddGlobalOption(blobStorageContainerNameOption);
        rootCommand.AddGlobalOption(eventHubConnectionStringOption);
        rootCommand.AddGlobalOption(eventHubFqnOption);
        rootCommand.AddGlobalOption(eventHubKeyNameOption);
        rootCommand.AddGlobalOption(eventHubKeyOption);
        rootCommand.AddGlobalOption(eventHubNameOption);
        rootCommand.AddGlobalOption(connectionStringOption);
        rootCommand.AddGlobalOption(fqnOption);
        rootCommand.AddGlobalOption(manageKeyNameOption);
        rootCommand.AddGlobalOption(manageKeyOption);
        rootCommand.AddGlobalOption(listenKeyNameOption);
        rootCommand.AddGlobalOption(listenKeyOption);
        rootCommand.AddGlobalOption(topicNameOption);

        // Regular Commands
        rootCommand.AddCommand(Commands.SendCommand.GetCommand(connectionStringOption, fqnOption, manageKeyNameOption, manageKeyOption, topicNameOption));
        rootCommand.AddCommand(Commands.ReceiveCommand.GetCommand(connectionStringOption, fqnOption, manageKeyNameOption, manageKeyOption, topicNameOption));
        rootCommand.AddCommand(Commands.CrateSasCommand.GetCommand(fqnOption, manageKeyOption, topicNameOption));
        rootCommand.AddCommand(Commands.FullSequenceCommand.GetCommand(
            eventHubConnectionStringOption,
            eventHubFqnOption,
            eventHubKeyNameOption,
            eventHubKeyOption,
            eventHubNameOption,
            connectionStringOption,
            fqnOption,
            manageKeyNameOption,
            manageKeyOption,
            listenKeyNameOption,
            listenKeyOption,
            topicNameOption,
            subscriptionNameOption
        ));
        rootCommand.AddCommand(Commands.LatencyTestCommand.GetCommand(
            blobStorageConnectionStringOption,
            blobStorageContainerNameOption,
            eventHubConnectionStringOption  
        ));

        // Admin Commands
        rootCommand.AddCommand(Commands.ListTopicsCommand.GetCommand(connectionStringOption, fqnOption, manageKeyNameOption, manageKeyOption));
        rootCommand.AddCommand(Commands.ListSubscriptionsCommand.GetCommand(connectionStringOption, fqnOption, manageKeyNameOption, manageKeyOption, topicNameOption));
        rootCommand.AddCommand(Commands.CreateSubscriptionCommand.GetCommand(connectionStringOption, fqnOption, manageKeyNameOption, manageKeyOption, topicNameOption));
        rootCommand.AddCommand(Commands.DeleteSubscriptionCommand.GetCommand(connectionStringOption, fqnOption, manageKeyNameOption, manageKeyOption, topicNameOption));

        var exitCode = await rootCommand.InvokeAsync(args);

        return exitCode;
    }
}