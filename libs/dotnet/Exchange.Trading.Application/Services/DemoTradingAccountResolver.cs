using Exchange.Platform.Contracts;
using Exchange.Trading.Application.Abstractions;
using System.Collections.Concurrent;

namespace Exchange.Trading.Application.Services;

public sealed class DemoTradingAccountResolver : ITradingAccountResolver, ITradingAccountProvisioner
{
    private readonly ConcurrentDictionary<Guid, TradingAccount> _accountsByAccountId;
    private readonly Guid _defaultParticipantId;

    public DemoTradingAccountResolver(IEnumerable<TradingAccount> tradingAccounts)
    {
        _accountsByAccountId = new ConcurrentDictionary<Guid, TradingAccount>(
            tradingAccounts.ToDictionary(account => account.AccountId));
        _defaultParticipantId = DemoSeed.Participants.Single().ParticipantId;
    }

    public Task<TradingAccount?> ResolveByAccountIdAsync(Guid accountId, CancellationToken cancellationToken)
    {
        _accountsByAccountId.TryGetValue(accountId, out var tradingAccount);
        return Task.FromResult(tradingAccount);
    }

    public Task<TradingAccount> EnsureForAccountAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var existing = _accountsByAccountId.GetOrAdd(accountId, CreateDefaultTradingAccount);
        return Task.FromResult(existing);
    }

    private TradingAccount CreateDefaultTradingAccount(Guid accountId)
    {
        var now = DateTimeOffset.UtcNow;
        return new TradingAccount(
            Guid.NewGuid(),
            accountId,
            _defaultParticipantId,
            $"AUTO-{accountId:N}"[..13],
            TradingAccountStatus.Active,
            now,
            now);
    }
}
