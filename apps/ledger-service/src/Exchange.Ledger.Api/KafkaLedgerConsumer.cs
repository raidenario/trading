using System.Text.Json;
using System.Text.Json.Serialization;
using Confluent.Kafka;
using Exchange.Platform.Contracts.Commands;
using Exchange.Platform.Contracts.Events;
using Exchange.Platform.Contracts.Messaging;

namespace Exchange.Ledger.Api;

public sealed class KafkaLedgerConsumer(
    IConfiguration configuration,
    ILogger<KafkaLedgerConsumer> logger,
    LedgerProjectionStore store,
    LedgerEventPublisher publisher) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        Task.Run(() => ConsumeLoop(stoppingToken), stoppingToken);

    private void ConsumeLoop(CancellationToken stoppingToken)
    {
        var bootstrapServers = configuration["Kafka:BootstrapServers"] ?? "localhost:29092";
        using var consumer = new ConsumerBuilder<string, string>(new ConsumerConfig
        {
            BootstrapServers = bootstrapServers,
            GroupId = "ledger-service-projections",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = true
        }).Build();

        consumer.Subscribe(
        [
            KafkaTopics.AccountEvents,
            KafkaTopics.OrderCommands,
            KafkaTopics.MatchingEvents
        ]);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(stoppingToken);
                ProcessAsync(result.Topic, result.Message.Value, stoppingToken).GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ConsumeException ex)
            {
                if (ex.Error.Code is ErrorCode.UnknownTopicOrPart or ErrorCode.Local_UnknownTopic)
                {
                    logger.LogWarning(
                        "Kafka topics ainda nao estao prontos em {BootstrapServers}. Aguardando bootstrap dos topicos...",
                        bootstrapServers);
                    Thread.Sleep(TimeSpan.FromSeconds(3));
                    continue;
                }

                logger.LogError(ex, "Ledger consumer failure.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error in ledger projection loop.");
            }
        }
    }

    private async Task ProcessAsync(string topic, string payload, CancellationToken cancellationToken)
    {
        if (topic == KafkaTopics.OrderCommands)
        {
            var order = JsonSerializer.Deserialize<CreateOrderCommand>(payload, JsonOptions);
            if (order is not null)
            {
                var adjustments = store.Apply(order).ToArray();
                logger.LogInformation(
                    "Ledger consumed {Topic} for OrderId={OrderId} AccountId={AccountId} Symbol={Symbol} and produced {AdjustmentCount} balance adjustments.",
                    topic,
                    order.OrderId,
                    order.AccountId,
                    order.Symbol,
                    adjustments.Length);

                foreach (var adjustment in adjustments)
                {
                    await publisher.PublishAsync(adjustment, cancellationToken);
                }
            }

            return;
        }

        var envelope = JsonSerializer.Deserialize<IntegrationEventEnvelope>(payload, JsonOptions);
        if (envelope is null)
        {
            return;
        }

        switch (envelope.EventType)
        {
            case nameof(AccountCreated):
                logger.LogInformation("Ledger consumed {Topic} event {EventType}.", topic, envelope.EventType);
                Apply<AccountCreated>(envelope, store.Apply);
                break;
            case nameof(AccountFunded):
            {
                var message = envelope.Payload.Deserialize<AccountFunded>(JsonOptions);
                if (message is not null)
                {
                    logger.LogInformation(
                        "Ledger consumed {Topic} event {EventType} for AccountId={AccountId} Asset={Asset}.",
                        topic,
                        envelope.EventType,
                        message.AccountId,
                        message.Asset);
                    await publisher.PublishAsync(store.Apply(message), cancellationToken);
                }

                break;
            }
            case nameof(OrderRejected):
            {
                var message = envelope.Payload.Deserialize<OrderRejected>(JsonOptions);
                if (message is not null)
                {
                    logger.LogInformation(
                        "Ledger consumed {Topic} event {EventType} for OrderId={OrderId}.",
                        topic,
                        envelope.EventType,
                        message.OrderId);
                    var adjustment = store.Apply(message);
                    if (adjustment is not null)
                    {
                        await publisher.PublishAsync(adjustment, cancellationToken);
                    }
                }

                break;
            }
            case nameof(TradeExecuted):
            {
                var message = envelope.Payload.Deserialize<TradeExecuted>(JsonOptions);
                if (message is not null)
                {
                    var adjustments = store.Apply(message).ToArray();
                    logger.LogInformation(
                        "Ledger consumed {Topic} event {EventType} TradeId={TradeId} BuyAccountId={BuyAccountId} SellAccountId={SellAccountId} and produced {AdjustmentCount} balance adjustments.",
                        topic,
                        envelope.EventType,
                        message.TradeId,
                        message.BuyAccountId,
                        message.SellAccountId,
                        adjustments.Length);

                    foreach (var adjustment in adjustments)
                    {
                        await publisher.PublishAsync(adjustment, cancellationToken);
                    }
                }

                break;
            }
        }
    }

    private static void Apply<T>(IntegrationEventEnvelope envelope, Action<T> apply)
        where T : class
    {
        var message = envelope.Payload.Deserialize<T>(JsonOptions);
        if (message is not null)
        {
            apply(message);
        }
    }
}
