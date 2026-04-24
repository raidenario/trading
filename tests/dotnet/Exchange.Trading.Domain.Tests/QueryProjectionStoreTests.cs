using Exchange.Platform.Contracts;
using Exchange.Platform.Contracts.Commands;
using Exchange.Platform.Contracts.Events;
using Exchange.Platform.Contracts.ReadModels;
using Exchange.Query.Api;

namespace Exchange.Trading.Domain.Tests;

public sealed class QueryProjectionStoreTests
{
    [Fact]
    public void Projects_instruments_positions_and_enriched_trades_without_breaking_existing_history()
    {
        var store = new QueryProjectionStore();
        var buyer = DemoSeed.Accounts.First().AccountId;
        var seller = DemoSeed.Accounts.Skip(1).First().AccountId;
        var instrument = DemoSeed.Instruments.First(x => x.Symbol == "BTC-USD");
        var buyTradingAccount = DemoSeed.TradingAccounts.First(x => x.AccountId == buyer).TradingAccountId;
        var sellTradingAccount = DemoSeed.TradingAccounts.First(x => x.AccountId == seller).TradingAccountId;
        var buyOrderId = Guid.NewGuid();
        var sellOrderId = Guid.NewGuid();
        var submittedAt = DateTimeOffset.UtcNow;

        store.Apply(new CreateOrderCommand(
            buyOrderId,
            buyer,
            "BTC-USD",
            OrderSide.Buy,
            OrderType.Limit,
            0.5m,
            50000m,
            TimeInForce.Gtc,
            "buy-1",
            submittedAt,
            InstrumentId: instrument.InstrumentId,
            TradingAccountId: buyTradingAccount));

        store.Apply(new CreateOrderCommand(
            sellOrderId,
            seller,
            "BTC-USD",
            OrderSide.Sell,
            OrderType.Limit,
            0.5m,
            50000m,
            TimeInForce.Gtc,
            "sell-1",
            submittedAt,
            InstrumentId: instrument.InstrumentId,
            TradingAccountId: sellTradingAccount));

        store.Apply(new TradeExecuted(
            "trade-1",
            buyOrderId,
            sellOrderId,
            buyer,
            seller,
            "BTC-USD",
            50000m,
            0.5m,
            submittedAt.AddSeconds(1),
            InstrumentId: instrument.InstrumentId,
            BuyTradingAccountId: buyTradingAccount,
            SellTradingAccountId: sellTradingAccount));

        Assert.Contains(store.GetOrderHistory(buyer), item => item.OrderId == buyOrderId);
        Assert.Contains(store.GetInstruments(), item => item.Symbol == "BTC-USD");
        Assert.Contains(store.GetPositions(buyTradingAccount), item => item.NetQuantity == 0.5m && item.InstrumentId == instrument.InstrumentId);
        Assert.Contains(store.GetEnrichedTrades("BTC-USD", 10), item => item.InstrumentId == instrument.InstrumentId && item.BuyTradingAccountId == buyTradingAccount);
        Assert.Contains(store.GetEnrichedOrders(buyer), item => item.TradingAccountId == buyTradingAccount && item.InstrumentId == instrument.InstrumentId);
    }

    [Fact]
    public void Stores_candle_history_and_returns_latest_candle_for_ticker_view()
    {
        var store = new QueryProjectionStore();
        var firstOpen = new DateTimeOffset(2026, 4, 24, 13, 0, 0, TimeSpan.Zero);
        var secondOpen = firstOpen.AddMinutes(1);

        store.Apply(new TickerUpdated("PETR4", 25.50m, 25.49m, 25.51m, 100m, 0m, firstOpen.AddSeconds(59)));
        store.Apply(new CandleUpdated("PETR4", "1m", 25.10m, 25.80m, 24.90m, 25.50m, 400m, firstOpen, firstOpen.AddSeconds(59)));
        store.Apply(new CandleUpdated("PETR4", "1m", 25.50m, 26.10m, 25.40m, 25.90m, 550m, secondOpen, secondOpen.AddSeconds(59)));

        var candles = store.GetCandles("PETR4", "1m", 10).ToArray();

        Assert.Equal(2, candles.Length);
        Assert.Equal(firstOpen, candles[0].OpenedAt);
        Assert.Equal(secondOpen, candles[1].OpenedAt);

        var tickerWithCandle = store.GetTickerWithCandle("PETR4");
        var candleProperty = tickerWithCandle.GetType().GetProperty("candle");
        Assert.NotNull(candleProperty);
        var latestCandle = Assert.IsType<CandleSnapshot>(candleProperty!.GetValue(tickerWithCandle));
        Assert.Equal(25.90m, latestCandle.Close);
        Assert.Equal(secondOpen, latestCandle.OpenedAt);
    }

    [Fact]
    public void Keeps_only_latest_candle_update_for_same_open_time()
    {
        var store = new QueryProjectionStore();
        var openTime = new DateTimeOffset(2026, 4, 24, 13, 0, 0, TimeSpan.Zero);

        store.Apply(new CandleUpdated("PETR4", "1m", 25.10m, 25.80m, 24.90m, 25.50m, 400m, openTime, openTime.AddSeconds(59)));
        store.Apply(new CandleUpdated("PETR4", "1m", 25.10m, 25.95m, 24.85m, 25.70m, 430m, openTime, openTime.AddSeconds(59)));

        var candles = store.GetCandles("PETR4", "1m", 10).ToArray();

        Assert.Single(candles);
        Assert.Equal(25.70m, candles[0].Close);
        Assert.Equal(25.95m, candles[0].High);
        Assert.Equal(430m, candles[0].Volume);
    }
}
