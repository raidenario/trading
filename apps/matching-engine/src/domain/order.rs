use serde::{Deserialize, Serialize};

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
pub enum OrderSide {
    Buy,
    Sell,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
pub enum OrderType {
    Limit,
    Market,
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

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
pub enum TimeInForce {
    Gtc,
    Ioc,
    Fok,
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct Order {
    #[serde(alias = "orderId")]
    pub id: String,
    pub account_id: String,
    pub symbol: String,
    pub side: OrderSide,
    #[serde(alias = "type")]
    pub order_type: OrderType,
    pub time_in_force: TimeInForce,
    pub price: Option<u64>,
    pub quantity: u64,
    pub remaining_quantity: u64,
    #[serde(default)]
    pub sequence: u64,
    pub status: OrderStatus,
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
