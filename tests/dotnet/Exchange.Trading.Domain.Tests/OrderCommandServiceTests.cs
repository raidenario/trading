using Exchange.Platform.Contracts;
using Exchange.Platform.Contracts.Commands;
using Exchange.Trading.Application.Abstractions;
using Exchange.Trading.Application.Models;
using Exchange.Trading.Application.Services;
using Exchange.Trading.Domain.Entities;
using Exchange.Trading.Infrastructure.Repositories;
using DomainOrderStatus = Exchange.Trading.Domain.Enums.OrderStatus;

namespace Exchange.Trading.Domain.Tests;

public sealed class OrderCommandServiceTests
{
    [Fact]
    public async Task CreateAsync_resolves_instrument_and_default_trading_account_before_submission()
    {
        var repository = new InMemoryOrderRepository();
        var matchingClient = new CapturingMatchingEngineClient();
        var instrumentCatalog = new StaticInstrumentCatalog(DemoSeed.Instruments);
        var tradingAccountResolver = new DemoTradingAccountResolver(DemoSeed.TradingAccounts);
        var service = new OrderCommandService(repository, matchingClient, instrumentCatalog, tradingAccountResolver);

        var command = new CreateOrderCommand(
            Guid.NewGuid(),
            DemoSeed.Accounts.First().AccountId,
            "BTC-USD",
            OrderSide.Buy,
            OrderType.Limit,
            0.5m,
            50000m,
            TimeInForce.Gtc,
            "client-1",
            DateTimeOffset.UtcNow);

        await service.CreateAsync(command, CancellationToken.None);

        Assert.NotNull(matchingClient.SubmittedOrder);
        Assert.Equal(DemoSeed.Instruments.First(x => x.Symbol == "BTC-USD").InstrumentId, matchingClient.SubmittedOrder!.InstrumentId);
        Assert.Equal(DemoSeed.TradingAccounts.First(x => x.AccountId == command.AccountId).TradingAccountId, matchingClient.SubmittedOrder.TradingAccountId);
        Assert.Equal("BTC-USD", matchingClient.SubmittedOrder.Symbol.Value);
    }

    [Fact]
    public async Task CreateAsync_persists_enriched_order_while_accepting_legacy_request_shape()
    {
        var repository = new InMemoryOrderRepository();
        var service = new OrderCommandService(
            repository,
            new CapturingMatchingEngineClient(),
            new StaticInstrumentCatalog(DemoSeed.Instruments),
            new DemoTradingAccountResolver(DemoSeed.TradingAccounts));

        var command = new CreateOrderCommand(
            Guid.NewGuid(),
            DemoSeed.Accounts.First().AccountId,
            "ETH-USD",
            OrderSide.Sell,
            OrderType.Limit,
            1.25m,
            3000m,
            TimeInForce.Gtc,
            null,
            DateTimeOffset.UtcNow);

        await service.CreateAsync(command, CancellationToken.None);
        var stored = await repository.GetByIdAsync(command.OrderId, CancellationToken.None);

        Assert.NotNull(stored);
        Assert.Equal(command.AccountId, stored!.AccountId);
        Assert.Equal("ETH-USD", stored.Symbol.Value);
        Assert.NotNull(stored.InstrumentId);
        Assert.NotNull(stored.TradingAccountId);
        Assert.Equal(OrderSource.Api, stored.SourceSystem);
    }

    [Fact]
    public async Task CreateAsync_rejects_invalid_instrument_rule_before_matching_submission()
    {
        var repository = new InMemoryOrderRepository();
        var matchingClient = new CapturingMatchingEngineClient();
        var service = new OrderCommandService(
            repository,
            matchingClient,
            new StaticInstrumentCatalog(
                DemoSeed.Instruments,
                DemoSeed.InstrumentTradingRules,
                DemoSeed.InstrumentMarketConfigs,
                DemoSeed.InstrumentStatuses),
            new DemoTradingAccountResolver(DemoSeed.TradingAccounts));

        var command = new CreateOrderCommand(
            Guid.NewGuid(),
            DemoSeed.Accounts.First().AccountId,
            "PETR4",
            OrderSide.Buy,
            OrderType.Limit,
            150m,
            37.10m,
            TimeInForce.Gtc,
            null,
            DateTimeOffset.Parse("2026-04-09T15:00:00Z"));

        var result = await service.CreateAsync(command, CancellationToken.None);

        Assert.Equal(DomainOrderStatus.Rejected, result.Status);
        Assert.Contains("lot", result.RejectionReason!, StringComparison.OrdinalIgnoreCase);
        Assert.Null(matchingClient.SubmittedOrder);
    }

    [Fact]
    public async Task CreateAsync_enriches_execution_instructions_from_instrument_runtime_definition()
    {
        var repository = new InMemoryOrderRepository();
        var matchingClient = new CapturingMatchingEngineClient();
        var service = new OrderCommandService(
            repository,
            matchingClient,
            new StaticInstrumentCatalog(
                DemoSeed.Instruments,
                DemoSeed.InstrumentTradingRules,
                DemoSeed.InstrumentMarketConfigs,
                DemoSeed.InstrumentStatuses),
            new DemoTradingAccountResolver(DemoSeed.TradingAccounts));

        var command = new CreateOrderCommand(
            Guid.NewGuid(),
            DemoSeed.Accounts.First().AccountId,
            "PETR4F",
            OrderSide.Buy,
            OrderType.Market,
            1m,
            null,
            TimeInForce.Ioc,
            null,
            DateTimeOffset.Parse("2026-04-09T15:00:00Z"),
            ExecutionInstructions: new Dictionary<string, string> { ["sourceHint"] = "test" });

        await service.CreateAsync(command, CancellationToken.None);

        Assert.NotNull(matchingClient.SubmittedOrder);
        Assert.NotNull(matchingClient.SubmittedOrder!.ExecutionInstructions);
        Assert.Equal("test", matchingClient.SubmittedOrder.ExecutionInstructions!["sourceHint"]);
        Assert.Equal("SpotFractional", matchingClient.SubmittedOrder.ExecutionInstructions["bookProfile"]);
        Assert.Equal("Regular", matchingClient.SubmittedOrder.ExecutionInstructions["session"]);
        Assert.Equal("true", matchingClient.SubmittedOrder.ExecutionInstructions["matchingEnabled"]);
        Assert.Equal("true", matchingClient.SubmittedOrder.ExecutionInstructions["separateBook"]);
        Assert.Equal("Equity", matchingClient.SubmittedOrder.ExecutionInstructions["assetClass"]);
    }

    private sealed class CapturingMatchingEngineClient : IMatchingEngineClient
    {
        public Order? SubmittedOrder { get; private set; }

        public Task<OrderSubmissionResult> SubmitAsync(Order order, CancellationToken cancellationToken)
        {
            SubmittedOrder = order;
            return Task.FromResult(new OrderSubmissionResult(true, DomainOrderStatus.Pending, Array.Empty<Trade>(), null, null));
        }

        public Task<OrderCancellationResult> CancelAsync(Guid orderId, string symbol, CancellationToken cancellationToken) =>
            Task.FromResult(new OrderCancellationResult(true, null));
    }
}
