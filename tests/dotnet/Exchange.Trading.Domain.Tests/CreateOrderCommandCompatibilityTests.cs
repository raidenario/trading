using System.Text.Json;
using System.Text.Json.Serialization;
using Exchange.Platform.Contracts;
using Exchange.Platform.Contracts.Commands;

namespace Exchange.Trading.Domain.Tests;

public sealed class CreateOrderCommandCompatibilityTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public void Deserializes_legacy_payload_without_b3_fields()
    {
        const string payload =
            """
            {
              "orderId":"5d48b1c6-54e1-4034-b5d8-18e99b4fb5a1",
              "accountId":"11111111-1111-1111-1111-111111111111",
              "symbol":"BTC-USD",
              "side":"Buy",
              "type":"Limit",
              "quantity":1.25,
              "price":50000.12,
              "timeInForce":"Gtc",
              "clientOrderId":"legacy-order",
              "submittedAt":"2026-04-01T11:00:00Z",
              "schemaVersion":1
            }
            """;

        var command = JsonSerializer.Deserialize<CreateOrderCommand>(payload, JsonOptions);

        Assert.NotNull(command);
        Assert.Equal("BTC-USD", command!.Symbol);
        Assert.Null(command.InstrumentId);
        Assert.Null(command.TradingAccountId);
        Assert.Equal(OrderSource.Api, command.SourceSystem);
    }

    [Fact]
    public void Serializes_optional_b3_fields_when_available()
    {
        var command = new CreateOrderCommand(
            Guid.Parse("5d48b1c6-54e1-4034-b5d8-18e99b4fb5a1"),
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "BTC-USD",
            OrderSide.Buy,
            OrderType.Limit,
            1.25m,
            50000.12m,
            TimeInForce.Gtc,
            "client-001",
            DateTimeOffset.Parse("2026-04-01T11:00:00Z"),
            InstrumentId: Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001"),
            TradingAccountId: Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001"),
            SourceSystem: OrderSource.Api,
            ExecutionInstructions: new Dictionary<string, string> { ["postOnly"] = "false" });

        var json = JsonSerializer.Serialize(command, JsonOptions);

        Assert.Contains("\"instrumentId\":\"aaaaaaaa-0000-0000-0000-000000000001\"", json);
        Assert.Contains("\"tradingAccountId\":\"bbbbbbbb-0000-0000-0000-000000000001\"", json);
        Assert.Contains("\"sourceSystem\":\"Api\"", json);
        Assert.Contains("\"executionInstructions\":{\"postOnly\":\"false\"}", json);
    }
}
