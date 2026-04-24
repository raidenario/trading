using Exchange.Platform.Contracts;

namespace Exchange.Trading.Application.Abstractions;

public interface ITradingAccountProvisioner
{
    Task<TradingAccount> EnsureForAccountAsync(Guid accountId, CancellationToken cancellationToken);
}
