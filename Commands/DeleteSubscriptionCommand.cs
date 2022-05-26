using Azure.Messaging.ServiceBus.Administration;
using dotnet_servicebus.Helpers;
using System.CommandLine;

namespace dotnet_servicebus.Commands;

public class DeleteSubscriptionCommand
{
    public static Command GetCommand(
        Option connectionStringOption,
        Option fqnOption,
        Option keyNameOption,
        Option keyOption,
        Option topicNameOption
    )
    {
        var subNameArgument = new Argument<string>("subname", "Subscription Name");
        var command = new Command("deleteSubscription", "Delete Subscription");
        command.AddAlias("ds");
        command.AddArgument(subNameArgument);
        command.SetHandler(async (
            string connectionString,
            string fqn,
            string keyName,
            string key,
            string topicName,
            string subscriptionName
        ) =>
        {
            ServiceBusHelpers.PrintParams(connectionString, fqn, keyName, key, topicName);
            var cs = ServiceBusHelpers.GetConnectionStringFromOptions(connectionString, fqn, topicName, keyName, key);
            await DeleteSubscription(cs, topicName, subscriptionName);
        },
        connectionStringOption,
        fqnOption,
        keyNameOption,
        keyOption,
        topicNameOption,
        subNameArgument);

        return command;
    }

    static async Task DeleteSubscription(
        string connectionString,
        string topicName,
        string subscriptionName
    )
    {
        var adminClient  = new ServiceBusAdministrationClient(connectionString);
        var response = await adminClient.DeleteSubscriptionAsync(topicName, subscriptionName);
        if (!response.IsError)
        {
            Console.WriteLine($"Deleted Subscription!");
        }
        else
        {
            Console.WriteLine($"Did NOT Delete Subscription {response.ReasonPhrase}!");

        }
    }
}
