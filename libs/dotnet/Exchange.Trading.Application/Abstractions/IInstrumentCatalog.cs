using Exchange.Platform.Contracts;

namespace Exchange.Trading.Application.Abstractions;

public interface IInstrumentCatalog
{
    Task<Instrument?> GetBySymbolAsync(string symbol, CancellationToken cancellationToken);

    Task<InstrumentDefinition?> ResolveAsync(string symbol, Guid? instrumentId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<Instrument>> ListAsync(CancellationToken cancellationToken);
}
