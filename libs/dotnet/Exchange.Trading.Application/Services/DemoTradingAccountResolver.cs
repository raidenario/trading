using Exchange.Platform.Contracts;
using Exchange.Trading.Application.Abstractions;

namespace Exchange.Trading.Application.Services;

public sealed class DemoTradingAccountResolver : ITradingAccountResolver
{
    private readonly Dictionary<Guid, TradingAccount> _accountsByAccountId;

    public DemoTradingAccountResolver(IEnumerable<TradingAccount> tradingAccounts)
    {
        _accountsByAccountId = tradingAccounts.ToDictionary(account => account.AccountId);
    }

    public Task<TradingAccount?> ResolveByAccountIdAsync(Guid accountId, CancellationToken cancellationToken)
    {
        _accountsByAccountId.TryGetValue(accountId, out var tradingAccount);
        return Task.FromResult(tradingAccount);
    }
}
