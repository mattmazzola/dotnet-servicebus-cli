using Azure.Messaging.ServiceBus;
using dotnet_servicebus.Helpers;
using dotnet_servicebus.Models;
using Newtonsoft.Json;
using System.CommandLine;

namespace dotnet_servicebus.Commands;

public class SendCommand
{
    public static Command GetCommand(
        Option connectionStringOption,
        Option fqnOption,
        Option keyNameOption,
        Option keyOption,
        Option topicNameOption
    )
    {
        var numOfMessagesToSendOption = new Option<int>(
            name: "--num-messages",
            description: "Number of Messages to Send",
            getDefaultValue: () => 3);
        numOfMessagesToSendOption.AddAlias("-m");

        var agentSubscriptionFilterValueOption = new Option<string>(
            name: "--agentId",
            description: "Value of agentSubscriptionFilterValue property on messsages",
            getDefaultValue: () => "f8e5cd9c");
        agentSubscriptionFilterValueOption.AddAlias("-ag");

        var sourceOption = new Option<string>(
            name: "--source",
            description: "Value of source property on messsages",
            getDefaultValue: () => "MYSOURCE");
        sourceOption.AddAlias("-so");

        var command = new Command("send", "Send Messages.")
        {
            numOfMessagesToSendOption,
            agentSubscriptionFilterValueOption,
            sourceOption,
        };

        command.AddAlias("s");
        command.SetHandler(async (
            string connectionString,
            string fqn,
            string keyName,
            string key,
            string topicName,
            int numOfMessages,
            string agentId,
            string source
        ) => {
            ServiceBusHelpers.PrintParams(connectionString, fqn, keyName, key, topicName);
            var cs = ServiceBusHelpers.GetConnectionStringFromOptions(connectionString, fqn, topicName, keyName, key);
            var client = ServiceBusHelpers.CreateClientFromConnectionString(cs);

            await SendAsync(client, topicName, numOfMessages, agentId, source);
        },
        connectionStringOption,
        fqnOption,
        keyNameOption,
        keyOption,
        topicNameOption,
        numOfMessagesToSendOption,
        agentSubscriptionFilterValueOption,
        sourceOption
        );

        return command;
    }

    public static async Task SendAsync(
        ServiceBusClient client,
        string topicName,
        int numOfMessagesToSend,
        string agentSubscriptionFilterValue,
        string source,
        bool closeConnections = true
    )
    {
        var sender = client.CreateSender(topicName);

        Console.WriteLine($"Sending {numOfMessagesToSend} messages to topic {topicName}...");
        Console.WriteLine();

        using ServiceBusMessageBatch messageBatch = await sender.CreateMessageBatchAsync();

        var tournamentId = Guid.NewGuid().ToString();
        var taskId = Guid.NewGuid().ToString();
        var gameId = Guid.NewGuid().ToString();

        for (int i = 1; i <= numOfMessagesToSend; i++)
        {
            var gameEvent = new Event()
            {
                EventType = "PlayerMove",
                TournamentId = tournamentId,
                TaskId = taskId,
                GameId = gameId,
                RoleId = "Role1",
                GroupId = "Group1",
                Source = source,
                AgentSubscriptionFilterValue = agentSubscriptionFilterValue
            };

            var messageString = JsonConvert.SerializeObject(gameEvent);
            var message = new ServiceBusMessage(messageString)
            {
                ApplicationProperties =
                {
                    ["agentSubscriptionFilterValue"] = agentSubscriptionFilterValue,
                    ["source"] = source,
                }
            };

            // Add message
            var isMessageAdded = messageBatch.TryAddMessage(message);
            if (!isMessageAdded)
            {
                throw new Exception($"The message {i} is too large to fit in the batch.");
            }
            else
            {
                Console.WriteLine($"Added message {i} to the batch");
                var mString = JsonConvert.SerializeObject(gameEvent, Formatting.Indented);
                Console.WriteLine(mString);
                Console.WriteLine();
            }
        }

        try
        {
            // Use the producer client to send the batch of messages to the Service Bus topic
            await sender.SendMessagesAsync(messageBatch);
            Console.WriteLine($"A batch of {messageBatch.Count} messages has been published to topic {topicName}.");
        }
        finally
        {
            if (closeConnections)
            {
                // Calling DisposeAsync on client types is required to ensure that network
                // resources and other unmanaged objects are properly cleaned up.
                await sender.DisposeAsync();
                await client.DisposeAsync();
            }
        }

        Console.WriteLine();
    }
}
