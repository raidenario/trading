#[derive(Debug, Clone, PartialEq, Eq)]
pub struct Trade {
    pub trade_id: String,
    pub symbol: String,
    pub buy_order_id: String,
    pub sell_order_id: String,
    pub buy_account_id: String,
    pub sell_account_id: String,
    pub price: u64,
    pub quantity: u64,
}
