namespace Exchange.Platform.Contracts.Messaging;

public static class KafkaTopics
{
    public const string OrderCommands = "order-commands";
    public const string MatchingEvents = "matching-events";
    public const string LedgerEvents = "ledger-events";
    public const string MarketDataEvents = "marketdata-events";
    public const string AccountEvents = "account-events";
}
