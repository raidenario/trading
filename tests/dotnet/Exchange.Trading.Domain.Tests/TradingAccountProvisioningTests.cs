using Exchange.Platform.Contracts;
using Exchange.Platform.Contracts.Commands;
using Exchange.Trading.Application.Abstractions;
using Exchange.Trading.Application.Models;
using Exchange.Trading.Application.Services;
using Exchange.Trading.Domain.Entities;
using Exchange.Trading.Infrastructure.Messaging;
using Exchange.Trading.Infrastructure.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using DomainOrderStatus = Exchange.Trading.Domain.Enums.OrderStatus;

namespace Exchange.Trading.Domain.Tests;

public sealed class TradingAccountProvisioningTests
{
    [Fact]
    public async Task CreateAccountAsync_provisions_default_trading_account_that_can_be_resolved_for_order_submission()
    {
        var resolver = new DemoTradingAccountResolver(DemoSeed.TradingAccounts);
        var accountService = new InMemoryAccountService(
            new InMemoryIntegrationEventPublisher(NullLogger<InMemoryIntegrationEventPublisher>.Instance),
            resolver);
        var repository = new InMemoryOrderRepository();
        var matchingClient = new CapturingMatchingEngineClient();
        var orderService = new OrderCommandService(
            repository,
            matchingClient,
            new StaticInstrumentCatalog(DemoSeed.Instruments),
            resolver);

        var accountId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var createResult = await accountService.CreateAsync(
            new CreateAccountCommand(accountId, "Delta Runtime", "delta@exchange.local", DateTimeOffset.UtcNow),
            CancellationToken.None);

        Assert.True(createResult.Success);

        var tradingAccount = await resolver.ResolveByAccountIdAsync(accountId, CancellationToken.None);

        Assert.NotNull(tradingAccount);
        Assert.Equal(accountId, tradingAccount!.AccountId);
        Assert.Equal(DemoSeed.Participants.Single().ParticipantId, tradingAccount.ParticipantId);

        var orderId = Guid.NewGuid();
        var submitResult = await orderService.CreateAsync(
            new CreateOrderCommand(
                orderId,
                accountId,
                "BTC-USD",
                OrderSide.Buy,
                OrderType.Limit,
                0.5m,
                50000m,
                TimeInForce.Gtc,
                "runtime-account-order",
                DateTimeOffset.UtcNow),
            CancellationToken.None);

        var stored = await repository.GetByIdAsync(orderId, CancellationToken.None);

        Assert.Equal(DomainOrderStatus.Pending, submitResult.Status);
        Assert.NotNull(stored);
        Assert.Equal(tradingAccount.TradingAccountId, stored!.TradingAccountId);
        Assert.Equal(tradingAccount.TradingAccountId, matchingClient.SubmittedOrder!.TradingAccountId);
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
