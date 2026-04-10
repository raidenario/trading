use std::collections::HashMap;

use crate::domain::Order;
use crate::order_book::{MatchOutcome, OrderBook};

#[derive(Debug, Default)]
pub struct MatchingEngine {
    books: HashMap<String, OrderBook>,
    order_sequence: u64,
    trade_sequence: u64,
}

impl MatchingEngine {
    pub fn new() -> Self {
        Self::default()
    }

    pub fn submit(&mut self, order: Order) -> MatchOutcome {
        self.order_sequence += 1;
        let symbol = order.symbol.clone();
        let routing_key = Self::routing_key(&order);
        let instrument_id = order.instrument_id.clone();
        let sequenced_order = order.prepare_for_matching(self.order_sequence);
        let book = self
            .books
            .entry(routing_key)
            .or_insert_with_key(|key| OrderBook::new(key.clone(), symbol, instrument_id));

        book.submit(sequenced_order, &mut self.trade_sequence)
    }

    pub fn snapshot(&self, symbol: &str) -> Option<crate::order_book::BookSnapshot> {
        self.books.get(symbol).map(|book| book.snapshot())
    }

    pub fn snapshot_by_key(&self, key: &str) -> Option<crate::order_book::BookSnapshot> {
        self.books.get(key).map(|book| book.snapshot())
    }

    fn routing_key(order: &Order) -> String {
        match order.instrument_id.as_deref() {
            Some(instrument_id) => {
                let profile = order
                    .execution_instructions
                    .get("bookProfile")
                    .cloned()
                    .unwrap_or_else(|| "SpotStandard".to_string());
                let separate_book = order
                    .execution_instructions
                    .get("separateBook")
                    .map(|value| value.eq_ignore_ascii_case("true"))
                    .unwrap_or(false);

                if separate_book {
                    format!("{}|{}", instrument_id, profile)
                } else {
                    instrument_id.to_string()
                }
            }
            None => order.symbol.clone(),
        }
    }
}

#[cfg(test)]
mod tests {
    use crate::domain::{Order, OrderSide, OrderStatus, TimeInForce};

    use super::MatchingEngine;

    #[test]
    fn matches_crossing_limit_orders() {
        let mut engine = MatchingEngine::new();

        let resting_sell = Order::new_limit(
            "sell-1",
            "acct-sell",
            "BTC-USD",
            OrderSide::Sell,
            50_000,
            10,
            TimeInForce::Gtc,
        );
        let incoming_buy = Order::new_limit(
            "buy-1",
            "acct-buy",
            "BTC-USD",
            OrderSide::Buy,
            50_000,
            10,
            TimeInForce::Gtc,
        );

        let first = engine.submit(resting_sell);
        assert_eq!(first.order.status, OrderStatus::Accepted);
        assert_eq!(first.snapshot.asks.len(), 1);

        let second = engine.submit(incoming_buy);
        assert_eq!(second.trades.len(), 1);
        assert_eq!(second.trades[0].quantity, 10);
        assert_eq!(second.order.status, OrderStatus::Filled);
        assert!(second.snapshot.asks.is_empty());
    }

    #[test]
    fn preserves_fifo_within_same_price_level() {
        let mut engine = MatchingEngine::new();

        engine.submit(Order::new_limit(
            "sell-1",
            "acct-sell-1",
            "BTC-USD",
            OrderSide::Sell,
            50_000,
            5,
            TimeInForce::Gtc,
        ));
        engine.submit(Order::new_limit(
            "sell-2",
            "acct-sell-2",
            "BTC-USD",
            OrderSide::Sell,
            50_000,
            5,
            TimeInForce::Gtc,
        ));

        let result = engine.submit(Order::new_limit(
            "buy-1",
            "acct-buy-1",
            "BTC-USD",
            OrderSide::Buy,
            50_000,
            7,
            TimeInForce::Gtc,
        ));

        assert_eq!(result.trades.len(), 2);
        assert_eq!(result.trades[0].sell_order_id, "sell-1");
        assert_eq!(result.trades[0].quantity, 5);
        assert_eq!(result.trades[1].sell_order_id, "sell-2");
        assert_eq!(result.trades[1].quantity, 2);
        assert_eq!(result.snapshot.asks[0].quantity, 3);
    }

    #[test]
    fn matches_orders_from_kafka_payload_shape() {
        let mut engine = MatchingEngine::new();

        let sell_payload = r#"{
            "OrderId":"sell-1",
            "AccountId":"acct-sell",
            "Symbol":"BTC-USD",
            "Side":2,
            "Type":1,
            "Quantity":0.5000,
            "Price":50019.18,
            "TimeInForce":1,
            "ClientOrderId":"sim-sell",
            "SubmittedAt":"2026-03-23T15:11:49.11788+00:00",
            "SchemaVersion":1
        }"#;

        let buy_payload = r#"{
            "OrderId":"buy-1",
            "AccountId":"acct-buy",
            "Symbol":"BTC-USD",
            "Side":1,
            "Type":1,
            "Quantity":0.2500,
            "Price":50019.18,
            "TimeInForce":1,
            "ClientOrderId":"sim-buy",
            "SubmittedAt":"2026-03-23T15:11:59.637268+00:00",
            "SchemaVersion":1
        }"#;

        let first = engine.submit(serde_json::from_str(sell_payload).expect("sell payload"));
        let second = engine.submit(serde_json::from_str(buy_payload).expect("buy payload"));

        assert_eq!(first.order.status, OrderStatus::Accepted);
        assert_eq!(second.trades.len(), 1);
        assert_eq!(second.trades[0].quantity, 2_500);
        assert_eq!(second.trades[0].price, 500_191_800);
        assert_eq!(second.order.status, OrderStatus::Filled);
        assert_eq!(second.snapshot.asks[0].quantity, 2_500);
    }

    #[test]
    fn propagates_instrument_and_trading_account_ids_into_trades() {
        let mut engine = MatchingEngine::new();

        let sell_payload = r#"{
            "OrderId":"sell-1",
            "AccountId":"acct-sell",
            "TradingAccountId":"ta-sell",
            "InstrumentId":"inst-btcusd",
            "Symbol":"BTC-USD",
            "Side":2,
            "Type":1,
            "Quantity":0.5000,
            "Price":50019.18,
            "TimeInForce":1,
            "SchemaVersion":1
        }"#;

        let buy_payload = r#"{
            "OrderId":"buy-1",
            "AccountId":"acct-buy",
            "TradingAccountId":"ta-buy",
            "InstrumentId":"inst-btcusd",
            "Symbol":"BTC-USD",
            "Side":1,
            "Type":1,
            "Quantity":0.5000,
            "Price":50019.18,
            "TimeInForce":1,
            "SchemaVersion":1
        }"#;

        engine.submit(serde_json::from_str(sell_payload).expect("sell payload"));
        let result = engine.submit(serde_json::from_str(buy_payload).expect("buy payload"));

        assert_eq!(result.trades.len(), 1);
        assert_eq!(result.trades[0].instrument_id.as_deref(), Some("inst-btcusd"));
        assert_eq!(result.trades[0].buy_trading_account_id.as_deref(), Some("ta-buy"));
        assert_eq!(result.trades[0].sell_trading_account_id.as_deref(), Some("ta-sell"));
    }

    #[test]
    fn keeps_books_independent_when_symbol_matches_but_instrument_id_differs() {
        let mut engine = MatchingEngine::new();

        let first_instrument_order = serde_json::from_str::<Order>(r#"{
            "OrderId":"sell-1",
            "AccountId":"acct-sell",
            "InstrumentId":"inst-petr4-cash",
            "Symbol":"PETR4",
            "Side":2,
            "Type":1,
            "Quantity":100.0,
            "Price":37.10,
            "TimeInForce":1,
            "SchemaVersion":1
        }"#).expect("first instrument");

        let second_instrument_order = serde_json::from_str::<Order>(r#"{
            "OrderId":"buy-1",
            "AccountId":"acct-buy",
            "InstrumentId":"inst-petr4-fractional",
            "Symbol":"PETR4",
            "Side":1,
            "Type":1,
            "Quantity":100.0,
            "Price":37.10,
            "TimeInForce":1,
            "ExecutionInstructions":{"bookProfile":"SpotFractional","separateBook":"true"},
            "SchemaVersion":1
        }"#).expect("second instrument");

        let first = engine.submit(first_instrument_order);
        let second = engine.submit(second_instrument_order);

        assert_eq!(first.order.status, OrderStatus::Accepted);
        assert_eq!(second.order.status, OrderStatus::Accepted);
        assert!(second.trades.is_empty());
        assert!(engine.snapshot_by_key("inst-petr4-cash").is_some());
        assert!(engine.snapshot_by_key("inst-petr4-fractional|SpotFractional").is_some());
    }

    #[test]
    fn snapshot_carries_instrument_routing_metadata() {
        let mut engine = MatchingEngine::new();

        let order = serde_json::from_str::<Order>(r#"{
            "OrderId":"buy-1",
            "AccountId":"acct-buy",
            "InstrumentId":"inst-petr4-fractional",
            "Symbol":"PETR4",
            "Side":1,
            "Type":1,
            "Quantity":10.0,
            "Price":37.10,
            "TimeInForce":1,
            "ExecutionInstructions":{"bookProfile":"SpotFractional","separateBook":"true"},
            "SchemaVersion":1
        }"#).expect("instrument order");

        let result = engine.submit(order);

        assert_eq!(result.snapshot.instrument_id.as_deref(), Some("inst-petr4-fractional"));
        assert_eq!(result.snapshot.book_key, "inst-petr4-fractional|SpotFractional");
        assert_eq!(result.snapshot.symbol, "PETR4");
    }
}
