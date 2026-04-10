using System.Text.Json;
using System.Text.Json.Serialization;
using Exchange.Platform.Contracts;
using Exchange.Platform.Contracts.Events;

namespace Exchange.Trading.Domain.Tests;

public sealed class TradeExecutedCompatibilityTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public void Deserializes_legacy_trade_payload_without_b3_fields()
    {
        const string payload =
            """
            {
              "tradeId":"trade-0000000001",
              "buyOrderId":"5d48b1c6-54e1-4034-b5d8-18e99b4fb5a1",
              "sellOrderId":"4a95d4f2-7d16-4f98-a724-7cfd55ddc8d7",
              "buyAccountId":"11111111-1111-1111-1111-111111111111",
              "sellAccountId":"22222222-2222-2222-2222-222222222222",
              "symbol":"BTC-USD",
              "price":50000.12,
              "quantity":0.50,
              "executedAt":"2026-04-01T11:01:00Z",
              "schemaVersion":1
            }
            """;

        var trade = JsonSerializer.Deserialize<TradeExecuted>(payload, JsonOptions);

        Assert.NotNull(trade);
        Assert.Equal("BTC-USD", trade!.Symbol);
        Assert.Null(trade.InstrumentId);
        Assert.Null(trade.BuyTradingAccountId);
        Assert.Null(trade.SellTradingAccountId);
    }

    [Fact]
    public void Demo_seed_contains_default_b3_reference_data()
    {
        Assert.Contains(DemoSeed.Instruments, instrument => instrument.Symbol == "BTC-USD" && instrument.AssetClass == AssetClass.Crypto);
        Assert.Contains(DemoSeed.Instruments, instrument => instrument.Symbol == "ETH-USD" && instrument.AssetClass == AssetClass.Crypto);
        Assert.Contains(DemoSeed.Instruments, instrument => instrument.Symbol == "SOL-USD" && instrument.AssetClass == AssetClass.Crypto);
        Assert.Equal(DemoSeed.Accounts.Count, DemoSeed.TradingAccounts.Count);
        Assert.Single(DemoSeed.Participants);
    }
}
