namespace Exchange.Platform.Contracts;

public enum AssetClass
{
    Spot = 1,
    Crypto = 2,
    Equity = 3,
    FixedIncome = 4,
    Derivative = 5,
    Synthetic = 6,
    Etf = 7,
    Bdr = 8,
    Fx = 9,
    Commodity = 10
}

public enum Segment
{
    Cash = 1,
    Crypto = 2,
    Futures = 3,
    Options = 4,
    Lending = 5
}

public enum Market
{
    Simulator = 1,
    Spot = 2,
    Derivatives = 3,
    OTC = 4
}

public enum ParticipantType
{
    Broker = 1,
    Member = 2,
    InternalDesk = 3
}

public enum ParticipantStatus
{
    Active = 1,
    Inactive = 2,
    Suspended = 3
}

public enum TradingAccountStatus
{
    Active = 1,
    Inactive = 2,
    Suspended = 3
}

public enum TradingStatus
{
    Active = 1,
    Halted = 2,
    Expired = 3,
    Suspended = 4,
    Auction = 5,
    AfterMarketOnly = 6,
    Disabled = 7
}

public enum InstrumentRuleProfile
{
    SpotStandard = 1,
    SpotFractional = 2,
    SpotExtendedHours = 3,
    Disabled = 4
}

public enum MarketSession
{
    Closed = 1,
    Regular = 2,
    AfterMarket = 3,
    Auction = 4
}

public enum DeliveryType
{
    None = 1,
    Physical = 2,
    Cash = 3
}

public enum PaymentType
{
    Undefined = 1,
    Dvp = 2,
    FreeOfPayment = 3
}

public enum SettlementType
{
    Undefined = 1,
    Spot = 2,
    Future = 3,
    Deferred = 4
}

public enum AllocationStatus
{
    Pending = 1,
    Allocated = 2,
    Cancelled = 3
}

public enum OrderSource
{
    Api = 1,
    Web = 2,
    Simulator = 3,
    Admin = 4
}

public enum ReferenceType
{
    Funding = 1,
    Order = 2,
    TradeExecution = 3,
    TradeAllocation = 4,
    Position = 5,
    SettlementPlaceholder = 6
}

public enum BalanceBucket
{
    Available = 1,
    Reserved = 2,
    Total = 3
}

public enum EntryDirection
{
    Debit = 1,
    Credit = 2
}

public enum PlaceholderStatus
{
    Reserved = 1,
    Pending = 2,
    Open = 3,
    Closed = 4
}
