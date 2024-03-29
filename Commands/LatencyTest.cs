﻿using System.CommandLine;
using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Producer;
using System.Text;
using Azure.Messaging.EventHubs.Consumer;
using Newtonsoft.Json;
using dotnet_servicebus.Models;
using System.Diagnostics;
using Azure.Messaging.EventHubs.Processor;
using Azure.Storage.Blobs;
using System.Collections.Concurrent;
using System;

namespace dotnet_servicebus.Commands;

public class LatencyTestCommand
{
    private static IDictionary<string, IList<ReceivedEvent>> partitionIdToEvents = new ConcurrentDictionary<string, IList<ReceivedEvent>>();

    public static Command GetCommand(
        Option blobStorageConnectionStringOption,
        Option blobStorageContainerNameOption,
        Option eventHubConnectionStringOption
    )
    {
        var numOfMessagesToSendOption = new Option<int>(
            name: "--num-messages",
            description: "Number of Messages to Send",
            getDefaultValue: () => 20);
        numOfMessagesToSendOption.AddAlias("-n");

        var maxEventCountPerBatchOption = new Option<int?>(
            name: "--max-events-per-batch",
            description: "Maximum number of events per batch");
        maxEventCountPerBatchOption.AddAlias("-m");

        var consumerGroupOption = new Option<string>(
            name: "--consumer-group",
            description: "Consumer Group",
            getDefaultValue: () => "$Default");
        consumerGroupOption.AddAlias("-cg");

        var subscribeTimeOption = new Option<int>(
            name: "--subscribe-time",
            description: "Subscribe Time Max in Seconds",
            getDefaultValue: () => 10);
        subscribeTimeOption.AddAlias("-st");

        var command = new Command("latency", "Run Latency test. Send N events and Receive N, record time.")
        {
            blobStorageConnectionStringOption,
            blobStorageContainerNameOption,
            eventHubConnectionStringOption,
            numOfMessagesToSendOption,
            maxEventCountPerBatchOption,
            consumerGroupOption,
            subscribeTimeOption
        };
        command.AddAlias("lat");
        command.SetHandler(async (
            string blobStorageConnectionString,
            string blobStorageContainerName,
            string eventHubConnectionString,
            int numOfEvents,
            int? maxEventCountPerBatch,
            string eventHubConsumerGroup,
            int subscribeTime
        ) =>
        {
            // Create cancelation token for max subscription time.
            using var cancellationSource = new CancellationTokenSource();
            cancellationSource.CancelAfter(TimeSpan.FromSeconds(subscribeTime));

            // Receive Events
            Console.WriteLine($"Create BlobContainerClient for container: {blobStorageContainerName}");
            var blobStorageClient = new BlobContainerClient(blobStorageConnectionString, blobStorageContainerName);

            Console.WriteLine($"Create EventProcessorClient using consumer group: {eventHubConsumerGroup}");
            var eventHubProcessor = new EventProcessorClient(blobStorageClient, eventHubConsumerGroup, eventHubConnectionString);
            eventHubProcessor.PartitionInitializingAsync += InitializeEventHandler;
            eventHubProcessor.ProcessEventAsync += ProcessEventHandler;
            eventHubProcessor.ProcessErrorAsync += ProcessErrorHandler;

            Console.WriteLine($"Start Processing...");
            await eventHubProcessor.StartProcessingAsync();

            // Create producer client
            var eventHubProducerClient = new EventHubProducerClient(eventHubConnectionString);
            Console.WriteLine($"Create ProducerClient for EventHub: {eventHubProducerClient.FullyQualifiedNamespace}");

            var stopWatchEventCreation = new Stopwatch();
            stopWatchEventCreation.Start();

            var eventsToSend = new Queue<EventData>();
            var tournamentId = Guid.NewGuid().ToString();
            var taskId = Guid.NewGuid().ToString();
            var gameId = Guid.NewGuid().ToString();
            var roleId = Guid.NewGuid().ToString();
            var groupId = Guid.NewGuid().ToString();
            var agentSubscriptionFilterValue = Guid.NewGuid().ToString();

            for (int i = 1; i <= numOfEvents; i++)
            {
                var testEvent = new Event
                {
                    EventType = "PlayerChatEvent",
                    Message = $"Chat Message {i}",
                    GameId = gameId,
                    TaskId = taskId,
                    TournamentId = tournamentId,
                    RoleId = roleId,
                    GroupId = groupId,
                    AgentSubscriptionFilterValue = agentSubscriptionFilterValue,
                };

                var testEventJson = JsonConvert.SerializeObject(testEvent);
                var eventData = new EventData(Encoding.UTF8.GetBytes(testEventJson));
                eventData.Properties["gameId"] = gameId;
                eventData.Properties["eventIndex"] = i;
                eventsToSend.Enqueue(eventData);
            }

            stopWatchEventCreation.Stop();

            Console.WriteLine($"Sending {numOfEvents} events...");
            var stopWatchSendReceive = new Stopwatch();
            stopWatchSendReceive.Start();

            // https://github.com/Azure/azure-sdk-for-net/blob/main/sdk/eventhub/Azure.Messaging.EventHubs/samples/Sample04_PublishingEvents.md#creating-and-publishing-multiple-batches
            var batches = default(IEnumerable<EventDataBatch>);

            try
            {
                batches = await SendEvents(eventHubProducerClient, eventsToSend, maxEventCountPerBatch);
            }
            finally
            {
                foreach (var batch in batches ?? Array.Empty<EventDataBatch>())
                {
                    batch.Dispose();
                }

                await eventHubProducerClient.CloseAsync();
            }

            // Wait some time to ensure events are received
            await Task.Delay(TimeSpan.FromSeconds(subscribeTime));

            // Stop the processing
            await eventHubProcessor.StopProcessingAsync();
            Console.WriteLine($"Sent and received {partitionIdToEvents.Values.SelectMany(x => x).Count()} events in {stopWatchSendReceive.Elapsed.TotalSeconds} seconds.");

            // Save Events
            var latencyTestResultsFileName = $"LatencyTest_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}.csv";
            Console.WriteLine($"Writing {latencyTestResultsFileName} CSV file...");

            var (csvString, averageEnqueueTimespan, averageEnqueueReceiveTimespan, averageReceiveTimespan) = GetCsvStringFromEventAnalysis();
            var currentDirectory = Directory.GetCurrentDirectory();
            var latencyTestResultsDirectory = Path.Combine(currentDirectory, "LatencyTestResults");

            if (!Directory.Exists(latencyTestResultsDirectory))
            {
                Directory.CreateDirectory(latencyTestResultsDirectory);
            }

            var latencyTestResultsFilePath = Path.Combine(latencyTestResultsDirectory, latencyTestResultsFileName);
            File.WriteAllText(latencyTestResultsFilePath, csvString);
            Console.WriteLine($"Write file to {latencyTestResultsFilePath}");
            Console.WriteLine($"Created {numOfEvents} events in {stopWatchEventCreation.Elapsed}");
            Console.WriteLine($"Average Latency:");
            Console.WriteLine($"Produce-Enqueue:\t{averageEnqueueTimespan} (How long it took to send)");
            Console.WriteLine($"Enqueue-Receive:\t{averageEnqueueReceiveTimespan} (How long it took to receive)");
            Console.WriteLine($"Produce-Receive:\t{averageReceiveTimespan} (Total: Send - Receive Latency)");
        },
        blobStorageConnectionStringOption,
        blobStorageContainerNameOption,
        eventHubConnectionStringOption,
        numOfMessagesToSendOption,
        maxEventCountPerBatchOption,
        consumerGroupOption,
        subscribeTimeOption
        );

        return command;
    }

    private static async Task<IReadOnlyList<EventDataBatch>> SendEvents(
        EventHubProducerClient producer,
        Queue<EventData> queuedEvents,
        int? maxEventCountPerBatch
    )
    {
        var batches = new List<EventDataBatch>();
        var currentBatch = default(EventDataBatch);

        while (queuedEvents.Count > 0)
        {
            var eventData = queuedEvents.Peek();
            var partitionKey = eventData.Properties["gameId"].ToString();
            currentBatch ??= await producer.CreateBatchAsync(new CreateBatchOptions {
                PartitionKey = partitionKey,
            });

            var isEventAddedToBatch = currentBatch.TryAdd(eventData);
            if (!isEventAddedToBatch)
            {
                if (currentBatch.Count == 0)
                {
                    throw new Exception("There was an event too large to fit into a batch.");
                }
                else
                {
                    Console.WriteLine($"The current batch is full with {currentBatch.Count} events of size {currentBatch.SizeInBytes / 1024} KB. Sending current batch and creating new empty batch.");
                }

                await producer.SendAsync(currentBatch);
                batches.Add(currentBatch);
                currentBatch = default;
            }
            else
            {
                queuedEvents.Dequeue();

                if (maxEventCountPerBatch != null && currentBatch.Count == maxEventCountPerBatch)
                {
                    Console.WriteLine($"The current batch has reached the limit of {maxEventCountPerBatch} events. Closing current batch and creating new empty batch.");

                    await producer.SendAsync(currentBatch);
                    batches.Add(currentBatch);
                    currentBatch = default;
                }
            }
        }

        // If there are still items in the current batch, add it to those to be sent.
        if ((currentBatch != default) && (currentBatch.Count > 0))
        {
            await producer.SendAsync(currentBatch);
            batches.Add(currentBatch);
        }

        return batches;
    }

    public static (string, TimeSpan, TimeSpan, TimeSpan) GetCsvStringFromEventAnalysis()
    {
        // CSV Headers
        var csvString = $"Event Index, Partition Id, Sequence Id, Event Id, Produced At, Enqueued At, Received At, Produced Enqueued Latency, Enqueued Received Latency, Produced Received Latency\n";

        // All events should only be from single partition since they all have game game id, but we get from all partitions anyways for completeness
        var areEventsInMoreThanOnePartition = partitionIdToEvents.Keys.Count() > 1;
        if (areEventsInMoreThanOnePartition)
        {
            Console.WriteLine($"WARNING: Events were received from different partitions. {string.Join(", ", partitionIdToEvents.Keys)}");
        }

        var receivedEvents = partitionIdToEvents.Values.SelectMany(x => x);
        var averageEnqueueLatencyMs = receivedEvents
            .Select(re => re.ProducedEnqueuedLatency.TotalMilliseconds)
            .Average();
        var averageEnqueueLatency = TimeSpan.FromMilliseconds(averageEnqueueLatencyMs);

        var averageEnqueueReceiveLatencyMs = receivedEvents
            .Select(re => re.EnqueuedReceivedLatency.TotalMilliseconds)
            .Average();
        var averageEnqueueReceiveLatency = TimeSpan.FromMilliseconds(averageEnqueueReceiveLatencyMs);

        var averageReceiveLatencyMs = receivedEvents
            .Select(re => re.ProducedReceivedLatency.TotalMilliseconds)
            .Average();
        var averageReceiveLatency = TimeSpan.FromMilliseconds(averageReceiveLatencyMs);

        foreach (var (partitionId, partitionEvents) in partitionIdToEvents)
        {
            foreach (var receivedEvent in partitionEvents)
            {
                csvString += $"{receivedEvent.EventData.Properties["eventIndex"]}, {partitionId}, {receivedEvent.EventData.SequenceNumber}, {receivedEvent.EventBody.Id}, {receivedEvent.EventBody.ProducedAtDatetime:0}, {receivedEvent.EventData.EnqueuedTime:O}, {receivedEvent.ReceivedDatetimeOffset:O}, {receivedEvent.ProducedEnqueuedLatency}, {receivedEvent.EnqueuedReceivedLatency}, {receivedEvent.ProducedReceivedLatency}\n";
            }
        };

        return (
            csvString,
            averageEnqueueLatency,
            averageEnqueueReceiveLatency,
            averageReceiveLatency
        );
    }

    public static Task InitializeEventHandler(PartitionInitializingEventArgs args)
    {
        if (args.CancellationToken.IsCancellationRequested)
        {
            return Task.CompletedTask;
        }

        // TODO: Determine why do we have to subtract time in order to receive events that should be enqueued in the future?
        // Latest should be sufficient.
        // Perhaps it takes 1 or 2 seconds to establish connection with EH which happens in the background
        // meaning the events are published before connection is established.
        var startPositionWhenNoCheckpoint = EventPosition.FromEnqueuedTime(DateTimeOffset.UtcNow.Subtract(TimeSpan.FromSeconds(5)));
        //var startPositionWhenNoCheckpoint = EventPosition.Latest;

        args.DefaultStartingPosition = startPositionWhenNoCheckpoint;

        return Task.CompletedTask;
    }

    public static Task ProcessEventHandler(ProcessEventArgs eventArgs)
    {
        // Ensure there is entry in dictionary for given partition
        if (!partitionIdToEvents.ContainsKey(eventArgs.Partition.PartitionId))
        {
            partitionIdToEvents.Add(eventArgs.Partition.PartitionId, new List<ReceivedEvent>());
        };

        var eventBodyString = Encoding.UTF8.GetString(eventArgs.Data.Body.ToArray());
        var eventObject = JsonConvert.DeserializeObject<Event>(eventBodyString);
        var receivedEvent = new ReceivedEvent
        {
            EventData = eventArgs.Data,
            ReceivedDatetimeOffset = DateTimeOffset.UtcNow,
            EventBody = eventObject
        };

        partitionIdToEvents[eventArgs.Partition.PartitionId].Add(receivedEvent);

        return Task.CompletedTask;
    }

    public static Task ProcessErrorHandler(ProcessErrorEventArgs eventArgs)
    {
        Console.WriteLine($"ProcessError: {eventArgs.Exception.Message}");
        Console.WriteLine(eventArgs.Exception);

        return Task.CompletedTask;
    }
}
