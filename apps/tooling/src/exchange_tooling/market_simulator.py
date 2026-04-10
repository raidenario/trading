"""
Exchange Platform Market Simulator

Generates continuous fake market data and order flow to bring the
exchange platform to life during local development and demos.
"""
from __future__ import annotations

import math
import random
import time
from dataclasses import dataclass, field
from datetime import UTC, datetime

from .instruments import InstrumentCatalog, MarketSession


@dataclass(slots=True)
class MarketConfig:
    symbol: str
    base_price: float
    volatility: float = 0.002
    tick_size: float = 0.01
    lot_size: float = 0.0001


DEFAULT_MARKETS = [
    MarketConfig("BTC-USD", 50_000.0, 0.0015, 0.01, 0.0001),
    MarketConfig("ETH-USD", 3_500.0, 0.002, 0.01, 0.001),
    MarketConfig("SOL-USD", 125.0, 0.003, 0.01, 0.01),
]


def build_market_configs(
    *,
    symbols: tuple[str, ...] | None = None,
    asset_classes: tuple[str, ...] | None = None,
    book_mode: str | None = None,
    session: MarketSession = MarketSession.REGULAR,
) -> list[MarketConfig]:
    catalog = InstrumentCatalog.default()
    selected = catalog.filter(
        symbols=symbols,
        asset_classes=asset_classes,
        book_mode=book_mode,
        session=session,
    )

    return [
        MarketConfig(
            symbol=item.symbol,
            base_price=item.base_price,
            volatility=0.002 if item.asset_class != "Crypto" else 0.003,
            tick_size=item.tick_size,
            lot_size=item.lot_size,
        )
        for item in selected
    ] or DEFAULT_MARKETS


@dataclass
class SimulatedTicker:
    symbol: str
    last_price: float
    best_bid: float
    best_ask: float
    high_24h: float
    low_24h: float
    volume_24h: float
    open_price: float
    as_of: str = ""

    @property
    def change_24h(self) -> float:
        return round(self.last_price - self.open_price, 4)

    @property
    def change_percent_24h(self) -> float:
        if self.open_price == 0:
            return 0
        return round((self.last_price - self.open_price) / self.open_price * 100, 4)

    def to_dict(self) -> dict:
        return {
            "symbol": self.symbol,
            "lastPrice": self.last_price,
            "bestBid": self.best_bid,
            "bestAsk": self.best_ask,
            "high24h": self.high_24h,
            "low24h": self.low_24h,
            "volume24h": self.volume_24h,
            "change24h": self.change_24h,
            "changePercent24h": self.change_percent_24h,
            "asOf": self.as_of,
        }


@dataclass
class SimulatedCandle:
    symbol: str
    interval: str
    open: float
    high: float
    low: float
    close: float
    volume: float
    open_time: str
    close_time: str

    def to_dict(self) -> dict:
        return {
            "symbol": self.symbol,
            "interval": self.interval,
            "open": self.open,
            "high": self.high,
            "low": self.low,
            "close": self.close,
            "volume": self.volume,
            "openTime": self.open_time,
            "closeTime": self.close_time,
        }


@dataclass
class PriceEngine:
    """Geometric Brownian Motion price simulator."""

    config: MarketConfig
    current_price: float = 0
    _step: int = 0
    _high: float = 0
    _low: float = float("inf")
    _volume: float = 0
    _open: float = 0
    _candle_open: float = 0
    _candle_high: float = 0
    _candle_low: float = float("inf")
    _candle_volume: float = 0
    _candle_start: str = ""

    def __post_init__(self):
        self.current_price = self.config.base_price
        self._open = self.config.base_price
        self._high = self.config.base_price
        self._low = self.config.base_price
        self._candle_open = self.config.base_price
        self._candle_high = self.config.base_price
        self._candle_start = datetime.now(UTC).isoformat()

    def tick(self) -> SimulatedTicker:
        """Advance one tick with GBM-like randomness."""
        drift = 0.0
        shock = random.gauss(0, 1)
        change = self.config.volatility * shock + drift
        self.current_price *= 1 + change
        self.current_price = max(self.current_price, self.config.tick_size)
        self.current_price = round(self.current_price, 2)

        spread = max(self.config.tick_size, self.current_price * 0.0002)
        best_bid = round(self.current_price - spread / 2, 2)
        best_ask = round(self.current_price + spread / 2, 2)

        self._high = max(self._high, self.current_price)
        self._low = min(self._low, self.current_price)

        trade_qty = round(random.uniform(0.01, 2.0), 4)
        self._volume += trade_qty
        self._candle_volume += trade_qty
        self._candle_high = max(self._candle_high, self.current_price)
        self._candle_low = min(self._candle_low, self.current_price)
        self._step += 1

        now = datetime.now(UTC).isoformat()
        return SimulatedTicker(
            symbol=self.config.symbol,
            last_price=self.current_price,
            best_bid=best_bid,
            best_ask=best_ask,
            high_24h=self._high,
            low_24h=self._low,
            volume_24h=round(self._volume, 4),
            open_price=self._open,
            as_of=now,
        )

    def close_candle(self, interval: str = "1m") -> SimulatedCandle:
        now = datetime.now(UTC).isoformat()
        candle = SimulatedCandle(
            symbol=self.config.symbol,
            interval=interval,
            open=self._candle_open,
            high=self._candle_high,
            low=self._candle_low,
            close=self.current_price,
            volume=round(self._candle_volume, 4),
            open_time=self._candle_start,
            close_time=now,
        )
        self._candle_open = self.current_price
        self._candle_high = self.current_price
        self._candle_low = self.current_price
        self._candle_volume = 0
        self._candle_start = now
        return candle
