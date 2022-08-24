namespace dotnet_servicebus.Configuration;

public sealed class EventHubConfiguration
{
    public static readonly string SectionName = "EventHub";

    public string? ConnectionString { get; init; }

    public string? FullyQualifiedNamespace { get; init; }

    public string? SendListenSharedAccessKeyName { get; init; }

    public string? SendListenSharedAccessKey { get; init; }

    public string? EventHubName { get; init; }
}
