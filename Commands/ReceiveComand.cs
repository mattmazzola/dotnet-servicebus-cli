using Azure.Messaging.ServiceBus;
using dotnet_servicebus.Helpers;
using Microsoft.Azure.ServiceBus;
using Newtonsoft.Json;
using System.CommandLine;

namespace dotnet_servicebus.Commands;

public class ReceiveCommand
{
    public static Command GetCommand(
        Option connectionStringOption,
        Option fqnOption,
        Option keyNameOption,
        Option keyOption,
        Option topicNameOption
    )
    {
        var durationOption = new Option<int>(
            name: "--duration",
            description: "Number of Seconds to receive to Send",
            getDefaultValue: () => 5);
        durationOption.AddAlias("-d");

        var subscriptionNameArgument = new Argument<string>("subscriptionName", "Subscription Name");
        var command = new Command("receive", "Receive Messages.")
        {
            durationOption,
        };
        command.AddAlias("r");
        command.AddArgument(subscriptionNameArgument);
        command.SetHandler(async(
            string connectionString,
            string fqn,
            string keyName,
            string key,
            string topicName,
            string subscriptionName,
            int durationSeconds
        ) =>
        {
            ServiceBusHelpers.PrintParams(connectionString, fqn, keyName, key, topicName, subscriptionName);
            var cs = ServiceBusHelpers.GetConnectionStringFromOptions(connectionString, fqn, topicName, keyName, key);
            var client = ServiceBusHelpers.CreateClientFromConnectionString(cs);
            await ReceiveAsync(client, topicName, subscriptionName, durationSeconds);
        },
        connectionStringOption,
        fqnOption,
        keyNameOption,
        keyOption,
        topicNameOption,
        subscriptionNameArgument,
        durationOption
        );

        return command;
    }

    public static async Task ReceiveAsync(
        ServiceBusClient client,
        string topicName,
        string subscriptionName,
        int durationSeconds
    )
    {
        Console.WriteLine($"Creating processor for topic {topicName} subscription {subscriptionName}");
        var processor = client.CreateProcessor(topicName, subscriptionName, new ServiceBusProcessorOptions());

        try
        {
            processor.ProcessMessageAsync += ProcessMessageAsync;
            processor.ProcessErrorAsync += ProcessErrorAsync;

            await processor.StartProcessingAsync();

            Console.WriteLine($"Start processing for {durationSeconds} seconds...");
            await Task.Delay(TimeSpan.FromSeconds(durationSeconds));

            Console.WriteLine("Stopping the receiver...");
            await processor.StopProcessingAsync();
            Console.WriteLine("Stopped receiving messages");
        }
        finally
        {
            await processor.DisposeAsync();
            await client.DisposeAsync();
        }
    }

    static async Task ProcessMessageAsync(ProcessMessageEventArgs args)
    {
        var bodyJson = args.Message.Body.ToString();
        var formatedBodyJson = JsonConvert.SerializeObject(JsonConvert.DeserializeObject(bodyJson), Formatting.Indented);
        var applicationProperties = JsonConvert.SerializeObject(args.Message.ApplicationProperties, Formatting.Indented);
        Console.WriteLine($"Message (SeqNum: {args.Message.SequenceNumber}):");
        Console.WriteLine($"----------------------------------------------");
        Console.WriteLine($"EnqueuedSequenceNumber:\t{args.Message.EnqueuedSequenceNumber}");
        Console.WriteLine($"EnqueuedTime:\t\t{args.Message.EnqueuedTime}");
        Console.WriteLine();
        Console.WriteLine($"Body:");
        Console.WriteLine(formatedBodyJson);
        Console.WriteLine();
        Console.WriteLine($"User/Custom/Application Properties:");
        Console.WriteLine(applicationProperties);
        Console.WriteLine();

        await args.CompleteMessageAsync(args.Message);
    }

    static Task ProcessErrorAsync(ProcessErrorEventArgs args)
    {
        Console.WriteLine(args.Exception.ToString());
        return Task.CompletedTask;
    }
}
