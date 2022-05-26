using Azure.Messaging.ServiceBus;
using dotnet_servicebus.Helpers;
using dotnet_servicebus.Models;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.CommandLine;
using System.Text;

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

        var agentIdOption = new Option<string>(
            name: "--agentId",
            description: "Value of agentId property on messsages",
            getDefaultValue: () => "f8e5cd9c");
        agentIdOption.AddAlias("-ag");

        var useOldLib = new Option<bool>(
            name: "--use-old-lib",
            description: "Use deprecrated Microsoft.Azure.ServiceBus lib",
            getDefaultValue: () => false);
        useOldLib.AddAlias("-l");

        var command = new Command("send", "Send Messages.")
        {
            numOfMessagesToSendOption,
            agentIdOption,
            useOldLib
        };

        command.AddAlias("s");
        command.SetHandler(async (
            string connectionString,
            string fqn,
            string keyName,
            string key,
            string topicName,
            int numOfMessages,
            string agentId
        ) => {
            ServiceBusHelpers.PrintParams(connectionString, fqn, keyName, key, topicName);
            var cs = ServiceBusHelpers.GetConnectionStringFromOptions(connectionString, fqn, topicName, keyName, key);
            var client = ServiceBusHelpers.CreateClientFromConnectionString(cs);

            await SendAsync(client, topicName, numOfMessages, agentId);
        },
        connectionStringOption,
        fqnOption,
        keyNameOption,
        keyOption,
        topicNameOption,
        numOfMessagesToSendOption,
        agentIdOption
        );

        return command;
    }

    public static async Task SendAsync(
        ServiceBusClient client,
        string topicName,
        int numOfMessagesToSend,
        string agentId,
        bool closeConnections = true
    )
    {
        var sender = client.CreateSender(topicName);

        Console.WriteLine($"Sending {numOfMessagesToSend} messages to topic {topicName}...");
        using ServiceBusMessageBatch messageBatch = await sender.CreateMessageBatchAsync();

        for (int i = 1; i <= numOfMessagesToSend; i++)
        {
            var gameEvent = new Event()
            {
                EventType = "PlayerMove",
                Message = $"{i} - {DateTime.Now.ToShortTimeString()}",
                AgentId = agentId
            };

            var messageString = JsonConvert.SerializeObject(gameEvent);
            var message = new ServiceBusMessage(messageString)
            {
                ApplicationProperties =
                {
                    ["agentId"] = agentId,
                    ["groupId"] = agentId,
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
                Console.WriteLine($"Added message {messageString} to the batch");
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
    }

    public static async Task SendAsyncOldLib(
        string connectionString,
        string topicName,
        int numOfMessagesToSend
    )
    {
        Console.WriteLine($"Connecting to service bus using: {connectionString}");
        var csStringBuilder = new ServiceBusConnectionStringBuilder(connectionString);
        var connection = new ServiceBusConnection(csStringBuilder);
        var sender = new MessageSender(connection, topicName);

        try
        {
            Console.WriteLine($"Sending {numOfMessagesToSend} messages to topic {topicName}...");
            for (int i = 1; i <= numOfMessagesToSend; i++)
            {
                // Create message
                var messageJson = new JObject();
                messageJson["message"] = $"Message {i} - {DateTime.Now}";
                messageJson["agentId"] = $"agent{i}";

                var messageString = JsonConvert.SerializeObject(messageJson);
                var bytes = Encoding.UTF8.GetBytes(messageString);
                var message = new Message(bytes);

                await sender.SendAsync(message);
            }
        }
        finally
        {
            // Calling DisposeAsync on client types is required to ensure that network
            // resources and other unmanaged objects are properly cleaned up.
            await sender.CloseAsync();
            await connection.CloseAsync();
        }
    }
}
