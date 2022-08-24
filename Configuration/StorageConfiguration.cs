namespace dotnet_servicebus.Configuration;

public sealed class StorageConfiguration
{
    public static readonly string SectionName = "Storage";

    public string? ConnectionString { get; init; }

    public string? ContainerName { get; init; }
}
