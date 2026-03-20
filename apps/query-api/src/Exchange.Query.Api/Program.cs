using Exchange.Platform.Contracts;
using Exchange.Platform.Contracts.ReadModels;

namespace Exchange.Query.Api;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
                policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
        });

        var app = builder.Build();
        app.UseCors();

        app.MapGet("/health", () => Results.Ok(new
        {
            service = "query-api",
            status = "ok",
            utcNow = DateTimeOffset.UtcNow
        }));

        app.MapGet("/api/history/orders", (Guid? accountId) =>
        {
            var effectiveAccountId = accountId ?? Guid.Parse("11111111-1111-1111-1111-111111111111");
            var history = new[]
            {
                new OrderHistoryItem(
                    Guid.Parse("20000000-0000-0000-0000-000000000001"),
                    effectiveAccountId,
                    "BTC-USD",
                    OrderSide.Buy,
                    OrderType.Limit,
                    OrderStatus.Filled,
                    0.5m, 0.5m, 49980m,
                    DateTimeOffset.UtcNow.AddMinutes(-15),
                    DateTimeOffset.UtcNow.AddMinutes(-14)),
                new OrderHistoryItem(
                    Guid.Parse("20000000-0000-0000-0000-000000000002"),
                    effectiveAccountId,
                    "ETH-USD",
                    OrderSide.Sell,
                    OrderType.Limit,
                    OrderStatus.PartiallyFilled,
                    1.5m, 0.7m, 3500m,
                    DateTimeOffset.UtcNow.AddMinutes(-8),
                    DateTimeOffset.UtcNow.AddMinutes(-3))
            };

            return Results.Ok(history);
        });

        app.MapGet("/api/balances/{accountId:guid}", (Guid accountId) =>
        {
            var balances = new[]
            {
                new BalanceSnapshot(accountId, "USD", 15000m, 1500m, DateTimeOffset.UtcNow),
                new BalanceSnapshot(accountId, "BTC", 0.85m, 0.10m, DateTimeOffset.UtcNow)
            };

            return Results.Ok(balances);
        });

        app.MapGet("/api/markets/{symbol}/ticker", (string symbol) =>
        {
            var ticker = new TickerSnapshot(
                symbol.ToUpperInvariant(),
                50010m, 50000m, 50020m, 50700m, 48900m,
                235.42m, 1.85m,
                DateTimeOffset.UtcNow);

            var candle = new CandleSnapshot(
                symbol.ToUpperInvariant(),
                "1m",
                49990m, 50030m, 49970m, 50010m, 12.54m,
                DateTimeOffset.UtcNow.AddMinutes(-1),
                DateTimeOffset.UtcNow);

            return Results.Ok(new { ticker, candle });
        });

        app.MapGet("/api/trades/recent", (string? symbol, int? limit) =>
        {
            var effectiveSymbol = symbol?.ToUpperInvariant() ?? "BTC-USD";
            var effectiveLimit = Math.Clamp(limit ?? 20, 1, 100);
            var trades = Enumerable.Range(1, effectiveLimit).Select(i => new RecentTradeView(
                $"trade-{i:D10}",
                effectiveSymbol,
                50000m + (i % 2 == 0 ? 10m : -10m),
                0.1m * i,
                i % 2 == 0 ? "Buy" : "Sell",
                DateTimeOffset.UtcNow.AddSeconds(-i * 3)
            )).ToList();

            return Results.Ok(trades);
        });

        app.MapGet("/api/markets/overview", () =>
        {
            var overview = new[]
            {
                new MarketOverviewItem("BTC-USD", 50010m, 110m, 0.22m, 1250.5m, 50700m, 48900m, DateTimeOffset.UtcNow),
                new MarketOverviewItem("ETH-USD", 3510m, 35m, 1.01m, 8500.2m, 3580m, 3420m, DateTimeOffset.UtcNow),
                new MarketOverviewItem("SOL-USD", 125.50m, -2.30m, -1.80m, 45000m, 130m, 120m, DateTimeOffset.UtcNow)
            };

            return Results.Ok(overview);
        });

        app.Run();
    }
}
