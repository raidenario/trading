use rdkafka::config::ClientConfig;
use rdkafka::consumer::{Consumer, StreamConsumer};
use rdkafka::message::Message;
use matching_engine::{MatchingEngine, Order};

const DECIMAL_SCALE: f64 = 10_000.0;

fn format_ticks(value: u64) -> String {
    format!("{:.4}", value as f64 / DECIMAL_SCALE)
}

fn format_price(price: Option<u64>) -> String {
    price
        .map(format_ticks)
        .unwrap_or_else(|| "MARKET".to_string())
}

#[tokio::main]
async fn main() {
    // Configura logs para aparecerem no terminal
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

    consumer
        .subscribe(&["order-commands"])
        .expect("Can't subscribe to specified topics");

    let mut engine = MatchingEngine::new();
    log::info!("Matching Engine ready. Listening for orders...");

    loop {
        match consumer.recv().await {
            Err(e) => log::error!("Kafka error: {}", e),
            Ok(m) => {
                let payload = match m.payload_view::<str>() {
                    None => continue,
                    Some(Ok(s)) => s,
                    Some(Err(e)) => {
                        log::error!("Error deserializing message payload: {}", e);
                        continue;
                    }
                };

                // Tenta desserializar a ordem do JSON
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
                        
                        // Log dos trades resultantes
                        for trade in outcome.trades {
                            log::info!(
                                "    MATCH FOUND: {} {} @ {}",
                                trade.symbol,
                                format_ticks(trade.quantity),
                                format_ticks(trade.price)
                            );
                            log::info!("    (Buy: {} | Sell: {})", trade.buy_order_id, trade.sell_order_id);
                        }
                        
                        log::info!("<<< STATUS: {:?}", outcome.order.status);
                    }
                    Err(e) => {
                        log::error!("Failed to parse order JSON: {}. Payload: {}", e, payload);
                    }
                }
            }
        }
    }
}
