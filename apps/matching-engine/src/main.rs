use chrono::{DateTime, Utc};
use rdkafka::config::ClientConfig;
use rdkafka::consumer::{Consumer, StreamConsumer};
use rdkafka::message::Message;
use rdkafka::producer::{FutureProducer, FutureRecord};
use rdkafka::util::Timeout;
use serde::{Deserialize, Serialize};
use matching_engine::{MatchingEngine, Order, OrderStatus};

const DECIMAL_SCALE: f64 = 10_000.0;
const ORDER_COMMANDS_TOPIC: &str = "order-commands";
const MATCHING_EVENTS_TOPIC: &str = "matching-events";
const MARKETDATA_EVENTS_TOPIC: &str = "marketdata-events";

fn format_ticks(value: u64) -> String {
    format!("{:.4}", value as f64 / DECIMAL_SCALE)
}

fn format_price(price: Option<u64>) -> String {
    price
        .map(format_ticks)
        .unwrap_or_else(|| "MARKET".to_string())
}

fn ticks_to_decimal(value: u64) -> f64 {
    value as f64 / DECIMAL_SCALE
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "PascalCase")]
struct CancelOrderCommand {
    #[serde(alias = "orderId")]
    order_id: String,
}

#[derive(Debug, Serialize)]
#[serde(rename_all = "PascalCase")]
struct IntegrationEventEnvelope<T>
where
    T: Serialize,
{
    event_type: String,
    payload: T,
    occurred_at: DateTime<Utc>,
    schema_version: i32,
}

#[derive(Debug, Serialize)]
#[serde(rename_all = "PascalCase")]
struct OrderAcceptedEvent {
    order_id: String,
    account_id: String,
    symbol: String,
    status: String,
    remaining_quantity: f64,
    accepted_at: DateTime<Utc>,
    schema_version: i32,
}

#[derive(Debug, Serialize)]
#[serde(rename_all = "PascalCase")]
struct OrderRejectedEvent {
    order_id: String,
    account_id: String,
    symbol: String,
    reason: String,
    rejected_at: DateTime<Utc>,
    schema_version: i32,
}

#[derive(Debug, Serialize)]
#[serde(rename_all = "PascalCase")]
struct TradeExecutedEvent {
    trade_id: String,
    buy_order_id: String,
    sell_order_id: String,
    buy_account_id: String,
    sell_account_id: String,
    instrument_id: Option<String>,
    buy_trading_account_id: Option<String>,
    sell_trading_account_id: Option<String>,
    symbol: String,
    price: f64,
    quantity: f64,
    executed_at: DateTime<Utc>,
    schema_version: i32,
}

#[derive(Debug, Serialize)]
#[serde(rename_all = "PascalCase")]
struct BookUpdatedEvent {
    instrument_id: Option<String>,
    book_key: String,
    symbol: String,
    bids: Vec<BookLevelDto>,
    asks: Vec<BookLevelDto>,
    as_of: DateTime<Utc>,
    schema_version: i32,
}

#[derive(Debug, Serialize)]
#[serde(rename_all = "PascalCase")]
struct BookLevelDto {
    price: f64,
    quantity: f64,
    order_count: usize,
}

#[derive(Debug, Serialize)]
#[serde(rename_all = "PascalCase")]
struct TickerUpdatedEvent {
    instrument_id: Option<String>,
    book_key: String,
    symbol: String,
    last_price: f64,
    best_bid: f64,
    best_ask: f64,
    volume24_h: f64,
    change24_h: f64,
    as_of: DateTime<Utc>,
    schema_version: i32,
}

async fn publish_event<T>(
    producer: &FutureProducer,
    topic: &str,
    key: &str,
    event_type: &str,
    payload: T,
) where
    T: Serialize,
{
    let envelope = IntegrationEventEnvelope {
        event_type: event_type.to_string(),
        payload,
        occurred_at: Utc::now(),
        schema_version: 1,
    };

    match serde_json::to_string(&envelope) {
        Ok(json) => {
            let record = FutureRecord::to(topic).key(key).payload(&json);
            if let Err((error, _)) = producer.send(record, Timeout::Never).await {
                log::error!("Failed to publish {} to {}: {}", event_type, topic, error);
            }
        }
        Err(error) => log::error!("Failed to serialize {} envelope: {}", event_type, error),
    }
}

#[tokio::main]
async fn main() {
    env_logger::init_from_env(env_logger::Env::default().default_filter_or("info"));

    let broker = std::env::var("KAFKA_BOOTSTRAP_SERVERS").unwrap_or_else(|_| "localhost:29092".to_string());
    log::info!("Starting Matching Engine Service...");
    log::info!("Connecting to Kafka at: {}", broker);

    let consumer: StreamConsumer = ClientConfig::new()
        .set("group.id", "matching-engine-group")
        .set("bootstrap.servers", &broker)
        .set("auto.offset.reset", "earliest")
        .create()
        .expect("Consumer creation failed");

    let producer: FutureProducer = ClientConfig::new()
        .set("bootstrap.servers", &broker)
        .set("message.timeout.ms", "5000")
        .create()
        .expect("Producer creation failed");

    consumer
        .subscribe(&[ORDER_COMMANDS_TOPIC])
        .expect("Can't subscribe to specified topics");

    let mut engine = MatchingEngine::new();
    log::info!("Matching Engine ready. Listening for orders...");

    loop {
        match consumer.recv().await {
            Err(error) => log::error!("Kafka error: {}", error),
            Ok(message) => {
                let payload = match message.payload_view::<str>() {
                    None => continue,
                    Some(Ok(text)) => text,
                    Some(Err(error)) => {
                        log::error!("Error deserializing message payload: {}", error);
                        continue;
                    }
                };

                if payload.contains("\"RequestedAt\"") && !payload.contains("\"Quantity\"") {
                    if let Ok(cancel) = serde_json::from_str::<CancelOrderCommand>(payload) {
                        log::warn!("Cancel command received for {} but cancel flow is not wired in the engine yet.", cancel.order_id);
                        continue;
                    }
                }

                match serde_json::from_str::<Order>(payload) {
                    Ok(order) => {
                        log::info!(
                            ">>> NEW ORDER: {} {} {} @ {}",
                            order.side,
                            format_ticks(order.quantity),
                            order.symbol,
                            format_price(order.price)
                        );

                        let outcome = engine.submit(order);
                        let order_status = outcome.order.status.clone();
                        let now = Utc::now();

                        if order_status == OrderStatus::Rejected {
                            publish_event(
                                &producer,
                                MATCHING_EVENTS_TOPIC,
                                &outcome.order.symbol,
                                "OrderRejected",
                                OrderRejectedEvent {
                                    order_id: outcome.order.id.clone(),
                                    account_id: outcome.order.account_id.clone(),
                                    symbol: outcome.order.symbol.clone(),
                                    reason: "Order rejected by matching rules or insufficient resting liquidity.".to_string(),
                                    rejected_at: now,
                                    schema_version: 1,
                                },
                            ).await;
                        } else if outcome.order.remaining_quantity > 0 {
                            publish_event(
                                &producer,
                                MATCHING_EVENTS_TOPIC,
                                &outcome.order.symbol,
                                "OrderAccepted",
                                OrderAcceptedEvent {
                                    order_id: outcome.order.id.clone(),
                                    account_id: outcome.order.account_id.clone(),
                                    symbol: outcome.order.symbol.clone(),
                                    status: format!("{:?}", outcome.order.status),
                                    remaining_quantity: ticks_to_decimal(outcome.order.remaining_quantity),
                                    accepted_at: now,
                                    schema_version: 1,
                                },
                            ).await;
                        }

                        let mut last_trade_price = None;
                        let mut traded_volume = 0.0_f64;

                        for trade in outcome.trades {
                            let trade_symbol = trade.symbol.clone();
                            let trade_key = trade.instrument_id.clone().unwrap_or_else(|| trade_symbol.clone());
                            last_trade_price = Some(ticks_to_decimal(trade.price));
                            traded_volume += ticks_to_decimal(trade.quantity);

                            log::info!(
                                "    MATCH FOUND: {} {} @ {}",
                                trade.symbol,
                                format_ticks(trade.quantity),
                                format_ticks(trade.price)
                            );
                            log::info!("    (Buy: {} | Sell: {})", trade.buy_order_id, trade.sell_order_id);

                            publish_event(
                                &producer,
                                MATCHING_EVENTS_TOPIC,
                                &trade_key,
                                "TradeExecuted",
                                TradeExecutedEvent {
                                    trade_id: trade.trade_id,
                                    buy_order_id: trade.buy_order_id,
                                    sell_order_id: trade.sell_order_id,
                                    buy_account_id: trade.buy_account_id,
                                    sell_account_id: trade.sell_account_id,
                                    instrument_id: trade.instrument_id,
                                    buy_trading_account_id: trade.buy_trading_account_id,
                                    sell_trading_account_id: trade.sell_trading_account_id,
                                    symbol: trade_symbol,
                                    price: ticks_to_decimal(trade.price),
                                    quantity: ticks_to_decimal(trade.quantity),
                                    executed_at: now,
                                    schema_version: 1,
                                },
                            ).await;
                        }

                        publish_event(
                            &producer,
                            MARKETDATA_EVENTS_TOPIC,
                            &outcome.snapshot.book_key,
                            "BookUpdated",
                            BookUpdatedEvent {
                                instrument_id: outcome.snapshot.instrument_id.clone(),
                                book_key: outcome.snapshot.book_key.clone(),
                                symbol: outcome.snapshot.symbol.clone(),
                                bids: outcome.snapshot.bids.iter().map(|level| BookLevelDto {
                                    price: ticks_to_decimal(level.price),
                                    quantity: ticks_to_decimal(level.quantity),
                                    order_count: level.order_count,
                                }).collect(),
                                asks: outcome.snapshot.asks.iter().map(|level| BookLevelDto {
                                    price: ticks_to_decimal(level.price),
                                    quantity: ticks_to_decimal(level.quantity),
                                    order_count: level.order_count,
                                }).collect(),
                                as_of: now,
                                schema_version: 1,
                            },
                        ).await;

                        if let Some(last_trade_price) = last_trade_price {
                            let best_bid = outcome.snapshot.bids.first().map(|level| ticks_to_decimal(level.price)).unwrap_or(last_trade_price);
                            let best_ask = outcome.snapshot.asks.first().map(|level| ticks_to_decimal(level.price)).unwrap_or(last_trade_price);

                            publish_event(
                                &producer,
                                MARKETDATA_EVENTS_TOPIC,
                                &outcome.snapshot.book_key,
                                "TickerUpdated",
                                TickerUpdatedEvent {
                                    instrument_id: outcome.snapshot.instrument_id.clone(),
                                    book_key: outcome.snapshot.book_key.clone(),
                                    symbol: outcome.snapshot.symbol.clone(),
                                    last_price: last_trade_price,
                                    best_bid,
                                    best_ask,
                                    volume24_h: traded_volume,
                                    change24_h: 0.0,
                                    as_of: now,
                                    schema_version: 1,
                                },
                            ).await;
                        }

                        log::info!("<<< STATUS: {:?}", order_status);
                    }
                    Err(error) => {
                        log::error!("Failed to parse order JSON: {}. Payload: {}", error, payload);
                    }
                }
            }
        }
    }
}
