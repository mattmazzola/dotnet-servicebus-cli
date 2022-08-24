using Azure.Messaging.ServiceBus.Administration;
using dotnet_servicebus.Helpers;
using Microsoft.Azure.ServiceBus.Primitives;
using System.CommandLine;

namespace dotnet_servicebus.Commands;

public class FullSequenceCommand
{
    public static Command GetCommand(
        Option eventHubConnectionStringOption,
        Option eventHubFqnOption,
        Option eventHubSendListenKeyNameOption,
        Option eventHubSendListenKeyOption,
        Option eventHubNameOption,
        Option connectionStringOption,
        Option fqnOption,
        Option manageKeyNameOption,
        Option manageKeyOption,
        Option listenKeyNameOption,
        Option listenKeyOption,
        Option topicNameOption,
        Option subscriptionNameOption
    )
    {
        var agentSubscriptionFilterValueOption = new Option<string>(
            name: "--agentSubscriptionFilterValue",
            description: "Agent Subscription Filter Value");
        agentSubscriptionFilterValueOption.AddAlias("-asfv");

        var command = new Command("runFullSequence", "Run Full Sequence, Create SAS Token, Send")
        {
            subscriptionNameOption,
            agentSubscriptionFilterValueOption,
        };
        command.AddAlias("full");
        command.SetHandler(async (
            string eventHubConnectionString,
            string eventHubFqn,
            string eventHubSendListenKeyName,
            string eventHubSendListenKey,
            string eventHubName,
            string connectionString,
            string fqn,
            string manageKeyName,
            string manageKey,
            string listenKeyName,
            string listenKey,
            string topicName,
            string subscriptionName,
            string agentSubscriptionFilterValue
            ) =>
        {
            Console.WriteLine("Event Hub Parameters:");
            ServiceBusHelpers.PrintParams(eventHubConnectionString, eventHubFqn, eventHubSendListenKeyName, eventHubSendListenKey, eventHubName);
            var eventHubCs = ServiceBusHelpers.GetConnectionStringFromOptions(eventHubConnectionString, eventHubFqn, eventHubName, eventHubSendListenKeyName, eventHubSendListenKey);

            Console.WriteLine("Service Bus Parameters:");
            ServiceBusHelpers.PrintParams(connectionString, fqn, manageKeyName, manageKey, topicName);

            var serviceBusCs = ServiceBusHelpers.GetConnectionStringFromOptions(connectionString, fqn, topicName, manageKeyName, manageKey);

            var sbAdminClient = ServiceBusHelpers.CreateAdminClientFromConnectionString(serviceBusCs);
            var topicsAsyncPageable = sbAdminClient.GetTopicsAsync();
            var topicsProperties = await topicsAsyncPageable.ToListAsync();

            var doesTopicExist = topicsProperties.Any(t => t.Name == topicName);
            if (doesTopicExist)
            {
                Console.WriteLine($"Topic {topicName} found");
            }
            else
            {
                var topicOptions = new CreateTopicOptions(topicName)
                {
                };
                
                var createdTopic = await sbAdminClient.CreateTopicAsync(topicOptions);
                Console.WriteLine($"Created Topic {createdTopic.Value.Name}!");
            }


            var eventHubAudience = $"https://{eventHubFqn}";
            var audience = $"https://{fqn}";
            var validityDuration = TimeSpan.FromDays(365);
            var numOfMessagesToSend = 5;
            var source = "dotnet-servicebus-cli";

            var subscriptionsAsyncPageable = sbAdminClient.GetSubscriptionsAsync(topicName);
            var subscriptions = await subscriptionsAsyncPageable.ToListAsync();

            var doesSubscriptionExist = subscriptions.Any(s => s.SubscriptionName == subscriptionName);
            if (doesSubscriptionExist)
            {
                Console.WriteLine($"Subscription {subscriptionName} found");
            }
            else
            {
                var subOptions = new CreateSubscriptionOptions(topicName, subscriptionName)
                {
                    MaxDeliveryCount = 1,
                    AutoDeleteOnIdle = TimeSpan.FromDays(1),
                };

                var sqlRuleFilter = new SqlRuleFilter($"service != 'AgentService' AND agentSubscriptionFilter = '{agentSubscriptionFilterValue}'");
                var ruleOptions = new CreateRuleOptions("singleGroup", sqlRuleFilter);
                var createdSub = await sbAdminClient.CreateSubscriptionAsync(subOptions, ruleOptions);
                Console.WriteLine($"Created Subscription {createdSub.Value.SubscriptionName}!");
            }

            Console.WriteLine();
            Console.WriteLine($"Generate EvenHub SAS Signature using params:");
            Console.WriteLine($"Audience:\t{eventHubAudience}");
            Console.WriteLine($"Duration:\t{validityDuration.TotalMinutes} minutes");
            Console.WriteLine();
            Console.WriteLine($"Creating EvenHub SAS Signature from TokenProvider:");
            Console.WriteLine();

            var eventHubSendSasToken = await ServiceBusHelpers.CreateSasTokenUsingProvider(
                eventHubAudience,
                eventHubName,
                null,
                eventHubSendListenKeyName,
                eventHubSendListenKey,
                validityDuration,
                TokenScope.Entity
            );
            Console.WriteLine(eventHubSendSasToken);
            Console.WriteLine();
            var sendClient = ServiceBusHelpers.CreateClientFromSasSignature(eventHubFqn, eventHubSendSasToken);
            await SendCommand.SendAsync(sendClient, eventHubName, numOfMessagesToSend, agentSubscriptionFilterValue, source, closeConnections: false);

            // Wait to give time for Stream Analytics to transfer events from Event Hub to Service Bus
            var saWaitTime = TimeSpan.FromSeconds(5);
            Console.WriteLine($"Waiting {saWaitTime.Seconds} seconds for Stream Analytics:");
            await Task.Delay(saWaitTime);

            Console.WriteLine();
            Console.WriteLine($"Generate Service Bus SAS Signature using params:");
            Console.WriteLine($"Audience:\t{audience}");
            Console.WriteLine($"Duration:\t{validityDuration.TotalMinutes} minutes");
            Console.WriteLine();
            Console.WriteLine($"Creating Service Bus SAS Signature from TokenProvider:");
            Console.WriteLine();
            var listenSasToken = await ServiceBusHelpers.CreateSasTokenUsingProvider(
                audience,
                topicName,
                subscriptionName,
                //manageKeyName,
                //manageKey,
                listenKeyName,
                listenKey,
                validityDuration,
                TokenScope.Entity
            );
            Console.WriteLine(listenSasToken);
            Console.WriteLine();

            var listenClient = ServiceBusHelpers.CreateClientFromSasSignature(fqn, listenSasToken);
            await ReceiveCommand.ReceiveAsync(listenClient, topicName, subscriptionName, durationSeconds: 2);
        },
        eventHubConnectionStringOption,
        eventHubFqnOption,
        eventHubSendListenKeyNameOption,
        eventHubSendListenKeyOption,
        eventHubNameOption,
        connectionStringOption,
        fqnOption,
        manageKeyNameOption,
        manageKeyOption,
        listenKeyNameOption,
        listenKeyOption,
        topicNameOption,
        subscriptionNameOption,
        agentSubscriptionFilterValueOption
        );

        return command;
    }
}
