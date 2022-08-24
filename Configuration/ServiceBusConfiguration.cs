namespace dotnet_servicebus.Configuration;

public sealed class ServiceBusConfiguration
{
    public static readonly string SectionName = "ServiceBus";

    public string? ConnectionString { get; init; }

    public string? FullyQualifiedNamespace { get; init; }

    public string? ManageSharedAccessKeyName { get; init; }

    public string? ManageSharedAccessKey { get; init; }

    public string? ListenSharedAccessKeyName { get; init; }

    public string? ListenSharedAccessKey { get; init; }

    public string? TopicName { get; init; }

    public string? SubscriptionName { get; init; }
}
