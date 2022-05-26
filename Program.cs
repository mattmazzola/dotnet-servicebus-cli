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

        var serviceBusConfiguration = config.GetRequiredSection(ServiceBusConfiguration.SectionName).Get<ServiceBusConfiguration>();

        // Set default serializer settings to camelCase
        JsonConvert.DefaultSettings = () => new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver()
        };

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

        var keyNameOption = new Option<string>(
            name: "--key-name",
            description: "Shared Access Key Name",
            getDefaultValue: () => serviceBusConfiguration.SharedAccessKeyName ?? "");
        keyNameOption.AddAlias("-kn");

        var keyOption = new Option<string>(
            name: "--key",
            description: "Shared Access Key",
            getDefaultValue: () => serviceBusConfiguration.SharedAccessKey ?? "");
        keyOption.AddAlias("-k");

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
        rootCommand.AddGlobalOption(connectionStringOption);
        rootCommand.AddGlobalOption(fqnOption);
        rootCommand.AddGlobalOption(keyNameOption);
        rootCommand.AddGlobalOption(keyOption);
        rootCommand.AddGlobalOption(topicNameOption);

        // Regular Commands
        rootCommand.AddCommand(Commands.SendCommand.GetCommand(connectionStringOption, fqnOption, keyNameOption, keyOption, topicNameOption));
        rootCommand.AddCommand(Commands.ReceiveCommand.GetCommand(connectionStringOption, fqnOption, keyNameOption, keyOption, topicNameOption));
        rootCommand.AddCommand(Commands.CrateSasCommand.GetCommand(fqnOption, keyOption, topicNameOption));
        rootCommand.AddCommand(Commands.FullSequenceCommand.GetCommand(connectionStringOption, fqnOption, keyNameOption, keyOption, topicNameOption, subscriptionNameOption));

        // Admin Commands
        rootCommand.AddCommand(Commands.ListTopicsCommand.GetCommand(connectionStringOption, fqnOption, keyNameOption, keyOption));
        rootCommand.AddCommand(Commands.ListSubscriptionsCommand.GetCommand(connectionStringOption, fqnOption, keyNameOption, keyOption, topicNameOption));
        rootCommand.AddCommand(Commands.CreateSubscriptionCommand.GetCommand(connectionStringOption, fqnOption, keyNameOption, keyOption, topicNameOption));
        rootCommand.AddCommand(Commands.DeleteSubscriptionCommand.GetCommand(connectionStringOption, fqnOption, keyNameOption, keyOption, topicNameOption));

        var exitCode = await rootCommand.InvokeAsync(args);

        return exitCode;
    }
}