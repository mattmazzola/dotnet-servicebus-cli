using Azure.Messaging.EventHubs;
using System;

namespace dotnet_servicebus.Models;

public class ReceivedEvent
{
    public EventData EventData { get; init; }

    public DateTimeOffset ReceivedDatetimeOffset { get; init; }

    public Event EventBody { get; init; }

    public TimeSpan ProducedEnqueuedLatency
    {
        get
        {
            return EventData.EnqueuedTime - DateTimeOffset.Parse(EventBody.ProducedAtDatetime);
        }
    }

    public TimeSpan ProducedReceivedLatency
    {
        get
        {
            return ReceivedDatetimeOffset - DateTimeOffset.Parse(EventBody.ProducedAtDatetime);
        }
    }
}
