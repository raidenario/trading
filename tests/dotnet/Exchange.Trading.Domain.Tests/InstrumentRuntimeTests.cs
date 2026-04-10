using Exchange.Platform.Contracts;
using Exchange.Platform.Contracts.Commands;
using Exchange.Trading.Application.Models;
using Exchange.Trading.Application.Services;

namespace Exchange.Trading.Domain.Tests;

public sealed class InstrumentRuntimeTests
{
    [Fact]
    public async Task Catalog_resolves_by_symbol_and_instrument_id()
    {
        var catalog = new StaticInstrumentCatalog(
            DemoSeed.Instruments,
            DemoSeed.InstrumentTradingRules,
            DemoSeed.InstrumentMarketConfigs,
            DemoSeed.InstrumentStatuses);

        var bySymbol = await catalog.ResolveAsync("PETR4", null, CancellationToken.None);
        var byId = await catalog.ResolveAsync("IGNORED", DemoSeed.Instruments.First(x => x.Symbol == "PETR4").InstrumentId, CancellationToken.None);

        Assert.NotNull(bySymbol);
        Assert.NotNull(byId);
        Assert.Equal(bySymbol!.Instrument.InstrumentId, byId!.Instrument.InstrumentId);
        Assert.Equal(InstrumentRuleProfile.SpotStandard, bySymbol.TradingRule.Profile);
    }

    [Fact]
    public void Validator_rejects_disabled_status_and_invalid_tick_size()
    {
        var catalog = new StaticInstrumentCatalog(
            DemoSeed.Instruments,
            DemoSeed.InstrumentTradingRules,
            DemoSeed.InstrumentMarketConfigs,
            DemoSeed.InstrumentStatuses);
        var validator = new InstrumentOrderValidator(catalog);

        var disabled = DemoSeed.Instruments.First(x => x.Symbol == "GOLD-SPOT");
        var disabledCommand = new CreateOrderCommand(
            Guid.NewGuid(),
            DemoSeed.Accounts.First().AccountId,
            disabled.Symbol,
            OrderSide.Buy,
            OrderType.Limit,
            1m,
            245.37m,
            TimeInForce.Gtc,
            null,
            DateTimeOffset.Parse("2026-04-09T15:00:00Z"));

        var disabledResult = validator.Validate(disabledCommand, catalog.ResolveAsync(disabled.Symbol, null, CancellationToken.None).Result!);

        Assert.False(disabledResult.IsValid);
        Assert.Contains("disabled", disabledResult.Reason!, StringComparison.OrdinalIgnoreCase);

        var invalidTick = new CreateOrderCommand(
            Guid.NewGuid(),
            DemoSeed.Accounts.First().AccountId,
            "PETR4",
            OrderSide.Buy,
            OrderType.Limit,
            100m,
            37.019m,
            TimeInForce.Gtc,
            null,
            DateTimeOffset.Parse("2026-04-09T15:00:00Z"));

        var invalidTickResult = validator.Validate(invalidTick, catalog.ResolveAsync("PETR4", null, CancellationToken.None).Result!);

        Assert.False(invalidTickResult.IsValid);
        Assert.Contains("tick", invalidTickResult.Reason!, StringComparison.OrdinalIgnoreCase);

        var halted = new CreateOrderCommand(
            Guid.NewGuid(),
            DemoSeed.Accounts.First().AccountId,
            "SMAL11",
            OrderSide.Buy,
            OrderType.Limit,
            10m,
            102.20m,
            TimeInForce.Gtc,
            null,
            DateTimeOffset.Parse("2026-04-09T15:00:00Z"));

        var haltedResult = validator.Validate(halted, catalog.ResolveAsync("SMAL11", null, CancellationToken.None).Result!);
        Assert.False(haltedResult.IsValid);
        Assert.Contains("halted", haltedResult.Reason!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validator_applies_after_market_only_and_fractional_rules()
    {
        var catalog = new StaticInstrumentCatalog(
            DemoSeed.Instruments,
            DemoSeed.InstrumentTradingRules,
            DemoSeed.InstrumentMarketConfigs,
            DemoSeed.InstrumentStatuses);
        var validator = new InstrumentOrderValidator(catalog);

        var afterMarketRejected = new CreateOrderCommand(
            Guid.NewGuid(),
            DemoSeed.Accounts.First().AccountId,
            "AAPL34",
            OrderSide.Buy,
            OrderType.Limit,
            1m,
            52.10m,
            TimeInForce.Gtc,
            null,
            DateTimeOffset.Parse("2026-04-09T15:00:00Z"));

        var rejectedResult = validator.Validate(afterMarketRejected, catalog.ResolveAsync("AAPL34", null, CancellationToken.None).Result!);
        Assert.False(rejectedResult.IsValid);
        Assert.Equal(MarketSession.Regular, rejectedResult.Session);

        var afterMarketAccepted = afterMarketRejected with
        {
            SubmittedAt = DateTimeOffset.Parse("2026-04-09T21:10:00Z")
        };

        var acceptedResult = validator.Validate(afterMarketAccepted, catalog.ResolveAsync("AAPL34", null, CancellationToken.None).Result!);
        Assert.True(acceptedResult.IsValid);
        Assert.Equal(MarketSession.AfterMarket, acceptedResult.Session);

        var fractionalInvalid = new CreateOrderCommand(
            Guid.NewGuid(),
            DemoSeed.Accounts.First().AccountId,
            "PETR4F",
            OrderSide.Buy,
            OrderType.Market,
            0.5m,
            null,
            TimeInForce.Ioc,
            null,
            DateTimeOffset.Parse("2026-04-09T15:00:00Z"));

        var fractionalInvalidResult = validator.Validate(fractionalInvalid, catalog.ResolveAsync("PETR4F", null, CancellationToken.None).Result!);
        Assert.False(fractionalInvalidResult.IsValid);
        Assert.Contains("lot", fractionalInvalidResult.Reason!, StringComparison.OrdinalIgnoreCase);

        var fractionalValid = fractionalInvalid with { Quantity = 1m };
        var fractionalValidResult = validator.Validate(fractionalValid, catalog.ResolveAsync("PETR4F", null, CancellationToken.None).Result!);
        Assert.True(fractionalValidResult.IsValid);
        Assert.Equal(InstrumentRuleProfile.SpotFractional, fractionalValidResult.ResolvedInstrument!.TradingRule.Profile);

        var auction = new CreateOrderCommand(
            Guid.NewGuid(),
            DemoSeed.Accounts.First().AccountId,
            "GOGL34",
            OrderSide.Buy,
            OrderType.Limit,
            1m,
            48.90m,
            TimeInForce.Gtc,
            null,
            DateTimeOffset.Parse("2026-04-09T12:50:00Z"));

        var auctionResult = validator.Validate(auction, catalog.ResolveAsync("GOGL34", null, CancellationToken.None).Result!);
        Assert.False(auctionResult.IsValid);
        Assert.Contains("auction", auctionResult.Reason!, StringComparison.OrdinalIgnoreCase);
    }
}
