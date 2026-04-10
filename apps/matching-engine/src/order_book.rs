use std::collections::BTreeMap;

use crate::domain::{Order, OrderSide, OrderStatus, OrderType, TimeInForce, Trade};
use crate::price_level::PriceLevel;

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct BookLevel {
    pub price: u64,
    pub quantity: u64,
    pub order_count: usize,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct BookSnapshot {
    pub book_key: String,
    pub instrument_id: Option<String>,
    pub symbol: String,
    pub bids: Vec<BookLevel>,
    pub asks: Vec<BookLevel>,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct MatchOutcome {
    pub order: Order,
    pub trades: Vec<Trade>,
    pub snapshot: BookSnapshot,
}

#[derive(Debug, Clone)]
pub struct OrderBook {
    pub book_key: String,
    pub instrument_id: Option<String>,
    pub symbol: String,
    bids: BTreeMap<u64, PriceLevel>,
    asks: BTreeMap<u64, PriceLevel>,
}

impl OrderBook {
    pub fn new(
        book_key: impl Into<String>,
        symbol: impl Into<String>,
        instrument_id: Option<String>,
    ) -> Self {
        Self {
            book_key: book_key.into(),
            instrument_id,
            symbol: symbol.into(),
            bids: BTreeMap::new(),
            asks: BTreeMap::new(),
        }
    }

    pub fn submit(&mut self, mut incoming: Order, trade_sequence: &mut u64) -> MatchOutcome {
        if incoming.time_in_force == TimeInForce::Fok && !self.can_fully_fill(&incoming) {
            incoming.reject();
            return MatchOutcome {
                order: incoming,
                trades: Vec::new(),
                snapshot: self.snapshot(),
            };
        }

        incoming.accept();

        let trades = match incoming.side {
            OrderSide::Buy => self.match_buy(&mut incoming, trade_sequence),
            OrderSide::Sell => self.match_sell(&mut incoming, trade_sequence),
        };

        if incoming.remaining_quantity > 0 && incoming.can_rest() {
            self.rest_order(incoming.clone());
            if incoming.remaining_quantity < incoming.quantity {
                incoming.status = OrderStatus::PartiallyFilled;
            } else {
                incoming.status = OrderStatus::Accepted;
            }
        } else if incoming.remaining_quantity > 0 && incoming.order_type == OrderType::Market {
            incoming.status = if trades.is_empty() {
                OrderStatus::Rejected
            } else {
                OrderStatus::PartiallyFilled
            };
        } else if incoming.remaining_quantity > 0 {
            incoming.status = if trades.is_empty() {
                OrderStatus::Rejected
            } else {
                OrderStatus::PartiallyFilled
            };
        } else {
            incoming.status = OrderStatus::Filled;
        }

        MatchOutcome {
            order: incoming,
            trades,
            snapshot: self.snapshot(),
        }
    }

    pub fn cancel(&mut self, order_id: &str, side: OrderSide, price: u64) -> bool {
        let levels = match side {
            OrderSide::Buy => &mut self.bids,
            OrderSide::Sell => &mut self.asks,
        };

        let mut retained = PriceLevel::new(price);
        let mut cancelled = false;

        {
            let Some(level) = levels.get_mut(&price) else {
                return false;
            };

            while let Some(order) = level.pop_front() {
                if order.id == order_id {
                    cancelled = true;
                    continue;
                }

                retained.push_back(order);
            }

            *level = retained;
        }

        let should_remove = levels.get(&price).map(|level| level.is_empty()).unwrap_or(false);

        if should_remove {
            levels.remove(&price);
        }

        cancelled
    }

    pub fn snapshot(&self) -> BookSnapshot {
        let bids = self
            .bids
            .iter()
            .rev()
            .map(|(_, level)| BookLevel {
                price: level.price(),
                quantity: level.total_quantity(),
                order_count: level.order_count(),
            })
            .collect();

        let asks = self
            .asks
            .iter()
            .map(|(_, level)| BookLevel {
                price: level.price(),
                quantity: level.total_quantity(),
                order_count: level.order_count(),
            })
            .collect();

        BookSnapshot {
            book_key: self.book_key.clone(),
            instrument_id: self.instrument_id.clone(),
            symbol: self.symbol.clone(),
            bids,
            asks,
        }
    }

    fn can_fully_fill(&self, order: &Order) -> bool {
        let available: u64 = match order.side {
            OrderSide::Buy => self
                .asks
                .iter()
                .filter(|(price, _)| order.price.map(|limit| **price <= limit).unwrap_or(true))
                .map(|(_, level)| level.total_quantity())
                .sum(),
            OrderSide::Sell => self
                .bids
                .iter()
                .rev()
                .filter(|(price, _)| order.price.map(|limit| **price >= limit).unwrap_or(true))
                .map(|(_, level)| level.total_quantity())
                .sum(),
        };

        available >= order.quantity
    }

    fn match_buy(&mut self, incoming: &mut Order, trade_sequence: &mut u64) -> Vec<Trade> {
        let mut trades = Vec::new();

        while incoming.remaining_quantity > 0 {
            let Some(best_ask_price) = self.asks.keys().next().copied() else {
                break;
            };

            if let Some(limit_price) = incoming.price {
                if best_ask_price > limit_price {
                    break;
                }
            }

            let remove_level;

            {
                let level = self.asks.get_mut(&best_ask_price).expect("ask level exists");

                while incoming.remaining_quantity > 0 {
                    let Some(resting) = level.front_mut() else {
                        break;
                    };

                    let fill_quantity = incoming.remaining_quantity.min(resting.remaining_quantity);

                    incoming.apply_fill(fill_quantity);
                    resting.apply_fill(fill_quantity);
                    *trade_sequence += 1;

                    trades.push(Trade {
                        trade_id: format!("trade-{:010}", *trade_sequence),
                        instrument_id: incoming.instrument_id.clone().or_else(|| resting.instrument_id.clone()),
                        symbol: incoming.symbol.clone(),
                        buy_order_id: incoming.id.clone(),
                        sell_order_id: resting.id.clone(),
                        buy_account_id: incoming.account_id.clone(),
                        sell_account_id: resting.account_id.clone(),
                        buy_trading_account_id: incoming.trading_account_id.clone(),
                        sell_trading_account_id: resting.trading_account_id.clone(),
                        price: best_ask_price,
                        quantity: fill_quantity,
                    });

                    if resting.is_filled() {
                        level.pop_front();
                    }

                    if incoming.is_filled() {
                        break;
                    }
                }

                remove_level = level.is_empty();
            }

            if remove_level {
                self.asks.remove(&best_ask_price);
            }
        }

        trades
    }

    fn match_sell(&mut self, incoming: &mut Order, trade_sequence: &mut u64) -> Vec<Trade> {
        let mut trades = Vec::new();

        while incoming.remaining_quantity > 0 {
            let Some(best_bid_price) = self.bids.keys().next_back().copied() else {
                break;
            };

            if let Some(limit_price) = incoming.price {
                if best_bid_price < limit_price {
                    break;
                }
            }

            let remove_level;

            {
                let level = self.bids.get_mut(&best_bid_price).expect("bid level exists");

                while incoming.remaining_quantity > 0 {
                    let Some(resting) = level.front_mut() else {
                        break;
                    };

                    let fill_quantity = incoming.remaining_quantity.min(resting.remaining_quantity);

                    incoming.apply_fill(fill_quantity);
                    resting.apply_fill(fill_quantity);
                    *trade_sequence += 1;

                    trades.push(Trade {
                        trade_id: format!("trade-{:010}", *trade_sequence),
                        instrument_id: incoming.instrument_id.clone().or_else(|| resting.instrument_id.clone()),
                        symbol: incoming.symbol.clone(),
                        buy_order_id: resting.id.clone(),
                        sell_order_id: incoming.id.clone(),
                        buy_account_id: resting.account_id.clone(),
                        sell_account_id: incoming.account_id.clone(),
                        buy_trading_account_id: resting.trading_account_id.clone(),
                        sell_trading_account_id: incoming.trading_account_id.clone(),
                        price: best_bid_price,
                        quantity: fill_quantity,
                    });

                    if resting.is_filled() {
                        level.pop_front();
                    }

                    if incoming.is_filled() {
                        break;
                    }
                }

                remove_level = level.is_empty();
            }

            if remove_level {
                self.bids.remove(&best_bid_price);
            }
        }

        trades
    }

    fn rest_order(&mut self, order: Order) {
        let price = order.price.expect("resting order must have a price");
        let levels = match order.side {
            OrderSide::Buy => &mut self.bids,
            OrderSide::Sell => &mut self.asks,
        };

        levels
            .entry(price)
            .or_insert_with(|| PriceLevel::new(price))
            .push_back(order);
    }
}
