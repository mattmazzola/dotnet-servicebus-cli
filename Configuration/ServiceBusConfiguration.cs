namespace dotnet_servicebus.Configuration;

public sealed class ServiceBusConfiguration
{
    public static readonly string SectionName = "ServiceBus";

    public string? ConnectionString { get; init; }

    public string? FullyQualifiedNamespace { get; init; }

    public string? SharedAccessKeyName { get; init; }

    public string? SharedAccessKey { get; init; }

    public string? TopicName { get; init; }

    public string? SubscriptionName { get; init; }
}
