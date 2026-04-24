using Exchange.Ledger.Api;
using Exchange.Platform.Contracts;
using Exchange.Platform.Contracts.Commands;
using Exchange.Platform.Contracts.Events;
using Exchange.Query.Api;
using Exchange.Trading.Application.Abstractions;
using Exchange.Trading.Application.Models;
using Exchange.Trading.Application.Services;
using Exchange.Trading.Domain.Entities;
using Exchange.Trading.Domain.ValueObjects;
using Exchange.Trading.Infrastructure.Repositories;
using DomainOrderStatus = Exchange.Trading.Domain.Enums.OrderStatus;

namespace Exchange.Trading.Domain.Tests;

/// <summary>
/// Tests that verify BTC-USD, ETH-USD, SOL-USD compatibility with 8 decimal precision.
/// </summary>
public sealed class CryptoPairCompatibilityTests
{
    private static readonly Guid BuyerAccountId = DemoSeed.Accounts.First().AccountId;
    private static readonly Guid BuyerTradingAccountId = DemoSeed.TradingAccounts.First(x => x.AccountId == BuyerAccountId).TradingAccountId;
    private static readonly Guid SellerAccountId = DemoSeed.Accounts.Skip(1).First().AccountId;
    private static readonly Guid SellerTradingAccountId = DemoSeed.TradingAccounts.First(x => x.AccountId == SellerAccountId).TradingAccountId;

    [Fact]
    public void BTC_USD_order_preserves_8_decimal_precision()
    {
        var catalog = new StaticInstrumentCatalog(DemoSeed.Instruments);
        var resolver = new DemoTradingAccountResolver(DemoSeed.TradingAccounts);
        var btc = DemoSeed.Instruments.First(x => x.Symbol == "BTC-USD");

        Assert.Equal(0.00000001m, btc.LotSize);
        Assert.Equal(0.01m, btc.TickSize);
        Assert.Equal(2, btc.PricePrecision);
        Assert.Equal(8, btc.QuantityPrecision);
        Assert.Equal(TradingStatus.Active, btc.TradingStatus);
    }

    [Fact]
    public void ETH_USD_order_preserves_8_decimal_precision()
    {
        var eth = DemoSeed.Instruments.First(x => x.Symbol == "ETH-USD");

        Assert.Equal(0.00000001m, eth.LotSize);
        Assert.Equal(0.01m, eth.TickSize);
        Assert.Equal(8, eth.QuantityPrecision);
        Assert.Equal(TradingStatus.Active, eth.TradingStatus);
    }

    [Fact]
    public void SOL_USD_order_preserves_8_decimal_precision()
    {
        var sol = DemoSeed.Instruments.First(x => x.Symbol == "SOL-USD");

        Assert.Equal(0.00000001m, sol.LotSize);
        Assert.Equal(0.0001m, sol.TickSize);
        Assert.Equal(4, sol.PricePrecision);
        Assert.Equal(8, sol.QuantityPrecision);
        Assert.Equal(TradingStatus.Active, sol.TradingStatus);
    }

    [Fact]
    public void Crypto_trade_updates_positions_with_fractional_quantities()
    {
        var store = new QueryProjectionStore();
        var submittedAt = DateTimeOffset.UtcNow;

        var buyOrderId = Guid.Parse("aaaaaaaa-aaaa-4000-9000-000000000001");
        var sellOrderId = Guid.Parse("aaaaaaaa-aaaa-4000-9000-000000000002");
        var btc = DemoSeed.Instruments.First(x => x.Symbol == "BTC-USD");

        store.Apply(new CreateOrderCommand(
            buyOrderId, BuyerAccountId, "BTC-USD",
            OrderSide.Buy, OrderType.Limit, 0.12345678m, 50000m,
            TimeInForce.Gtc, null, submittedAt,
            InstrumentId: btc.InstrumentId,
            TradingAccountId: BuyerTradingAccountId));

        store.Apply(new CreateOrderCommand(
            sellOrderId, SellerAccountId, "BTC-USD",
            OrderSide.Sell, OrderType.Limit, 0.12345678m, 50000m,
            TimeInForce.Gtc, null, submittedAt,
            InstrumentId: btc.InstrumentId,
            TradingAccountId: SellerTradingAccountId));

        store.Apply(new TradeExecuted(
            "trade-crypto-001", buyOrderId, sellOrderId,
            BuyerAccountId, SellerAccountId,
            "BTC-USD", 50000m, 0.12345678m, submittedAt.AddSeconds(1),
            InstrumentId: btc.InstrumentId,
            BuyTradingAccountId: BuyerTradingAccountId,
            SellTradingAccountId: SellerTradingAccountId));

        var buyerPositions = store.GetPositions(BuyerTradingAccountId);
        var btcPos = buyerPositions.First(p => p.InstrumentId == btc.InstrumentId);

        Assert.Equal(0.12345678m, btcPos.NetQuantity);
    }

    [Fact]
    public void Ledger_handles_crypto_balances_with_8_decimal_precision()
    {
        var store = new LedgerProjectionStore();
        var submittedAt = DateTimeOffset.UtcNow;

        var buyOrderId = Guid.Parse("aaaaaaaa-aaaa-4000-9000-000000000010");
        var sellOrderId = Guid.Parse("aaaaaaaa-aaaa-4000-9000-000000000011");
        var btc = DemoSeed.Instruments.First(x => x.Symbol == "BTC-USD");

        store.Apply(new CreateOrderCommand(
            buyOrderId, BuyerAccountId, "BTC-USD",
            OrderSide.Buy, OrderType.Limit, 0.00000001m, 50000m,
            TimeInForce.Gtc, null, submittedAt,
            InstrumentId: btc.InstrumentId,
            TradingAccountId: BuyerTradingAccountId));

        store.Apply(new CreateOrderCommand(
            sellOrderId, SellerAccountId, "BTC-USD",
            OrderSide.Sell, OrderType.Limit, 0.00000001m, 50000m,
            TimeInForce.Gtc, null, submittedAt,
            InstrumentId: btc.InstrumentId,
            TradingAccountId: SellerTradingAccountId));

        var adjustments = store.Apply(new TradeExecuted(
            "trade-satoshi", buyOrderId, sellOrderId,
            BuyerAccountId, SellerAccountId,
            "BTC-USD", 50000m, 0.00000001m, submittedAt.AddSeconds(1),
            InstrumentId: btc.InstrumentId,
            BuyTradingAccountId: BuyerTradingAccountId,
            SellTradingAccountId: SellerTradingAccountId));

        var buyerPositions = store.GetPositions(BuyerTradingAccountId);
        var btcPos = buyerPositions.First(p => p.InstrumentId == btc.InstrumentId);

        Assert.Equal(0.00000001m, btcPos.NetQuantity);
        Assert.NotEmpty(adjustments);
    }

    [Fact]
    public void OrderCommandService_resolves_crypto_instruments_correctly()
    {
        var repository = new InMemoryOrderRepository();
        var catalog = new StaticInstrumentCatalog(DemoSeed.Instruments);
        var resolver = new DemoTradingAccountResolver(DemoSeed.TradingAccounts);

        foreach (var symbol in new[] { "BTC-USD", "ETH-USD", "SOL-USD" })
        {
            var service = new OrderCommandService(repository, new AlwaysAcceptClient(), catalog, resolver);
            var instrument = DemoSeed.Instruments.First(x => x.Symbol == symbol);
            var orderId = Guid.NewGuid();

            var command = new CreateOrderCommand(
                orderId, BuyerAccountId, symbol,
                OrderSide.Buy, OrderType.Limit, instrument.LotSize, 50000m,
                TimeInForce.Gtc, null, DateTimeOffset.UtcNow,
                InstrumentId: instrument.InstrumentId,
                TradingAccountId: BuyerTradingAccountId);

            var result = service.CreateAsync(command, CancellationToken.None).Result;

            Assert.Equal(DomainOrderStatus.Pending, result.Status);
        }
    }

    [Fact]
    public void All_three_crypto_pairs_exist_in_seed_with_valid_rules()
    {
        var catalog = new StaticInstrumentCatalog(
            DemoSeed.Instruments,
            DemoSeed.InstrumentTradingRules,
            DemoSeed.InstrumentMarketConfigs,
            DemoSeed.InstrumentStatuses);

        foreach (var symbol in new[] { "BTC-USD", "ETH-USD", "SOL-USD" })
        {
            var instrument = catalog.ResolveAsync(symbol, null, CancellationToken.None).Result;
            Assert.NotNull(instrument);
            Assert.Equal(TradingStatus.Active, instrument!.Status.Status);
            Assert.True(instrument.TradingRule.MatchingEnabled);
        }
    }

    [Fact]
    public void QueryProjectionStore_enriched_trade_carries_instrument_id_for_crypto()
    {
        var store = new QueryProjectionStore();
        var submittedAt = DateTimeOffset.UtcNow;
        var buyOrderId = Guid.Parse("aaaaaaaa-aaaa-4000-9000-000000000030");
        var sellOrderId = Guid.Parse("aaaaaaaa-aaaa-4000-9000-000000000031");
        var btc = DemoSeed.Instruments.First(x => x.Symbol == "BTC-USD");

        store.Apply(new CreateOrderCommand(
            buyOrderId, BuyerAccountId, "BTC-USD",
            OrderSide.Buy, OrderType.Limit, 1m, 50000m,
            TimeInForce.Gtc, null, submittedAt,
            InstrumentId: btc.InstrumentId,
            TradingAccountId: BuyerTradingAccountId));

        store.Apply(new CreateOrderCommand(
            sellOrderId, SellerAccountId, "BTC-USD",
            OrderSide.Sell, OrderType.Limit, 1m, 50000m,
            TimeInForce.Gtc, null, submittedAt,
            InstrumentId: btc.InstrumentId,
            TradingAccountId: SellerTradingAccountId));

        store.Apply(new TradeExecuted(
            "trade-crypto-enriched", buyOrderId, sellOrderId,
            BuyerAccountId, SellerAccountId,
            "BTC-USD", 50000m, 1m, submittedAt.AddSeconds(1),
            InstrumentId: btc.InstrumentId,
            BuyTradingAccountId: BuyerTradingAccountId,
            SellTradingAccountId: SellerTradingAccountId));

        var trades = store.GetEnrichedTrades("BTC-USD", 10);
        var btcTrade = trades.First(t => t.TradeId == "trade-crypto-enriched");

        Assert.Equal(btc.InstrumentId, btcTrade.InstrumentId);
        Assert.Equal(BuyerTradingAccountId, btcTrade.BuyTradingAccountId);
        Assert.Equal(SellerTradingAccountId, btcTrade.SellTradingAccountId);
    }

    private sealed class AlwaysAcceptClient : IMatchingEngineClient
    {
        public Task<OrderSubmissionResult> SubmitAsync(Order order, CancellationToken cancellationToken) =>
            Task.FromResult(new OrderSubmissionResult(
                true, DomainOrderStatus.Pending, Array.Empty<Trade>(), null, null));

        public Task<OrderCancellationResult> CancelAsync(Guid orderId, string symbol, CancellationToken cancellationToken) =>
            Task.FromResult(new OrderCancellationResult(true, null));
    }
}
