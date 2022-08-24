using Azure;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Primitives;
using System.CommandLine;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Web;

namespace dotnet_servicebus.Helpers
{
    public static class ServiceBusHelpers
    {
        public static void PrintParams(
            string connectionString,
            string fqn,
            string keyName,
            string key,
            string topicName = null,
            string subscriptionName = null
        )
        {
            Console.WriteLine($"Running using these values:");
            Console.WriteLine($"---------------------------");
            Console.WriteLine($"Connection String:\t{connectionString}");
            Console.WriteLine($"FQN:\t\t{fqn}");
            Console.WriteLine($"Key Name:\t{keyName}");
            Console.WriteLine($"Key:\t\t{key}");
            Console.WriteLine($"Topic Name:\t{topicName}");
            Console.WriteLine($"Subscription Name:\t{subscriptionName}");
            Console.WriteLine($"---------------------------");
            Console.WriteLine();
        }

        public static string GetConnectionStringFromOptions(
            string connectionString,
            string fqn,
            string entityPath,
            string keyName,
            string key
        )
        {
            ServiceBusConnectionStringBuilder csBuilder;

            if (!string.IsNullOrEmpty(connectionString))
            {
                Console.WriteLine($"Connection string detected. Using...");
                csBuilder = new ServiceBusConnectionStringBuilder(connectionString);
            }
            else
            {
                Console.WriteLine($"Build connection string from FQN, KeyName, and Key");
                csBuilder = new ServiceBusConnectionStringBuilder(fqn, entityPath, keyName, key);
            }

            var cs = csBuilder.GetNamespaceConnectionString();
            Console.WriteLine();
            Console.WriteLine($"Connection String:");
            Console.WriteLine(cs);
            Console.WriteLine();

            return cs;
        }

        public static ServiceBusClient CreateClientFromConnectionString(string connectionString)
        {
            Console.WriteLine($"Create client using Connection String: {connectionString}");
            Console.WriteLine();
            var client = new ServiceBusClient(connectionString);

            return client;
        }

        public static ServiceBusAdministrationClient CreateAdminClientFromConnectionString(string connectionString)
        {
            Console.WriteLine($"Creating administrative service bus client using connection string:");
            Console.WriteLine(connectionString);
            Console.WriteLine();

            var adminClient = new ServiceBusAdministrationClient(connectionString);

            return adminClient;
        }

        public static ServiceBusClient CreateClientFromSasSignature(string fullyQualifiedNamespace, string sasSignature)
        {
            Console.WriteLine($"Create client using: FQN {fullyQualifiedNamespace} and SAS");
            Console.WriteLine();

            var credential = new AzureSasCredential(sasSignature);
            var client = new ServiceBusClient(fullyQualifiedNamespace, credential);

            return client;
        }

        public static async Task<string> CreateSasTokenUsingProvider(
            string endpoint,
            string entityPath,
            string subscriptionName,
            string keyName,
            string key,
            TimeSpan validityDuration,
            TokenScope tokenScope
        )
        {
            // https://github.com/Azure/azure-service-bus/blob/31a0018c122fb39e7016a25706023b45d9622374/samples/DotNet/Microsoft.Azure.ServiceBus/ManagingEntities/SASAuthorizationRule/Program.cs#L70-L72
            var sasTokenProvider = (SharedAccessSignatureTokenProvider)TokenProvider.CreateSharedAccessSignatureTokenProvider(keyName, key, validityDuration, tokenScope);
            Console.WriteLine($"Input Endpoint: {endpoint}");
            var path = string.IsNullOrEmpty(subscriptionName)
                ? entityPath
                : EntityNameHelper.FormatSubscriptionPath(entityPath, subscriptionName);
            var tokenAudienceUri = new Uri(new Uri(endpoint), path);
            Console.WriteLine($"Token Audience: {tokenAudienceUri}");

            var sasToken = await sasTokenProvider.GetTokenAsync(tokenAudienceUri.ToString(), validityDuration);
            return sasToken.TokenValue;
        }

        public static string BuildAudience(
            string fullyQualifiedNamespace
        )
        {
            var builder = new UriBuilder(fullyQualifiedNamespace)
            {
                Scheme = "https",
                Port = -1,
                Fragment = string.Empty,
                Password = string.Empty,
                UserName = string.Empty,
            };

            builder.Path = builder.Path.TrimEnd('/');
            return builder.Uri.AbsoluteUri.ToLowerInvariant();
        }
    }
}
