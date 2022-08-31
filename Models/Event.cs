namespace dotnet_servicebus.Models;

public class Event
{
    public string EventType { get; init; } = "UndefinedEventType";

    public string Id { get; init; } = Guid.NewGuid().ToString();

    public string GameId { get; init; } = Guid.NewGuid().ToString();

    public string TaskId { get; init; } = Guid.NewGuid().ToString();

    public string TournamentId { get; init; } = Guid.NewGuid().ToString();

    public string Source { get; init; } = "dotnet-servicebus-cli";

    public string ProducedAtDatetime { get; init; } = DateTimeOffset.UtcNow.ToString("o");

    public string? RoleId { get; init; }

    public string? GroupId { get; init; }

    public string? AgentSubscriptionFilterValue { get; init; } = string.Empty;

    public string? Message { get; init; }
}
