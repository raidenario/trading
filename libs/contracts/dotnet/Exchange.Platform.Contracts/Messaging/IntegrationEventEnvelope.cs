using System.Text.Json;

namespace Exchange.Platform.Contracts.Messaging;

public sealed record IntegrationEventEnvelope(
    string EventType,
    JsonElement Payload,
    DateTimeOffset OccurredAt,
    int SchemaVersion = 1);

public sealed record IntegrationEventEnvelope<TPayload>(
    string EventType,
    TPayload Payload,
    DateTimeOffset OccurredAt,
    int SchemaVersion = 1)
    where TPayload : class;
