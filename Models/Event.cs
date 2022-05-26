namespace dotnet_servicebus.Models;

public class Event
{
    public string EventType { get; init; } = "UndefinedEventType";

    public string Id { get; init; } = Guid.NewGuid().ToString();

    public string Message { get; init; } = "Default Message";

    public string AgentId { get; init; } = string.Empty;
}
