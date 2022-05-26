using Azure.Messaging.ServiceBus.Administration;
using dotnet_servicebus.Helpers;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Primitives;
using System.CommandLine;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Web;

namespace dotnet_servicebus.Commands;

public class FullSequenceCommand
{
    public static Command GetCommand(
        Option connectionStringOption,
        Option fqnOption,
        Option keyNameOption,
        Option keyOption,
        Option topicNameOption,
        Option subscriptionNameOption
    )
    {
        var command = new Command("runFullSequence", "Run Full Sequence, Create SAS Token, Send")
        {
            subscriptionNameOption,
        };
        command.AddAlias("full");
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

            var adminClient = ServiceBusHelpers.CreateAdminClientFromConnectionString(cs);
            var topicsAsyncPageable = adminClient.GetTopicsAsync();
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
                
                var createdTopic = await adminClient.CreateTopicAsync(topicOptions);
                Console.WriteLine($"Created Topic {createdTopic.Value.Name}!");
            }


            var subscriptionsAsyncPageable = adminClient.GetSubscriptionsAsync(topicName);
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

                var sqlRuleFilter = new SqlRuleFilter("groupId='MYGROUP2'");
                var ruleOptions = new CreateRuleOptions("singleGroup", sqlRuleFilter);
                var createdSub = await adminClient.CreateSubscriptionAsync(subOptions, ruleOptions);
                Console.WriteLine($"Created Subscription {createdSub.Value.SubscriptionName}!");
            }

            var entityPath = topicName;
            var validityDuration = TimeSpan.FromMinutes(30);
            var sasKeyName = "myKey";
            var numOfMessages = 5;
            var groupId = "MYGROUP2";

            Console.WriteLine($"Generate SAS Signature using params:");
            Console.WriteLine($"FQN:\t{fqn}");
            Console.WriteLine($"EntityPath:\t{entityPath}");
            Console.WriteLine($"Audience:\t{TokenScope.Namespace}");
            Console.WriteLine($"Duration:\t{validityDuration.TotalMinutes} minutes");
            Console.WriteLine();
            Console.WriteLine($"Creating SAS Signature from TokenProvider:");
            Console.WriteLine();

            var (sasTokenManualGithub, audience) = ServiceBusHelpers.BuildSignature(
                fqn,
                entityPath,
                keyName,
                key,
                DateTimeOffset.Now.Add(validityDuration)
            );

            var sasFromTokenProvider = await ServiceBusHelpers.CreateSasTokenUsingProvider(audience, entityPath, keyName, key, validityDuration, TokenScope.Namespace);

            Console.WriteLine(sasTokenManualGithub);
            Console.WriteLine();

            var client = ServiceBusHelpers.CreateClientFromSasSignature(fqn, sasFromTokenProvider);
            await SendCommand.SendAsync(client, topicName, numOfMessages, groupId, closeConnections: false);
            await ReceiveCommand.ReceiveAsync(client, topicName, subscriptionName, durationSeconds: 5);
        },
        connectionStringOption,
        fqnOption,
        keyNameOption,
        keyOption,
        topicNameOption,
        subscriptionNameOption
        );

        return command;
    }

    static Task<string> CreateSasTokenManually(
        string resourceUri,
        string keyName,
        string key,
        TimeSpan validityDuration
    )
    {
        // https://docs.microsoft.com/en-us/rest/api/eventhub/generate-sas-token#c
        var sinceEpoch = DateTime.UtcNow - new DateTime(1970, 1, 1);
        var expiry = Convert.ToString((int)sinceEpoch.TotalSeconds + validityDuration.TotalSeconds);

        string stringToSign = HttpUtility.UrlEncode(resourceUri) + "\n" + expiry;
        var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
        var signature = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(stringToSign)));
        var sasToken = string.Format(CultureInfo.InvariantCulture, "SharedAccessSignature sr={0}&sig={1}&se={2}&skn={3}", HttpUtility.UrlEncode(resourceUri), HttpUtility.UrlEncode(signature), expiry, keyName);

        return Task.FromResult(sasToken);
    }

    static async Task<(string, string, string)> CreateSasTokenUsingProvider(
        string endpoint,
        string entityPath,
        string keyName,
        string key,
        TimeSpan validityDuration,
        TokenScope tokenScope
    )
    {
        // https://github.com/Azure/azure-service-bus/blob/31a0018c122fb39e7016a25706023b45d9622374/samples/DotNet/Microsoft.Azure.ServiceBus/ManagingEntities/SASAuthorizationRule/Program.cs#L70-L72
        var sasTokenProvider = (SharedAccessSignatureTokenProvider)TokenProvider.CreateSharedAccessSignatureTokenProvider(keyName, key, validityDuration, tokenScope);
        var tokenAudience = new Uri(new Uri(endpoint), EntityNameHelper.FormatSubscriptionPath(entityPath, "mySub")).ToString();

        Console.WriteLine($"Generate token for endpoint: {endpoint} and audience: {tokenAudience} for {validityDuration.TotalMinutes} minutes");
        var sasToken = await sasTokenProvider.GetTokenAsync(tokenAudience, validityDuration);
        var serviceBusConnectionStringBuilder = new ServiceBusConnectionStringBuilder(endpoint, entityPath, sasToken.TokenValue);
        var namespaceConnectionStringFromSas = serviceBusConnectionStringBuilder.GetNamespaceConnectionString();
        var entityConnectionStringFromSas = serviceBusConnectionStringBuilder.GetEntityConnectionString();

        return (
            sasToken.TokenValue,
            namespaceConnectionStringFromSas,
            entityConnectionStringFromSas
        );
    }
}
