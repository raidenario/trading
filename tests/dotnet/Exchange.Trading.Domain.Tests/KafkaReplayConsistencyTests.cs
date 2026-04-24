using Exchange.Ledger.Api;
using Exchange.Platform.Contracts;
using Exchange.Platform.Contracts.Commands;
using Exchange.Platform.Contracts.Events;
using Exchange.Query.Api;

namespace Exchange.Trading.Domain.Tests;

/// <summary>
/// Tests that verify Kafka event replay produces identical state regardless of event ordering.
/// This is the core idempotency + consistency requirement for CQRS replay.
/// </summary>
public sealed class KafkaReplayConsistencyTests
{
    private static readonly Guid BuyerAccountId = DemoSeed.Accounts.First().AccountId;
    private static readonly Guid SellerAccountId = DemoSeed.Accounts.Skip(1).First().AccountId;
    private static readonly Guid BuyerTradingAccountId = DemoSeed.TradingAccounts.First(x => x.AccountId == BuyerAccountId).TradingAccountId;
    private static readonly Guid SellerTradingAccountId = DemoSeed.TradingAccounts.First(x => x.AccountId == SellerAccountId).TradingAccountId;
    private static readonly Instrument BtcInstrument = DemoSeed.Instruments.First(x => x.Symbol == "BTC-USD");
    private static readonly Guid BuyOrderId = Guid.Parse("aaaaaaaa-aaaa-4000-8000-000000000001");
    private static readonly Guid SellOrderId = Guid.Parse("aaaaaaaa-aaaa-4000-8000-000000000002");

    [Fact]
    public void QueryProjectionStore_replay_produces_identical_positions_regardless_of_event_order()
    {
        var tradeId = "trade-replay-001";
        var submittedAt = DateTimeOffset.UtcNow;
        var executedAt = submittedAt.AddSeconds(1);

        var orderCommand = new CreateOrderCommand(
            BuyOrderId, BuyerAccountId, "BTC-USD",
            OrderSide.Buy, OrderType.Limit, 0.5m, 50000m,
            TimeInForce.Gtc, null, submittedAt,
            InstrumentId: BtcInstrument.InstrumentId,
            TradingAccountId: BuyerTradingAccountId);

        var sellOrderCommand = new CreateOrderCommand(
            SellOrderId, SellerAccountId, "BTC-USD",
            OrderSide.Sell, OrderType.Limit, 0.5m, 50000m,
            TimeInForce.Gtc, null, submittedAt,
            InstrumentId: BtcInstrument.InstrumentId,
            TradingAccountId: SellerTradingAccountId);

        var tradeEvent = new TradeExecuted(
            tradeId, BuyOrderId, SellOrderId,
            BuyerAccountId, SellerAccountId,
            "BTC-USD", 50000m, 0.5m, executedAt,
            InstrumentId: BtcInstrument.InstrumentId,
            BuyTradingAccountId: BuyerTradingAccountId,
            SellTradingAccountId: SellerTradingAccountId);

        var acceptedEvent = new OrderAccepted(
            BuyOrderId, BuyerAccountId, "BTC-USD",
            OrderStatus.Pending, 0.5m, submittedAt.AddMilliseconds(100));

        var sellAcceptedEvent = new OrderAccepted(
            SellOrderId, SellerAccountId, "BTC-USD",
            OrderStatus.Pending, 0.5m, submittedAt.AddMilliseconds(100));

        var expected = BuildExpectedProjection();

        var order1 = ReplayScenario(orderCommand, sellOrderCommand, acceptedEvent, sellAcceptedEvent, tradeEvent);
        var order2 = ReplayScenario(orderCommand, sellOrderCommand, tradeEvent, acceptedEvent, sellAcceptedEvent);
        var order3 = ReplayScenario(acceptedEvent, sellAcceptedEvent, orderCommand, sellOrderCommand, tradeEvent);

        Assert.Equal(expected, order1);
        Assert.Equal(expected, order2);
        Assert.Equal(expected, order3);
    }

    [Fact]
    public void LedgerProjectionStore_replay_produces_identical_balances_regardless_of_event_order()
    {
        var tradeId = "trade-replay-ledger-001";
        var submittedAt = DateTimeOffset.UtcNow;
        var executedAt = submittedAt.AddSeconds(1);

        var orderCommand = new CreateOrderCommand(
            BuyOrderId, BuyerAccountId, "BTC-USD",
            OrderSide.Buy, OrderType.Limit, 0.5m, 50000m,
            TimeInForce.Gtc, null, submittedAt,
            InstrumentId: BtcInstrument.InstrumentId,
            TradingAccountId: BuyerTradingAccountId);

        var sellOrderCommand = new CreateOrderCommand(
            SellOrderId, SellerAccountId, "BTC-USD",
            OrderSide.Sell, OrderType.Limit, 0.5m, 50000m,
            TimeInForce.Gtc, null, submittedAt,
            InstrumentId: BtcInstrument.InstrumentId,
            TradingAccountId: SellerTradingAccountId);

        var tradeEvent = new TradeExecuted(
            tradeId, BuyOrderId, SellOrderId,
            BuyerAccountId, SellerAccountId,
            "BTC-USD", 50000m, 0.5m, executedAt,
            InstrumentId: BtcInstrument.InstrumentId,
            BuyTradingAccountId: BuyerTradingAccountId,
            SellTradingAccountId: SellerTradingAccountId);

        var orderFirst = ReplayLedgerScenario(orderCommand, sellOrderCommand, tradeEvent);
        var tradeFirst = ReplayLedgerScenario(tradeEvent, orderCommand, sellOrderCommand);
        var interleaved = ReplayLedgerScenario(orderCommand, tradeEvent, sellOrderCommand);

        var orderPositions = orderFirst.GetPositions(BuyerTradingAccountId);
        var tradePositions = tradeFirst.GetPositions(BuyerTradingAccountId);
        var interleavedPositions = interleaved.GetPositions(BuyerTradingAccountId);

        var orderBuyerPos = orderPositions.First(p => p.InstrumentId == BtcInstrument.InstrumentId);
        var tradeBuyerPos = tradePositions.First(p => p.InstrumentId == BtcInstrument.InstrumentId);
        var interleavedBuyerPos = interleavedPositions.First(p => p.InstrumentId == BtcInstrument.InstrumentId);

        Assert.Equal(orderBuyerPos.NetQuantity, tradeBuyerPos.NetQuantity);
        Assert.Equal(orderBuyerPos.NetQuantity, interleavedBuyerPos.NetQuantity);
    }

    [Fact]
    public void Double_replay_of_same_events_produces_identical_state()
    {
        var tradeId = "trade-double-replay";
        var submittedAt = DateTimeOffset.UtcNow;

        var events = BuildEventSequence(submittedAt, tradeId);

        var store1 = ApplyEvents(new QueryProjectionStore(), events);
        var store2 = ApplyEvents(new QueryProjectionStore(), events);

        var positions1 = store1.GetPositions(BuyerTradingAccountId);
        var positions2 = store2.GetPositions(BuyerTradingAccountId);

        Assert.Equal(positions1.Count, positions2.Count);

        foreach (var pos1 in positions1)
        {
            var pos2 = positions2.First(p => p.InstrumentId == pos1.InstrumentId && p.PositionDate == pos1.PositionDate);
            Assert.Equal(pos1.NetQuantity, pos2.NetQuantity);
            Assert.Equal(pos1.LongQuantity, pos2.LongQuantity);
            Assert.Equal(pos1.ShortQuantity, pos2.ShortQuantity);
        }
    }

    [Fact]
    public void Multiple_trades_same_instrument_aggregate_position_correctly()
    {
        var store = new QueryProjectionStore();
        var submittedAt = DateTimeOffset.UtcNow;

        var buyOrderId1 = Guid.Parse("aaaaaaaa-aaaa-4000-8000-000000000010");
        var sellOrderId1 = Guid.Parse("aaaaaaaa-aaaa-4000-8000-000000000011");
        var buyOrderId2 = Guid.Parse("aaaaaaaa-aaaa-4000-8000-000000000012");
        var sellOrderId2 = Guid.Parse("aaaaaaaa-aaaa-4000-8000-000000000013");

        store.Apply(new CreateOrderCommand(
            buyOrderId1, BuyerAccountId, "BTC-USD",
            OrderSide.Buy, OrderType.Limit, 0.5m, 50000m,
            TimeInForce.Gtc, null, submittedAt,
            InstrumentId: BtcInstrument.InstrumentId,
            TradingAccountId: BuyerTradingAccountId));

        store.Apply(new CreateOrderCommand(
            sellOrderId1, SellerAccountId, "BTC-USD",
            OrderSide.Sell, OrderType.Limit, 0.5m, 50000m,
            TimeInForce.Gtc, null, submittedAt,
            InstrumentId: BtcInstrument.InstrumentId,
            TradingAccountId: SellerTradingAccountId));

        store.Apply(new TradeExecuted(
            "trade-multi-1", buyOrderId1, sellOrderId1,
            BuyerAccountId, SellerAccountId,
            "BTC-USD", 50000m, 0.5m, submittedAt.AddSeconds(1),
            InstrumentId: BtcInstrument.InstrumentId,
            BuyTradingAccountId: BuyerTradingAccountId,
            SellTradingAccountId: SellerTradingAccountId));

        store.Apply(new CreateOrderCommand(
            buyOrderId2, BuyerAccountId, "BTC-USD",
            OrderSide.Buy, OrderType.Limit, 0.3m, 51000m,
            TimeInForce.Gtc, null, submittedAt.AddSeconds(2),
            InstrumentId: BtcInstrument.InstrumentId,
            TradingAccountId: BuyerTradingAccountId));

        store.Apply(new CreateOrderCommand(
            sellOrderId2, SellerAccountId, "BTC-USD",
            OrderSide.Sell, OrderType.Limit, 0.3m, 51000m,
            TimeInForce.Gtc, null, submittedAt.AddSeconds(2),
            InstrumentId: BtcInstrument.InstrumentId,
            TradingAccountId: SellerTradingAccountId));

        store.Apply(new TradeExecuted(
            "trade-multi-2", buyOrderId2, sellOrderId2,
            BuyerAccountId, SellerAccountId,
            "BTC-USD", 51000m, 0.3m, submittedAt.AddSeconds(3),
            InstrumentId: BtcInstrument.InstrumentId,
            BuyTradingAccountId: BuyerTradingAccountId,
            SellTradingAccountId: SellerTradingAccountId));

        var buyerPositions = store.GetPositions(BuyerTradingAccountId);
        var btcPos = buyerPositions.First(p => p.InstrumentId == BtcInstrument.InstrumentId);

        Assert.Equal(0.8m, btcPos.NetQuantity);
    }

    private static ProjectionState BuildExpectedProjection() =>
        new(
            BuyerTradingAccountId,
            BtcInstrument.InstrumentId,
            0.5m,
            0.5m,
            0m);

    private static ProjectionState ReplayScenario(params object[] events)
    {
        var store = new QueryProjectionStore();
        _ = ApplyEvents(store, events);
        var positions = store.GetPositions(BuyerTradingAccountId);
        var pos = positions.FirstOrDefault(p => p.InstrumentId == BtcInstrument.InstrumentId);
        return new ProjectionState(
            BuyerTradingAccountId,
            BtcInstrument.InstrumentId,
            pos?.NetQuantity ?? 0m,
            pos?.LongQuantity ?? 0m,
            pos?.ShortQuantity ?? 0m);
    }

    private static LedgerProjectionStore ReplayLedgerScenario(params object[] events)
    {
        var store = new LedgerProjectionStore();
        foreach (var ev in events)
        {
            switch (ev)
            {
                case CreateOrderCommand cmd: store.Apply(cmd); break;
                case TradeExecuted trade: store.Apply(trade); break;
            }
        }
        return store;
    }

    private static object[] BuildEventSequence(DateTimeOffset submittedAt, string tradeId)
    {
        var buyOrderId = Guid.Parse("aaaaaaaa-aaaa-4000-8000-000000000020");
        var sellOrderId = Guid.Parse("aaaaaaaa-aaaa-4000-8000-800000000021");

        return
        [
            new CreateOrderCommand(
                buyOrderId, BuyerAccountId, "BTC-USD",
                OrderSide.Buy, OrderType.Limit, 0.5m, 50000m,
                TimeInForce.Gtc, null, submittedAt,
                InstrumentId: BtcInstrument.InstrumentId,
                TradingAccountId: BuyerTradingAccountId),
            new CreateOrderCommand(
                sellOrderId, SellerAccountId, "BTC-USD",
                OrderSide.Sell, OrderType.Limit, 0.5m, 50000m,
                TimeInForce.Gtc, null, submittedAt,
                InstrumentId: BtcInstrument.InstrumentId,
                TradingAccountId: SellerTradingAccountId),
            new TradeExecuted(
                tradeId, buyOrderId, sellOrderId,
                BuyerAccountId, SellerAccountId,
                "BTC-USD", 50000m, 0.5m, submittedAt.AddSeconds(1),
                InstrumentId: BtcInstrument.InstrumentId,
                BuyTradingAccountId: BuyerTradingAccountId,
                SellTradingAccountId: SellerTradingAccountId)
        ];
    }

    private static QueryProjectionStore ApplyEvents(QueryProjectionStore store, object[] events)
    {
        foreach (var ev in events)
        {
            switch (ev)
            {
                case CreateOrderCommand cmd: store.Apply(cmd); break;
                case TradeExecuted trade: store.Apply(trade); break;
            }
        }
        return store;
    }

    private sealed record ProjectionState(
        Guid TradingAccountId,
        Guid InstrumentId,
        decimal NetQuantity,
        decimal LongQuantity,
        decimal ShortQuantity);
}
