using Exchange.Platform.Contracts;
using Exchange.Platform.Contracts.Commands;
using Exchange.Trading.Application.Abstractions;
using Exchange.Trading.Application.Models;

namespace Exchange.Trading.Application.Services;

public sealed class InstrumentOrderValidator(IInstrumentCatalog instrumentCatalog)
{
    private readonly IInstrumentCatalog _instrumentCatalog = instrumentCatalog;

    /// <summary>
    /// Validates a <see cref="CreateOrderCommand"/> against the resolved instrument's
    /// trading rules, session windows, and status. Uses a declarative rule pipeline
    /// so that new rules can be added or reordered without nested control-flow.
    /// </summary>
    public InstrumentOrderValidationResult Validate(CreateOrderCommand command, InstrumentDefinition? resolvedInstrument)
    {
        _ = _instrumentCatalog;

        if (resolvedInstrument is null)
        {
            return Invalid("Instrument was not found.", MarketSession.Closed, null);
        }

        var session = ResolveSession(command.SubmittedAt, resolvedInstrument.MarketConfig);
        var status = resolvedInstrument.Status.Status;
        var rule = resolvedInstrument.TradingRule;
        var symbol = resolvedInstrument.Instrument.Symbol;

        // ── Declarative validation pipeline ──────────────────────────────
        // Each rule returns an error message or null when valid.
        // The pipeline short-circuits on the first failure.
        var rules = BuildValidationRules(command, session, status, rule, symbol);

        foreach (var validationRule in rules)
        {
            var error = validationRule();
            if (error is not null)
            {
                return Invalid(error, session, resolvedInstrument);
            }
        }

        return new InstrumentOrderValidationResult(
            true,
            null,
            session,
            resolvedInstrument,
            BuildExecutionInstructions(command, resolvedInstrument, session));
    }

    // ─── Rule definitions ────────────────────────────────────────────────
    // Each Func<string?> encapsulates a single validation concern.
    // Returning null means the rule passed; any string is a rejection reason.

    private static IReadOnlyList<Func<string?>> BuildValidationRules(
        CreateOrderCommand command,
        MarketSession session,
        TradingStatus status,
        InstrumentTradingRule rule,
        string symbol)
    {
        var rules = new List<Func<string?>>
        {
            // ── Quantity guard ───────────────────────────────────────
            () => command.Quantity <= 0
                ? "Quantity must be greater than zero."
                : null,

            // ── Book / matching gate ─────────────────────────────────
            () => rule.Profile == InstrumentRuleProfile.Disabled || !rule.MatchingEnabled
                ? "Instrument book mode is disabled."
                : null,

            // ── Instrument status gates ──────────────────────────────
            () => status is TradingStatus.Halted or TradingStatus.Suspended
                        or TradingStatus.Disabled or TradingStatus.Expired
                ? $"Instrument is {status}."
                : null,

            () => status == TradingStatus.Auction
                ? "AUCTION placeholder mode is not yet supported for order entry."
                : null,

            // ── Session gates ────────────────────────────────────────
            () => session == MarketSession.Closed
                ? "Instrument is currently outside of an allowed trading session."
                : null,

            () => status == TradingStatus.AfterMarketOnly && session != MarketSession.AfterMarket
                ? "Instrument accepts orders only during AFTER_MARKET."
                : null,

            () => !rule.AllowedSessions.Contains(session)
                ? $"Session {session} is not allowed for instrument '{symbol}'."
                : null,

            // ── Order-type gate ──────────────────────────────────────
            () => !rule.AllowedOrderTypes.Contains(command.Type)
                ? $"Order type {command.Type} is not allowed for instrument '{symbol}'."
                : null,

            // ── Quantity constraints ─────────────────────────────────
            () => !IsMultipleOf(command.Quantity, rule.LotSize, rule.QuantityPrecision)
                ? "Quantity does not respect the configured lot size."
                : null,

            () => command.Quantity < rule.MinQuantity
                ? "Quantity is below the instrument minimum."
                : null,

            () => rule.MaxQuantity.HasValue && command.Quantity > rule.MaxQuantity.Value
                ? "Quantity is above the instrument maximum."
                : null,

            () => !HasPrecision(command.Quantity, rule.QuantityPrecision)
                ? "Quantity precision is invalid for the instrument."
                : null,
        };

        // ── Limit-order price rules (only apply when Type == Limit) ──
        if (command.Type == OrderType.Limit)
        {
            rules.AddRange([
                () => !command.Price.HasValue
                    ? "Limit orders require a price."
                    : null,

                () => command.Price.HasValue
                      && !IsMultipleOf(command.Price.Value, rule.TickSize, rule.PricePrecision)
                    ? "Price does not respect the configured tick size."
                    : null,

                () => command.Price.HasValue
                      && !HasPrecision(command.Price.Value, rule.PricePrecision)
                    ? "Price precision is invalid for the instrument."
                    : null,
            ]);
        }

        return rules;
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
