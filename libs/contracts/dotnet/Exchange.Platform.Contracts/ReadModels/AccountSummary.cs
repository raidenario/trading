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

public sealed record InstrumentSnapshot(
    Guid InstrumentId,
    string Symbol,
    AssetClass AssetClass,
    Segment Segment,
    Market Market,
    string BaseAsset,
    string QuoteAsset,
    TradingStatus TradingStatus,
    decimal TickSize,
    decimal LotSize);

public sealed record PositionSnapshot(
    Guid PositionId,
    Guid TradingAccountId,
    Guid InstrumentId,
    string Symbol,
    DateOnly PositionDate,
    decimal NetQuantity,
    decimal? AverageOpenPrice,
    decimal LongQuantity,
    decimal ShortQuantity,
    DateTimeOffset UpdatedAt);

public sealed record EnrichedOrderView(
    Guid OrderId,
    Guid AccountId,
    Guid TradingAccountId,
    Guid InstrumentId,
    string Symbol,
    OrderSide Side,
    OrderType Type,
    OrderStatus Status,
    decimal Quantity,
    decimal FilledQuantity,
    decimal OpenQuantity,
    decimal? Price,
    OrderSource SourceSystem,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record EnrichedTradeView(
    string TradeId,
    Guid InstrumentId,
    string Symbol,
    Guid BuyOrderId,
    Guid SellOrderId,
    Guid BuyTradingAccountId,
    Guid SellTradingAccountId,
    decimal Price,
    decimal Quantity,
    DateTimeOffset ExecutedAt);
