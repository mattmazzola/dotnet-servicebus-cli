using Azure.Messaging.ServiceBus.Administration;
using dotnet_servicebus.Helpers;
using System.CommandLine;

namespace dotnet_servicebus.Commands;

public class CreateSubscriptionCommand
{
    public static Command GetCommand(
        Option connectionStringOption,
        Option fqnOption,
        Option keyNameOption,
        Option keyOption,
        Option topicNameOption
    )
    {
        var includeFilterOption = new Option<bool>(
            name: "--include-filter",
            description: "Inclue Filter on new Subscription",
            getDefaultValue: () => false);
        includeFilterOption.AddAlias("-f");

        var subNameArgument = new Argument<string>("subname", "Subscription Name");
        var command = new Command("createSubscription", "Create Subscription")
        {
            includeFilterOption
        };
        command.AddAlias("cs");
        command.AddArgument(subNameArgument);
        command.SetHandler(async (
            string connectionString,
            string fqn,
            string keyName,
            string key,
            string topicName,
            string subscriptionName,
            bool includeFilter
        ) =>
        {
            ServiceBusHelpers.PrintParams(connectionString, fqn, keyName, key, topicName);
            var cs = ServiceBusHelpers.GetConnectionStringFromOptions(connectionString, fqn, topicName, keyName, key);
            await CreateSubscription(cs, topicName, subscriptionName, includeFilter);
        },
        topicNameOption,
        subNameArgument,
        includeFilterOption);

        return command;
    }

    static async Task CreateSubscription(
        string connectionString,
        string topicName,
        string subscriptionName,
        bool includeFilter
    )
    {
        var adminClient  = new ServiceBusAdministrationClient(connectionString);
        var subOptions = new CreateSubscriptionOptions(topicName, subscriptionName)
        {
            MaxDeliveryCount = 1,
            AutoDeleteOnIdle = TimeSpan.FromDays(1),
        };

        if (includeFilter)
        {
            var sqlRuleFilter = new SqlRuleFilter("agentId='8fcfcf5f'");
            var ruleOptions = new CreateRuleOptions("agentOnly", sqlRuleFilter);
            var subscription = await adminClient.CreateSubscriptionAsync(subOptions, ruleOptions);
            Console.WriteLine($"Created Subscription {subscription.Value.SubscriptionName} with rule {ruleOptions.Filter}!");
        }
        else
        {
            var subscription = await adminClient.CreateSubscriptionAsync(subOptions);
            Console.WriteLine($"Created Subscription {subscription.Value.SubscriptionName} with default true filter!");
        }
    }
}
