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

        public static ServiceBusClient CreateClientFromNamedKey(string fullyQualifiedNamespace, string keyName, string key)
        {
            Console.WriteLine($"Create client using: FQN: {fullyQualifiedNamespace}, key name: {keyName}, and key: {key}");
            Console.WriteLine();
            var credential = new AzureNamedKeyCredential(keyName, key);
            var client = new ServiceBusClient(fullyQualifiedNamespace, credential);

            return client;
        }

        public static ServiceBusClient CreateClientFromSasSignature(string fullyQualifiedNamespace, string sasSignature)
        {
            Console.WriteLine($"Create client using FQN {fullyQualifiedNamespace} and SAS signature: {sasSignature}");
            Console.WriteLine();
            var credential = new AzureSasCredential(sasSignature);
            var client = new ServiceBusClient(fullyQualifiedNamespace, credential);

            return client;
        }

        public static Task<string> CreateSasTokenManually(
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

        public static async Task<string> CreateSasTokenUsingProvider(
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

            var sasToken = await sasTokenProvider.GetTokenAsync(tokenAudience, validityDuration);
            var serviceBusConnectionStringBuilder = new ServiceBusConnectionStringBuilder(endpoint, entityPath, sasToken.TokenValue);
            var namespaceConnectionStringFromSas = serviceBusConnectionStringBuilder.GetNamespaceConnectionString();
            var entityConnectionStringFromSas = serviceBusConnectionStringBuilder.GetEntityConnectionString();

            return sasToken.TokenValue;
        }

        public static (string, string) BuildSignature(
            string fullyQualifiedNamespace,
            string entityName,
            string sharedAccessKeyName,
            string sharedAccessKey,
            DateTimeOffset expirationTime
        )
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(sharedAccessKey));

            var audience = BuildAudience(fullyQualifiedNamespace, entityName);
            var encodedAudience = WebUtility.UrlEncode(audience);
            var expiration = Convert.ToString(ConvertToUnixTime(expirationTime), CultureInfo.InvariantCulture);
            var signature = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes($"{encodedAudience}\n{expiration}")));

            var sasSignature = string.Format(CultureInfo.InvariantCulture, "{0} {1}={2}&{3}={4}&{5}={6}&{7}={8}",
                "SharedAccessSignature",
                "sr",
                encodedAudience,
                "sig",
                WebUtility.UrlEncode(signature),
                "se",
                WebUtility.UrlEncode(expiration),
                "skn",
                WebUtility.UrlEncode(sharedAccessKeyName));

            return (sasSignature, audience);
        }

        private static string BuildAudience(
            string fullyQualifiedNamespace,
            string entityName
        )
        {
            if (string.IsNullOrEmpty(fullyQualifiedNamespace))
            {
                return string.Empty;
            }

            var builder = new UriBuilder(fullyQualifiedNamespace)
            {
                Scheme = "amqps",
                Path = entityName,
                Port = -1,
                Fragment = string.Empty,
                Password = string.Empty,
                UserName = string.Empty,
            };

            builder.Path = builder.Path.TrimEnd('/');
            return builder.Uri.AbsoluteUri.ToLowerInvariant();
        }

        private static long ConvertToUnixTime(DateTimeOffset dateTimeOffset) =>
            Convert.ToInt64((dateTimeOffset - Epoch).TotalSeconds);

        private static readonly DateTimeOffset Epoch = new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero);
    }
}
