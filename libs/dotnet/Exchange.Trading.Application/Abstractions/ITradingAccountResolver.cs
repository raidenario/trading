using Exchange.Platform.Contracts;

namespace Exchange.Trading.Application.Abstractions;

public interface ITradingAccountResolver
{
    Task<TradingAccount?> ResolveByAccountIdAsync(Guid accountId, CancellationToken cancellationToken);
}
