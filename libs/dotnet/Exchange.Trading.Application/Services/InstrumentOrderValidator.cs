using Exchange.Platform.Contracts;
using Exchange.Platform.Contracts.Commands;
using Exchange.Trading.Application.Abstractions;
using Exchange.Trading.Application.Models;

namespace Exchange.Trading.Application.Services;

public sealed class InstrumentOrderValidator(IInstrumentCatalog instrumentCatalog)
{
    private readonly IInstrumentCatalog _instrumentCatalog = instrumentCatalog;

    public InstrumentOrderValidationResult Validate(CreateOrderCommand command, InstrumentDefinition? resolvedInstrument)
    {
        _ = _instrumentCatalog;
        if (resolvedInstrument is null)
        {
            return Invalid("Instrument was not found.", MarketSession.Closed, null);
        }

        var session = ResolveSession(command.SubmittedAt, resolvedInstrument.MarketConfig);
        var status = resolvedInstrument.Status.Status;
        var tradingRule = resolvedInstrument.TradingRule;

        if (command.Quantity <= 0)
        {
            return Invalid("Quantity must be greater than zero.", session, resolvedInstrument);
        }

        if (tradingRule.Profile == InstrumentRuleProfile.Disabled || !tradingRule.MatchingEnabled)
        {
            return Invalid("Instrument book mode is disabled.", session, resolvedInstrument);
        }

        if (status is TradingStatus.Halted or TradingStatus.Suspended or TradingStatus.Disabled or TradingStatus.Expired)
        {
            return Invalid($"Instrument is {status}.", session, resolvedInstrument);
        }

        if (status == TradingStatus.Auction)
        {
            return Invalid("AUCTION placeholder mode is not yet supported for order entry.", session, resolvedInstrument);
        }

        if (session == MarketSession.Closed)
        {
            return Invalid("Instrument is currently outside of an allowed trading session.", session, resolvedInstrument);
        }

        if (status == TradingStatus.AfterMarketOnly && session != MarketSession.AfterMarket)
        {
            return Invalid("Instrument accepts orders only during AFTER_MARKET.", session, resolvedInstrument);
        }

        if (!tradingRule.AllowedSessions.Contains(session))
        {
            return Invalid($"Session {session} is not allowed for instrument '{resolvedInstrument.Instrument.Symbol}'.", session, resolvedInstrument);
        }

        if (!tradingRule.AllowedOrderTypes.Contains(command.Type))
        {
            return Invalid($"Order type {command.Type} is not allowed for instrument '{resolvedInstrument.Instrument.Symbol}'.", session, resolvedInstrument);
        }

        if (!HasPrecision(command.Quantity, tradingRule.QuantityPrecision))
        {
            return Invalid("Quantity precision is invalid for the instrument.", session, resolvedInstrument);
        }

        if (command.Quantity < tradingRule.MinQuantity)
        {
            return Invalid("Quantity is below the instrument minimum.", session, resolvedInstrument);
        }

        if (tradingRule.MaxQuantity.HasValue && command.Quantity > tradingRule.MaxQuantity.Value)
        {
            return Invalid("Quantity is above the instrument maximum.", session, resolvedInstrument);
        }

        if (!IsMultipleOf(command.Quantity, tradingRule.LotSize, tradingRule.QuantityPrecision))
        {
            return Invalid("Quantity does not respect the configured lot size.", session, resolvedInstrument);
        }

        if (command.Type == OrderType.Limit)
        {
            if (!command.Price.HasValue)
            {
                return Invalid("Limit orders require a price.", session, resolvedInstrument);
            }

            if (!HasPrecision(command.Price.Value, tradingRule.PricePrecision))
            {
                return Invalid("Price precision is invalid for the instrument.", session, resolvedInstrument);
            }

            if (!IsMultipleOf(command.Price.Value, tradingRule.TickSize, tradingRule.PricePrecision))
            {
                return Invalid("Price does not respect the configured tick size.", session, resolvedInstrument);
            }
        }

        return new InstrumentOrderValidationResult(
            true,
            null,
            session,
            resolvedInstrument,
            BuildExecutionInstructions(command, resolvedInstrument, session));
    }

    private static IReadOnlyDictionary<string, string> BuildExecutionInstructions(CreateOrderCommand command, InstrumentDefinition resolvedInstrument, MarketSession session)
    {
        var instructions = command.ExecutionInstructions is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(command.ExecutionInstructions, StringComparer.OrdinalIgnoreCase);

        instructions["bookProfile"] = resolvedInstrument.TradingRule.Profile.ToString();
        instructions["session"] = session.ToString();
        instructions["matchingEnabled"] = resolvedInstrument.TradingRule.MatchingEnabled ? "true" : "false";
        instructions["separateBook"] = resolvedInstrument.MarketConfig.SeparateBook ? "true" : "false";
        instructions["assetClass"] = resolvedInstrument.Instrument.AssetClass.ToString();

        return instructions;
    }

    private static MarketSession ResolveSession(DateTimeOffset submittedAt, InstrumentMarketConfig config)
    {
        var time = TimeOnly.FromDateTime(submittedAt.UtcDateTime);

        if (IsWithin(time, config.AuctionSessionStart, config.AuctionSessionEnd))
        {
            return MarketSession.Auction;
        }

        if (IsWithin(time, config.RegularSessionStart, config.RegularSessionEnd))
        {
            return MarketSession.Regular;
        }

        if (IsWithin(time, config.AfterMarketSessionStart, config.AfterMarketSessionEnd))
        {
            return MarketSession.AfterMarket;
        }

        return MarketSession.Closed;
    }

    private static bool IsWithin(TimeOnly time, TimeOnly? start, TimeOnly? end)
    {
        if (!start.HasValue || !end.HasValue)
        {
            return false;
        }

        return time >= start.Value && time < end.Value;
    }

    private static bool HasPrecision(decimal value, int precision) =>
        decimal.Round(value, precision, MidpointRounding.AwayFromZero) == value;

    private static bool IsMultipleOf(decimal value, decimal step, int precision)
    {
        if (step <= 0)
        {
            return true;
        }

        var remainder = decimal.Round(value % step, Math.Min(Math.Max(precision + 2, 4), 12), MidpointRounding.AwayFromZero);
        return remainder == 0m;
    }

    private static InstrumentOrderValidationResult Invalid(string reason, MarketSession session, InstrumentDefinition? resolvedInstrument) =>
        new(false, reason, session, resolvedInstrument, new Dictionary<string, string>());
}
