using System.Text.Json.Serialization;

namespace Exchange.Query.Api;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });
        builder.Services.AddSingleton<QueryProjectionStore>();
        builder.Services.AddSingleton<PostgresProjectionWriter>();
        builder.Services.AddHttpClient<IRealtimeEventForwarder, RealtimeEventForwarder>(client =>
        {
            client.BaseAddress = new Uri(builder.Configuration["RealtimeGateway:BaseUrl"] ?? "http://localhost:4000");
            client.Timeout = TimeSpan.FromSeconds(2);
        });
        builder.Services.AddHostedService<KafkaProjectionConsumer>();
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

        app.MapGet("/api/history/orders", (Guid? accountId, QueryProjectionStore store) =>
            Results.Ok(store.GetOrderHistory(accountId)));

        app.MapGet("/api/orders/enriched", (Guid? accountId, QueryProjectionStore store) =>
            Results.Ok(store.GetEnrichedOrders(accountId)));

        app.MapGet("/api/balances/{accountId:guid}", (Guid accountId, QueryProjectionStore store) =>
            Results.Ok(store.GetBalances(accountId)));

        app.MapGet("/api/instruments", (QueryProjectionStore store) =>
            Results.Ok(store.GetInstruments()));

        app.MapGet("/api/positions", (Guid? tradingAccountId, QueryProjectionStore store) =>
            Results.Ok(store.GetPositions(tradingAccountId)));

        app.MapGet("/api/markets/{symbol}/ticker", (string symbol, QueryProjectionStore store) =>
            Results.Ok(store.GetTickerWithCandle(symbol)));

        app.MapGet("/api/markets/{symbol}/candles", (string symbol, string? interval, int? limit, QueryProjectionStore store) =>
            Results.Ok(store.GetCandles(symbol, interval, limit)));

        app.MapGet("/api/markets/{symbol}/book", (string symbol, QueryProjectionStore store) =>
            Results.Ok(store.GetOrderBook(symbol)));

        app.MapGet("/api/trades/recent", (string? symbol, int? limit, QueryProjectionStore store) =>
            Results.Ok(store.GetRecentTrades(symbol, limit)));

        app.MapGet("/api/trades/enriched", (string? symbol, int? limit, QueryProjectionStore store) =>
            Results.Ok(store.GetEnrichedTrades(symbol, limit)));

        app.MapGet("/api/markets/overview", (QueryProjectionStore store) =>
            Results.Ok(store.GetMarketOverview()));

        app.Run();
    }
}
