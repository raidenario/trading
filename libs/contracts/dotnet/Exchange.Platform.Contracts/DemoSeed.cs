namespace Exchange.Platform.Contracts;

public sealed record DemoAccountSeed(Guid AccountId, string DisplayName, string Email);

public sealed record DemoBalanceSeed(Guid AccountId, string Asset, decimal Available, decimal Reserved);

public static class DemoSeed
{
    public static readonly IReadOnlyCollection<DemoAccountSeed> Accounts =
    [
        new(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Alice Trader", "alice@exchange.local"),
        new(Guid.Parse("22222222-2222-2222-2222-222222222222"), "Bob Market", "bob@exchange.local"),
        new(Guid.Parse("33333333-3333-3333-3333-333333333333"), "Charlie Whale", "charlie@exchange.local")
    ];

    public static readonly IReadOnlyCollection<DemoBalanceSeed> Balances =
    [
        new(Guid.Parse("11111111-1111-1111-1111-111111111111"), "USD", 100_000m, 0m),
        new(Guid.Parse("11111111-1111-1111-1111-111111111111"), "BTC", 5m, 0m),
        new(Guid.Parse("11111111-1111-1111-1111-111111111111"), "ETH", 50m, 0m),
        new(Guid.Parse("22222222-2222-2222-2222-222222222222"), "USD", 250_000m, 0m),
        new(Guid.Parse("22222222-2222-2222-2222-222222222222"), "BTC", 10m, 0m),
        new(Guid.Parse("22222222-2222-2222-2222-222222222222"), "SOL", 500m, 0m),
        new(Guid.Parse("33333333-3333-3333-3333-333333333333"), "USD", 1_000_000m, 0m),
        new(Guid.Parse("33333333-3333-3333-3333-333333333333"), "BTC", 50m, 0m),
        new(Guid.Parse("33333333-3333-3333-3333-333333333333"), "ETH", 200m, 0m),
        new(Guid.Parse("33333333-3333-3333-3333-333333333333"), "SOL", 2_000m, 0m)
    ];
}
