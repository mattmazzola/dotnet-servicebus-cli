using Azure.Messaging.ServiceBus.Administration;
using dotnet_servicebus.Helpers;
using System.CommandLine;

namespace dotnet_servicebus.Commands;

public class ListTopicsCommand
{
    public static Command GetCommand(
        Option connectionStringOption,
        Option fqnOption,
        Option keyNameOption,
        Option keyOption
    )
    {
        var command = new Command("listTopics", "List Topics");
        command.AddAlias("lt");
        command.SetHandler(async(
            string connectionString,
            string fqn,
            string keyName,
            string key
        ) =>
        {
            ServiceBusHelpers.PrintParams(connectionString, fqn, keyName, key);
            var entityPath = "";
            var cs = ServiceBusHelpers.GetConnectionStringFromOptions(connectionString, fqn, entityPath, keyName, key);

            await ListTopics(cs);
        },
        connectionStringOption,
        fqnOption,
        keyNameOption,
        keyOption
        );

        return command;
    }

    static async Task ListTopics(
        string connectionString
    )
    {
        var adminClient = ServiceBusHelpers.CreateAdminClientFromConnectionString(connectionString);
        var topicsAsyncPageable = adminClient.GetTopicsAsync();
        var topicsProperties = await topicsAsyncPageable.ToListAsync();

        Console.WriteLine($"Topic:");
        Console.WriteLine($"----------");
        if (!topicsProperties.Any())
        {
            Console.WriteLine($"0 Topics!");
        }
        else
        {
            foreach (var (topic, index) in topicsProperties.Select((tp, i) => (tp, i)))
            {
                Console.WriteLine($"{index + 1}: {topic.Name} ({topic.Status})");
            }
        }
        Console.WriteLine();
    }
}
