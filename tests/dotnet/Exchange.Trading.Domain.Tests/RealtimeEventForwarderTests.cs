using Exchange.Query.Api;

namespace Exchange.Trading.Domain.Tests;

public sealed class RealtimeEventForwarderTests
{
    [Fact]
    public void ShouldForward_Allows_Only_Realtime_Public_Event_Topics_When_Enabled()
    {
        Assert.True(RealtimeEventForwarder.ShouldForward("marketdata-events", enabled: true));
        Assert.True(RealtimeEventForwarder.ShouldForward("matching-events", enabled: true));

        Assert.False(RealtimeEventForwarder.ShouldForward("order-commands", enabled: true));
        Assert.False(RealtimeEventForwarder.ShouldForward("ledger-events", enabled: true));
        Assert.False(RealtimeEventForwarder.ShouldForward("marketdata-events", enabled: false));
    }

    [Fact]
    public void Describe_Extracts_Event_Metadata_For_Logs()
    {
        var json = """
            {
              "EventType": "TradeExecuted",
              "Payload": {
                "TradeId": "trade-1",
                "Symbol": "PETR4",
                "Price": 25.63,
                "Quantity": 100
              }
            }
            """;

        var description = RealtimeEventForwarder.Describe(json);

        Assert.Equal("TradeExecuted", description.EventType);
        Assert.Equal("PETR4", description.Symbol);
        Assert.Equal("trade-1", description.TradeId);
        Assert.Equal(25.63m, description.Price);
        Assert.Equal(100m, description.Quantity);
    }
}
