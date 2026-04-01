using System.Text.Json;
using Confluent.Kafka;
using Exchange.Platform.Contracts.Events;
using Exchange.Platform.Contracts.Messaging;

namespace Exchange.Ledger.Api;

public sealed class LedgerEventPublisher : IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly ILogger<LedgerEventPublisher> _logger;

    public LedgerEventPublisher(IConfiguration configuration, ILogger<LedgerEventPublisher> logger)
    {
        _logger = logger;
        var bootstrapServers = configuration["Kafka:BootstrapServers"] ?? "localhost:29092";
        _producer = new ProducerBuilder<string, string>(new ProducerConfig
        {
            BootstrapServers = bootstrapServers,
            Acks = Acks.All,
            EnableIdempotence = true
        }).Build();
    }

    public async Task PublishAsync(BalanceAdjusted balanceAdjusted, CancellationToken cancellationToken)
    {
        var envelope = new IntegrationEventEnvelope<BalanceAdjusted>(
            nameof(BalanceAdjusted),
            balanceAdjusted,
            balanceAdjusted.OccurredAt);

        await _producer.ProduceAsync(
            KafkaTopics.LedgerEvents,
            new Message<string, string>
            {
                Key = $"{balanceAdjusted.AccountId}:{balanceAdjusted.Asset}",
                Value = JsonSerializer.Serialize(envelope)
            },
            cancellationToken);

        _logger.LogInformation(
            "Ledger published {EventType} for AccountId={AccountId} Asset={Asset} AvailableDelta={AvailableDelta} ReservedDelta={ReservedDelta}.",
            nameof(BalanceAdjusted),
            balanceAdjusted.AccountId,
            balanceAdjusted.Asset,
            balanceAdjusted.AvailableDelta,
            balanceAdjusted.ReservedDelta);
    }

    public void Dispose()
    {
        _producer.Flush(TimeSpan.FromSeconds(10));
        _producer.Dispose();
    }
}
