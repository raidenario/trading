namespace Exchange.Platform.Contracts.ReadModels;

public sealed record AccountSummary(
    Guid AccountId,
    string DisplayName,
    string Email,
    DateTimeOffset CreatedAt);

public sealed record AccountBalanceView(
    Guid AccountId,
    string Asset,
    decimal Available,
    decimal Reserved,
    decimal Total,
    DateTimeOffset AsOf);

public sealed record RecentTradeView(
    string TradeId,
    string Symbol,
    decimal Price,
    decimal Quantity,
    string Side,
    DateTimeOffset ExecutedAt);

public sealed record MarketOverviewItem(
    string Symbol,
    decimal LastPrice,
    decimal Change24h,
    decimal ChangePercent24h,
    decimal Volume24h,
    decimal High24h,
    decimal Low24h,
    DateTimeOffset AsOf);
