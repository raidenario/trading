using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Exchange.Platform.Contracts.Messaging;

namespace Exchange.Query.Api;

public interface IRealtimeEventForwarder
{
    void ForwardIfRealtimeTopic(string topic, string envelopeJson);
}

public sealed class RealtimeEventForwarder(
    HttpClient httpClient,
    IConfiguration configuration,
    ILogger<RealtimeEventForwarder> logger) : IRealtimeEventForwarder
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly HashSet<string> RealtimeTopics = new(StringComparer.OrdinalIgnoreCase)
    {
        KafkaTopics.MarketDataEvents,
        KafkaTopics.MatchingEvents
    };

    private readonly bool _enabled = configuration.GetValue("RealtimeGateway:Enabled", true);
    private readonly string _eventsPath = configuration["RealtimeGateway:EventsPath"] ?? "/internal/events";

    public void ForwardIfRealtimeTopic(string topic, string envelopeJson)
    {
        if (!ShouldForward(topic, _enabled))
        {
            logger.LogTrace("Realtime forward skipped for Topic={Topic} Enabled={Enabled}", topic, _enabled);
            return;
        }

        var description = Describe(envelopeJson);
        logger.LogInformation(
            "Realtime forward enqueue Topic={Topic} EventType={EventType} Symbol={Symbol} Details={Details}",
            topic,
            description.EventType,
            description.Symbol,
            description);

        _ = Task.Run(async () =>
        {
            try
            {
                using var document = JsonDocument.Parse(envelopeJson);
                using var response = await httpClient.PostAsJsonAsync(_eventsPath, document.RootElement.Clone());
                if (!response.IsSuccessStatusCode)
                {
                    logger.LogWarning(
                        "Realtime gateway rejected forwarded event from {Topic}: HTTP {StatusCode}",
                        topic,
                        response.StatusCode);
                    return;
                }

                logger.LogInformation(
                    "Realtime forward delivered Topic={Topic} EventType={EventType} Symbol={Symbol}",
                    topic,
                    description.EventType,
                    description.Symbol);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Realtime gateway forwarding failed for topic {Topic}.", topic);
            }
        });
    }

    public static bool ShouldForward(string topic, bool enabled) =>
        enabled && RealtimeTopics.Contains(topic);

    public static RealtimeEventDescription Describe(string envelopeJson)
    {
        try
        {
            var envelope = JsonSerializer.Deserialize<IntegrationEventEnvelope>(envelopeJson, JsonOptions);
            if (envelope is null)
            {
                return new(null, null, null, null, null);
            }

            var payload = envelope.Payload;
            return new(
                envelope.EventType,
                GetString(payload, "Symbol"),
                GetString(payload, "TradeId"),
                GetDecimal(payload, "Price") ?? GetDecimal(payload, "LastPrice"),
                GetDecimal(payload, "Quantity"));
        }
        catch (JsonException)
        {
            return new(null, null, null, null, null);
        }
    }

    private static string? GetString(JsonElement payload, string propertyName)
    {
        if (payload.ValueKind != JsonValueKind.Object ||
            !payload.TryGetProperty(propertyName, out var property) ||
            property.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return property.GetString();
    }

    private static decimal? GetDecimal(JsonElement payload, string propertyName)
    {
        if (payload.ValueKind != JsonValueKind.Object ||
            !payload.TryGetProperty(propertyName, out var property) ||
            property.ValueKind != JsonValueKind.Number ||
            !property.TryGetDecimal(out var value))
        {
            return null;
        }

        return value;
    }
}

public sealed record RealtimeEventDescription(
    string? EventType,
    string? Symbol,
    string? TradeId,
    decimal? Price,
    decimal? Quantity);
