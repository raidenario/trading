using System.Text.Json;
using System.Text.Json.Serialization;
using Confluent.Kafka;
using Exchange.Platform.Contracts.Commands;
using Exchange.Platform.Contracts.Events;
using Exchange.Platform.Contracts.Messaging;

namespace Exchange.Query.Api;

public sealed class KafkaProjectionConsumer(
    IConfiguration configuration,
    ILogger<KafkaProjectionConsumer> logger,
    QueryProjectionStore store,
    PostgresProjectionWriter postgresWriter) : BackgroundService
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
        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = bootstrapServers,
            GroupId = "query-api-projections",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = true
        };

        using var consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();
        consumer.Subscribe(
        [
            KafkaTopics.AccountEvents,
            KafkaTopics.OrderCommands,
            KafkaTopics.MatchingEvents,
            KafkaTopics.LedgerEvents,
            KafkaTopics.MarketDataEvents
        ]);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(stoppingToken);
                Process(result.Topic, result.Message.Value);
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

                logger.LogError(ex, "Query API consumer failed.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected projection error in Query API.");
            }
        }
    }

    private void Process(string topic, string payload)
    {
        if (topic == KafkaTopics.OrderCommands)
        {
            var order = JsonSerializer.Deserialize<CreateOrderCommand>(payload, JsonOptions);
            if (order is not null)
            {
                store.Apply(order);
                postgresWriter.UpsertOrderAsync(order, CancellationToken.None).GetAwaiter().GetResult();
                logger.LogInformation(
                    "Query consumed {Topic} for OrderId={OrderId} AccountId={AccountId} Symbol={Symbol}.",
                    topic,
                    order.OrderId,
                    order.AccountId,
                    order.Symbol);
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
                PersistAndApply<AccountCreated>(envelope, store.Apply, (message, ct) => postgresWriter.UpsertAccountAsync(message, ct));
                logger.LogInformation("Query consumed {Topic} event {EventType}.", topic, envelope.EventType);
                break;
            case nameof(OrderAccepted):
                PersistAndApply<OrderAccepted>(envelope, store.Apply, (message, ct) => postgresWriter.ApplyOrderAcceptedAsync(message, ct));
                logger.LogInformation("Query consumed {Topic} event {EventType}.", topic, envelope.EventType);
                break;
            case nameof(OrderRejected):
                PersistAndApply<OrderRejected>(envelope, store.Apply, (message, ct) => postgresWriter.ApplyOrderRejectedAsync(message, ct));
                logger.LogInformation("Query consumed {Topic} event {EventType}.", topic, envelope.EventType);
                break;
            case nameof(TradeExecuted):
                PersistAndApply<TradeExecuted>(envelope, store.Apply, (message, ct) => postgresWriter.ApplyTradeExecutedAsync(message, ct));
                logger.LogInformation("Query consumed {Topic} event {EventType}.", topic, envelope.EventType);
                break;
            case nameof(BalanceAdjusted):
                logger.LogInformation("Query consumed {Topic} event {EventType}.", topic, envelope.EventType);
                DeserializeAndApply<BalanceAdjusted>(envelope, store.Apply);
                break;
            case nameof(TickerUpdated):
                logger.LogInformation("Query consumed {Topic} event {EventType}.", topic, envelope.EventType);
                DeserializeAndApply<TickerUpdated>(envelope, store.Apply);
                break;
        }
    }

    private void PersistAndApply<T>(
        IntegrationEventEnvelope envelope,
        Action<T> apply,
        Func<T, CancellationToken, Task> persist)
        where T : class
    {
        var message = envelope.Payload.Deserialize<T>(JsonOptions);
        if (message is null)
        {
            return;
        }

        apply(message);
        persist(message, CancellationToken.None).GetAwaiter().GetResult();
    }

    private void DeserializeAndApply<T>(IntegrationEventEnvelope envelope, Action<T> apply)
        where T : class
    {
        var message = envelope.Payload.Deserialize<T>(JsonOptions);
        if (message is not null)
        {
            apply(message);
        }
    }
}
