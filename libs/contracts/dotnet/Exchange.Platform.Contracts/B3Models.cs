namespace Exchange.Platform.Contracts;

public sealed record Instrument(
    Guid InstrumentId,
    string Symbol,
    AssetClass AssetClass,
    Segment Segment,
    Market Market,
    string? Isin,
    string BaseAsset,
    string QuoteAsset,
    int PricePrecision,
    int QuantityPrecision,
    decimal TickSize,
    decimal LotSize,
    TradingStatus TradingStatus,
    DateTimeOffset? TradingStartAt,
    DateTimeOffset? TradingEndAt,
    DateOnly? ExpirationDate,
    decimal? ContractMultiplier,
    SettlementType? SettlementType,
    DeliveryType? DeliveryType,
    PaymentType? PaymentType,
    IReadOnlyDictionary<string, string>? Metadata = null);

public sealed record InstrumentTradingRule(
    Guid InstrumentId,
    InstrumentRuleProfile Profile,
    decimal MinQuantity,
    decimal? MaxQuantity,
    decimal TickSize,
    decimal LotSize,
    int PricePrecision,
    int QuantityPrecision,
    IReadOnlyCollection<OrderType> AllowedOrderTypes,
    IReadOnlyCollection<MarketSession> AllowedSessions,
    bool MatchingEnabled = true);

public sealed record InstrumentMarketConfig(
    Guid InstrumentId,
    TimeOnly RegularSessionStart,
    TimeOnly RegularSessionEnd,
    TimeOnly? AfterMarketSessionStart,
    TimeOnly? AfterMarketSessionEnd,
    TimeOnly? AuctionSessionStart,
    TimeOnly? AuctionSessionEnd,
    bool SeparateBook = false);

public sealed record InstrumentStatusRecord(
    Guid InstrumentId,
    TradingStatus Status,
    DateTimeOffset UpdatedAt,
    string? Notes = null);

public sealed record InstrumentDefinition(
    Instrument Instrument,
    InstrumentTradingRule TradingRule,
    InstrumentMarketConfig MarketConfig,
    InstrumentStatusRecord Status);

public sealed record Participant(
    Guid ParticipantId,
    string ParticipantCode,
    string LegalName,
    string DisplayName,
    ParticipantType ParticipantType,
    ParticipantStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record TradingAccount(
    Guid TradingAccountId,
    Guid AccountId,
    Guid ParticipantId,
    string? ExternalAccountCode,
    TradingAccountStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record TradeAllocation(
    Guid TradeAllocationId,
    string TradeExecutionId,
    Guid TradingAccountId,
    OrderSide Side,
    decimal AllocatedQuantity,
    AllocationStatus AllocationStatus,
    DateTimeOffset CreatedAt);

public sealed record SettlementObligation(
    Guid SettlementObligationId,
    Guid TradingAccountId,
    Guid InstrumentId,
    decimal Quantity,
    decimal? Amount,
    PlaceholderStatus Status,
    string Notes);

public sealed record SettlementBatch(
    Guid SettlementBatchId,
    string BatchCode,
    PlaceholderStatus Status,
    string Notes);

public sealed record NettingSet(
    Guid NettingSetId,
    Guid ParticipantId,
    PlaceholderStatus Status,
    string Notes);

public sealed record ClearingSession(
    Guid ClearingSessionId,
    string SessionCode,
    PlaceholderStatus Status,
    string Notes);

public sealed record RiskSnapshot(
    Guid RiskSnapshotId,
    Guid TradingAccountId,
    DateTimeOffset CapturedAt,
    PlaceholderStatus Status,
    string Notes);

public sealed record CustodyMovement(
    Guid CustodyMovementId,
    Guid TradingAccountId,
    Guid InstrumentId,
    decimal Quantity,
    PlaceholderStatus Status,
    string Notes);
