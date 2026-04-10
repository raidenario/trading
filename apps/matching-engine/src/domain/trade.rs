#[derive(Debug, Clone, PartialEq, Eq)]
pub struct Trade {
    pub trade_id: String,
    pub instrument_id: Option<String>,
    pub symbol: String,
    pub buy_order_id: String,
    pub sell_order_id: String,
    pub buy_account_id: String,
    pub sell_account_id: String,
    pub buy_trading_account_id: Option<String>,
    pub sell_trading_account_id: Option<String>,
    pub price: u64,
    pub quantity: u64,
}
