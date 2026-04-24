use std::collections::HashMap;

use chrono::{DateTime, Duration, Timelike, Utc};

#[derive(Debug, Clone, PartialEq)]
pub struct CandleState {
    pub symbol: String,
    pub interval: String,
    pub open: f64,
    pub high: f64,
    pub low: f64,
    pub close: f64,
    pub volume: f64,
    pub open_time: DateTime<Utc>,
    pub close_time: DateTime<Utc>,
}

#[derive(Debug, Clone)]
pub struct CandleBook {
    interval: String,
    candles: HashMap<String, CandleState>,
}

impl CandleBook {
    pub fn new(interval: impl Into<String>) -> Self {
        Self {
            interval: interval.into(),
            candles: HashMap::new(),
        }
    }

    pub fn apply_trade(
        &mut self,
        symbol: impl Into<String>,
        executed_at: DateTime<Utc>,
        price: f64,
        quantity: f64,
    ) -> CandleState {
        let symbol = symbol.into();
        let open_time = floor_to_minute(executed_at);
        let close_time = open_time + Duration::seconds(59);

        let candle = self
            .candles
            .entry(symbol.clone())
            .and_modify(|current| {
                if current.open_time != open_time {
                    *current = CandleState {
                        symbol: symbol.clone(),
                        interval: self.interval.clone(),
                        open: price,
                        high: price,
                        low: price,
                        close: price,
                        volume: quantity,
                        open_time,
                        close_time,
                    };
                } else {
                    current.high = current.high.max(price);
                    current.low = current.low.min(price);
                    current.close = price;
                    current.volume += quantity;
                    current.close_time = close_time;
                }
            })
            .or_insert_with(|| CandleState {
                symbol: symbol.clone(),
                interval: self.interval.clone(),
                open: price,
                high: price,
                low: price,
                close: price,
                volume: quantity,
                open_time,
                close_time,
            });

        candle.clone()
    }
}

fn floor_to_minute(timestamp: DateTime<Utc>) -> DateTime<Utc> {
    timestamp
        .with_second(0)
        .and_then(|value| value.with_nanosecond(0))
        .expect("valid UTC timestamp")
}

#[cfg(test)]
mod tests {
    use chrono::{TimeZone, Utc};

    use super::CandleBook;

    #[test]
    fn aggregates_multiple_trades_within_same_minute() {
        let mut book = CandleBook::new("1m");
        let first = Utc.with_ymd_and_hms(2026, 4, 24, 13, 0, 5).unwrap();
        let second = Utc.with_ymd_and_hms(2026, 4, 24, 13, 0, 42).unwrap();

        book.apply_trade("PETR4", first, 25.10, 100.0);
        let candle = book.apply_trade("PETR4", second, 25.80, 120.0);

        assert_eq!(candle.open, 25.10);
        assert_eq!(candle.high, 25.80);
        assert_eq!(candle.low, 25.10);
        assert_eq!(candle.close, 25.80);
        assert_eq!(candle.volume, 220.0);
        assert_eq!(candle.open_time, Utc.with_ymd_and_hms(2026, 4, 24, 13, 0, 0).unwrap());
        assert_eq!(candle.close_time, Utc.with_ymd_and_hms(2026, 4, 24, 13, 0, 59).unwrap());
    }

    #[test]
    fn starts_new_candle_when_trade_moves_to_next_minute() {
        let mut book = CandleBook::new("1m");
        let first = Utc.with_ymd_and_hms(2026, 4, 24, 13, 0, 5).unwrap();
        let second = Utc.with_ymd_and_hms(2026, 4, 24, 13, 1, 1).unwrap();

        book.apply_trade("PETR4", first, 25.10, 100.0);
        let candle = book.apply_trade("PETR4", second, 25.80, 120.0);

        assert_eq!(candle.open, 25.80);
        assert_eq!(candle.high, 25.80);
        assert_eq!(candle.low, 25.80);
        assert_eq!(candle.close, 25.80);
        assert_eq!(candle.volume, 120.0);
        assert_eq!(candle.open_time, Utc.with_ymd_and_hms(2026, 4, 24, 13, 1, 0).unwrap());
    }
}
