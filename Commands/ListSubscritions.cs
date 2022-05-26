using Azure.Messaging.ServiceBus.Administration;
using dotnet_servicebus.Helpers;
using System.CommandLine;

namespace dotnet_servicebus.Commands;

public class ListSubscriptionsCommand
{
    public static Command GetCommand(
        Option connectionStringOption,
        Option fqnOption,
        Option keyNameOption,
        Option keyOption,
        Option topicNameOption
)
    {
        var command = new Command("listSubscriptions", "List Subscriptions");
        command.AddAlias("ls");
        command.SetHandler(async (
            string connectionString,
            string fqn,
            string keyName,
            string key,
            string topicName
        ) =>
        {
            ServiceBusHelpers.PrintParams(connectionString, fqn, keyName, key, topicName);
            var cs = ServiceBusHelpers.GetConnectionStringFromOptions(connectionString, fqn, topicName, keyName, key);
            await ListSubscriptions(cs, topicName);
        },
        topicNameOption);

        return command;
    }

    static async Task ListSubscriptions(
        string connectionString,
        string topicName
    )
    {
        var adminClient = new ServiceBusAdministrationClient(connectionString);
        var subscriptionsAsyncPageable = adminClient.GetSubscriptionsAsync(topicName);
        var subscriptions = await subscriptionsAsyncPageable.ToListAsync();

        Console.WriteLine($"Subscriptions:");
        Console.WriteLine($"--------------");
        if (!subscriptions.Any())
        {
            Console.WriteLine($"0 Subscriptions!");
        }
        else
        {
            foreach (var (subscription, index) in subscriptions.Select((tp, i) => (tp, i)))
            {
                Console.WriteLine($"{index + 1}: {subscription.SubscriptionName} ({subscription.Status})");
                Console.WriteLine();

                var rulesAsyncPageable = adminClient.GetRulesAsync(topicName, subscription.SubscriptionName);
                var rules = await rulesAsyncPageable.ToListAsync();

                Console.WriteLine($"\tRules:");
                Console.WriteLine($"\t--------------");
                foreach (var (rule, ruleIndex) in rules.Select((r, i) => (r, i)))
                {
                    Console.WriteLine($"\t{ruleIndex + 1}: {rule.Name} {rule.Filter}");
                }
                Console.WriteLine();
            }
        }

        Console.WriteLine();
    }
}
