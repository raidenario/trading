using Exchange.Platform.Contracts;

namespace Exchange.Trading.Application.Models;

public sealed record InstrumentOrderValidationResult(
    bool IsValid,
    string? Reason,
    MarketSession Session,
    InstrumentDefinition? ResolvedInstrument,
    IReadOnlyDictionary<string, string> EnrichedExecutionInstructions);
