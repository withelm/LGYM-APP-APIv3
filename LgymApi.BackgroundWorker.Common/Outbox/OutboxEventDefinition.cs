using System.Text.Json;
using LgymApi.Domain.Notifications;

namespace LgymApi.BackgroundWorker.Common.Outbox;

public abstract record OutboxEventDefinition(OutboxEventType EventType)
{
    public string EventTypeValue => EventType.Value;
}

public sealed record OutboxEventDefinition<TPayload>(OutboxEventType EventType) : OutboxEventDefinition(EventType)
{
    private static readonly JsonSerializerOptions DeserializeOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public string Serialize(TPayload payload, JsonSerializerOptions? serializerOptions = null)
    {
        return JsonSerializer.Serialize(payload, serializerOptions);
    }

    public TPayload? Deserialize(string payloadJson, JsonSerializerOptions? serializerOptions = null)
    {
        return JsonSerializer.Deserialize<TPayload>(payloadJson, serializerOptions ?? DeserializeOptions);
    }
}
