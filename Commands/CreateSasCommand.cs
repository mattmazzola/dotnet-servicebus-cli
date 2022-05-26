using Azure;
using Azure.Messaging.ServiceBus;
using dotnet_servicebus.Helpers;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Primitives;
using System.CommandLine;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Web;

namespace dotnet_servicebus.Commands;

public class CrateSasCommand
{
    public static Command GetCommand(Option fqnOption, Option keyOption, Option topicNameOption)
    {
        var keyNameArgument = new Argument<string>("keyname", "Key Name");
        var command = new Command("createSasToken", "Create SAS Token")
        {
            keyNameArgument,
        };
        command.AddAlias("csas");
        command.SetHandler(async (
            string topicName,
            string fqn,
            string key,
            string keyName
            ) =>
        {
            var entityPath = "games";
            var validityDuration = TimeSpan.FromMinutes(30);
            var resourceUri = $"https://{fqn}/";

            Console.WriteLine($"Generate SAS Signature using params:");
            Console.WriteLine($"FQN:\t{fqn}");
            Console.WriteLine($"EntityPath:\t{entityPath}");
            Console.WriteLine($"Audience:\t{TokenScope.Namespace}");
            Console.WriteLine($"Duraiont:\t{validityDuration.TotalMinutes} minutes");
            Console.WriteLine();
            Console.WriteLine($"1. Creating SAS Signature manually:");
            Console.WriteLine();

            var sasTokenManual = await ServiceBusHelpers.CreateSasTokenManually(
                resourceUri,
                keyName,
                key,
                validityDuration
            );

            Console.WriteLine(sasTokenManual);
            Console.WriteLine();
            Console.WriteLine($"2. Creating SAS Signature manually (From GitHub Suggestion):");
            Console.WriteLine();

            var (sasTokenManualGithub, audience) = ServiceBusHelpers.BuildSignature(
                fqn,
                entityPath,
                keyName,
                key,
                DateTimeOffset.Now.Add(validityDuration)
            );

            Console.WriteLine($"Audience: {audience}");
            Console.WriteLine(sasTokenManualGithub);
            Console.WriteLine();
            Console.WriteLine($"3. Generate SAS Signature using TokenProvider");
            Console.WriteLine();

            var sasToken = await ServiceBusHelpers.CreateSasTokenUsingProvider(
                resourceUri,
                "games",
                keyName,
                key,
                validityDuration,
                TokenScope.Namespace
            );

            Console.WriteLine(sasToken);
            Console.WriteLine();
        },
        topicNameOption,
        fqnOption,
        keyOption,
        keyNameArgument
        );

        return command;
    }
}
