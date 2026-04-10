using Exchange.Platform.Contracts;
using Exchange.Trading.Application.Abstractions;

namespace Exchange.Trading.Application.Services;

public sealed class StaticInstrumentCatalog : IInstrumentCatalog
{
    private readonly Dictionary<string, InstrumentDefinition> _bySymbol;
    private readonly Dictionary<Guid, InstrumentDefinition> _byInstrumentId;

    public StaticInstrumentCatalog(IEnumerable<Instrument> instruments)
        : this(instruments, Array.Empty<InstrumentTradingRule>(), Array.Empty<InstrumentMarketConfig>(), Array.Empty<InstrumentStatusRecord>())
    {
    }

    public StaticInstrumentCatalog(
        IEnumerable<Instrument> instruments,
        IEnumerable<InstrumentTradingRule> tradingRules,
        IEnumerable<InstrumentMarketConfig> marketConfigs,
        IEnumerable<InstrumentStatusRecord> statuses)
    {
        var rules = tradingRules.ToDictionary(item => item.InstrumentId);
        var configs = marketConfigs.ToDictionary(item => item.InstrumentId);
        var statusMap = statuses.ToDictionary(item => item.InstrumentId);

        _bySymbol = new Dictionary<string, InstrumentDefinition>(StringComparer.OrdinalIgnoreCase);
        _byInstrumentId = new Dictionary<Guid, InstrumentDefinition>();

        foreach (var instrument in instruments)
        {
            var definition = new InstrumentDefinition(
                instrument,
                rules.TryGetValue(instrument.InstrumentId, out var rule) ? rule : CreateDefaultRule(instrument),
                configs.TryGetValue(instrument.InstrumentId, out var config) ? config : CreateDefaultConfig(instrument),
                statusMap.TryGetValue(instrument.InstrumentId, out var status) ? status : CreateDefaultStatus(instrument));

            _bySymbol[instrument.Symbol] = definition;
            _byInstrumentId[instrument.InstrumentId] = definition;
        }
    }

    public Task<Instrument?> GetBySymbolAsync(string symbol, CancellationToken cancellationToken)
    {
        _bySymbol.TryGetValue(symbol, out var instrument);
        return Task.FromResult(instrument?.Instrument);
    }

    public Task<InstrumentDefinition?> ResolveAsync(string symbol, Guid? instrumentId, CancellationToken cancellationToken)
    {
        if (instrumentId.HasValue && _byInstrumentId.TryGetValue(instrumentId.Value, out var byId))
        {
            return Task.FromResult<InstrumentDefinition?>(byId);
        }

        _bySymbol.TryGetValue(symbol, out var bySymbol);
        return Task.FromResult<InstrumentDefinition?>(bySymbol);
    }

    public Task<IReadOnlyCollection<Instrument>> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyCollection<Instrument>>(_bySymbol.Values.Select(item => item.Instrument).OrderBy(item => item.Symbol).ToArray());

    private static InstrumentTradingRule CreateDefaultRule(Instrument instrument) =>
        new(
            instrument.InstrumentId,
            InstrumentRuleProfile.SpotStandard,
            instrument.LotSize,
            null,
            instrument.TickSize,
            instrument.LotSize,
            instrument.PricePrecision,
            instrument.QuantityPrecision,
            [OrderType.Limit, OrderType.Market],
            [MarketSession.Regular],
            instrument.TradingStatus == TradingStatus.Active);

    private static InstrumentMarketConfig CreateDefaultConfig(Instrument instrument) =>
        new(instrument.InstrumentId, new TimeOnly(0, 0), new TimeOnly(23, 59), null, null, null, null, false);

    private static InstrumentStatusRecord CreateDefaultStatus(Instrument instrument) =>
        new(instrument.InstrumentId, instrument.TradingStatus, DateTimeOffset.UtcNow);
}
