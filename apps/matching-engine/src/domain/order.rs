use std::collections::HashMap;

use serde::{Deserialize, Serialize, de};
use serde_repr::{Deserialize_repr, Serialize_repr};
use std::fmt;

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize_repr, Deserialize_repr)]
#[repr(u8)]
pub enum OrderSide {
    Buy = 1,
    Sell = 2,
}

impl fmt::Display for OrderSide {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Self::Buy => write!(f, "BUY"),
            Self::Sell => write!(f, "SELL"),
        }
    }
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize_repr, Deserialize_repr)]
#[repr(u8)]
pub enum OrderType {
    Limit = 1,
    Market = 2,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
pub enum OrderStatus {
    Pending,
    Accepted,
    PartiallyFilled,
    Filled,
    Cancelled,
    Rejected,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize_repr, Deserialize_repr)]
#[repr(u8)]
pub enum TimeInForce {
    Gtc = 1,
    Ioc = 2,
    Fok = 3,
}

#[derive(Debug, Clone, PartialEq, Eq, Deserialize, Serialize)]
#[serde(rename_all = "PascalCase")]
pub struct Order {
    #[serde(rename = "OrderId", alias = "orderId", alias = "Id", alias = "id")]
    pub id: String,
    
    #[serde(rename = "AccountId", alias = "accountId", alias = "account_id")]
    pub account_id: String,

    #[serde(rename = "TradingAccountId", alias = "tradingAccountId", alias = "trading_account_id", default)]
    pub trading_account_id: Option<String>,

    #[serde(rename = "InstrumentId", alias = "instrumentId", alias = "instrument_id", default)]
    pub instrument_id: Option<String>,

    #[serde(rename = "ExecutionInstructions", alias = "executionInstructions", alias = "execution_instructions", default)]
    pub execution_instructions: HashMap<String, String>,
    
    pub symbol: String,
    
    pub side: OrderSide,
    
    #[serde(rename = "Type", alias = "OrderType", alias = "order_type")]
    pub order_type: OrderType,
    
    #[serde(rename = "TimeInForce", alias = "time_in_force")]
    pub time_in_force: TimeInForce,

    #[serde(default, deserialize_with = "deserialize_price")]
    pub price: Option<u64>,

    #[serde(deserialize_with = "deserialize_quantity")]
    pub quantity: u64,

    #[serde(default, skip_deserializing)]
    pub remaining_quantity: u64,

    #[serde(default)]
    pub sequence: u64,

    #[serde(default = "default_status")]
    pub status: OrderStatus,
}

fn default_status() -> OrderStatus {
    OrderStatus::Pending
}

// Multiplicador para manter precisão (ex: 124.59 -> 1245900 ticks)
const PRICE_MULTIPLIER: f64 = 10000.0;

fn deserialize_price<'de, D>(deserializer: D) -> Result<Option<u64>, D::Error>
where D: de::Deserializer<'de> {
    let opt: Option<f64> = Deserialize::deserialize(deserializer)?;
    Ok(opt.map(|p| (p * PRICE_MULTIPLIER) as u64))
}

fn deserialize_quantity<'de, D>(deserializer: D) -> Result<u64, D::Error>
where D: de::Deserializer<'de> {
    let q: f64 = Deserialize::deserialize(deserializer)?;
    Ok((q * PRICE_MULTIPLIER) as u64)
}

impl Order {
    pub fn new_limit(
        id: impl Into<String>,
        account_id: impl Into<String>,
        symbol: impl Into<String>,
        side: OrderSide,
        price: u64,
        quantity: u64,
        time_in_force: TimeInForce,
    ) -> Self {
        Self {
            id: id.into(),
            account_id: account_id.into(),
            trading_account_id: None,
            instrument_id: None,
            execution_instructions: HashMap::new(),
            symbol: symbol.into(),
            side,
            order_type: OrderType::Limit,
            time_in_force,
            price: Some(price),
            quantity,
            remaining_quantity: quantity,
            sequence: 0,
            status: OrderStatus::Pending,
        }
    }

    pub fn new_market(
        id: impl Into<String>,
        account_id: impl Into<String>,
        symbol: impl Into<String>,
        side: OrderSide,
        quantity: u64,
        time_in_force: TimeInForce,
    ) -> Self {
        Self {
            id: id.into(),
            account_id: account_id.into(),
            trading_account_id: None,
            instrument_id: None,
            execution_instructions: HashMap::new(),
            symbol: symbol.into(),
            side,
            order_type: OrderType::Market,
            time_in_force,
            price: None,
            quantity,
            remaining_quantity: quantity,
            sequence: 0,
            status: OrderStatus::Pending,
        }
    }

    pub fn with_sequence(mut self, sequence: u64) -> Self {
        self.sequence = sequence;
        self
    }

    pub fn prepare_for_matching(mut self, sequence: u64) -> Self {
        self.sequence = sequence;
        self.remaining_quantity = self.quantity;
        self.status = OrderStatus::Pending;

        if self.order_type == OrderType::Market {
            self.price = None;
        }

        self
    }

    pub fn apply_fill(&mut self, fill_quantity: u64) {
        self.remaining_quantity = self.remaining_quantity.saturating_sub(fill_quantity);
        self.status = if self.remaining_quantity == 0 {
            OrderStatus::Filled
        } else {
            OrderStatus::PartiallyFilled
        };
    }

    pub fn reject(&mut self) {
        self.status = OrderStatus::Rejected;
    }

    pub fn accept(&mut self) {
        self.status = OrderStatus::Accepted;
    }

    pub fn can_rest(&self) -> bool {
        self.order_type == OrderType::Limit && self.time_in_force == TimeInForce::Gtc
    }

    pub fn is_filled(&self) -> bool {
        self.remaining_quantity == 0
    }
}

#[cfg(test)]
mod tests {
    use super::{Order, OrderStatus, OrderType, TimeInForce};

    #[test]
    fn deserializes_kafka_payload_and_initializes_runtime_fields() {
        let payload = r#"{
            "OrderId":"24f2dd65-e2ed-41ea-94b6-a9412e930926",
            "AccountId":"22222222-2222-2222-2222-222222222222",
            "Symbol":"SOL-USD",
            "Side":2,
            "Type":1,
            "Quantity":1.3383,
            "Price":125.45,
            "TimeInForce":1,
            "ClientOrderId":"sim-a504c8c9",
            "SubmittedAt":"2026-03-23T15:11:38.612508+00:00",
            "SchemaVersion":1
        }"#;

        let order = serde_json::from_str::<Order>(payload)
            .expect("payload from Kafka must deserialize")
            .prepare_for_matching(42);

        assert_eq!(order.id, "24f2dd65-e2ed-41ea-94b6-a9412e930926");
        assert_eq!(order.account_id, "22222222-2222-2222-2222-222222222222");
        assert_eq!(order.symbol, "SOL-USD");
        assert_eq!(order.order_type, OrderType::Limit);
        assert_eq!(order.time_in_force, TimeInForce::Gtc);
        assert_eq!(order.quantity, 13_383);
        assert_eq!(order.remaining_quantity, 13_383);
        assert_eq!(order.price, Some(1_254_500));
        assert_eq!(order.sequence, 42);
        assert_eq!(order.status, OrderStatus::Pending);
    }

    #[test]
    fn deserializes_b3_fields_when_present_and_keeps_old_payloads_compatible() {
        let payload = r#"{
            "OrderId":"24f2dd65-e2ed-41ea-94b6-a9412e930926",
            "AccountId":"22222222-2222-2222-2222-222222222222",
            "TradingAccountId":"bbbbbbbb-0000-0000-0000-000000000001",
            "InstrumentId":"aaaaaaaa-0000-0000-0000-000000000001",
            "Symbol":"SOL-USD",
            "Side":2,
            "Type":1,
            "Quantity":1.3383,
            "Price":125.45,
            "TimeInForce":1,
            "ClientOrderId":"sim-a504c8c9",
            "SubmittedAt":"2026-03-23T15:11:38.612508+00:00",
            "SchemaVersion":1
        }"#;

        let order = serde_json::from_str::<Order>(payload)
            .expect("payload with B3 fields must deserialize")
            .prepare_for_matching(42);

        assert_eq!(order.instrument_id.as_deref(), Some("aaaaaaaa-0000-0000-0000-000000000001"));
        assert_eq!(order.trading_account_id.as_deref(), Some("bbbbbbbb-0000-0000-0000-000000000001"));
        assert!(order.execution_instructions.is_empty());
        assert_eq!(order.symbol, "SOL-USD");
        assert_eq!(order.order_type, OrderType::Limit);
    }
}
