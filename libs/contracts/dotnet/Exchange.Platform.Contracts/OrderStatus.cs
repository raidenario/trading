namespace Exchange.Platform.Contracts;

public enum OrderStatus
{
    Pending = 1,
    Accepted = 2,
    PartiallyFilled = 3,
    Filled = 4,
    Cancelled = 5,
    Rejected = 6
}
