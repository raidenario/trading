"""
Historical real-market replay for the exchange simulator.

This module keeps yfinance at the edge of the system. Historical candles are
used only as input to generate regular CreateOrderCommand-compatible payloads,
which are submitted through the Gateway API so Kafka, the matching engine,
ledger service and query projections all participate in the run.
"""
from __future__ import annotations

import math
import time
from dataclasses import dataclass, field
from datetime import UTC, datetime, timedelta
from typing import Callable, Iterable, Protocol

import httpx

from .generators import PARTICIPANTS
from .instruments import InstrumentCatalog, InstrumentDefinition, MarketSession, quantize_step
from .models import OrderRequest


@dataclass(frozen=True, slots=True)
class SymbolMapping:
    symbol: str
    source_symbol: str


@dataclass(frozen=True, slots=True)
class RealMarketConfig:
    symbol: str
    source_symbol: str
    instrument: InstrumentDefinition
    session: MarketSession = MarketSession.REGULAR
    volume_scale: float = 0.00001


@dataclass(frozen=True, slots=True)
class HistoricalCandle:
    timestamp: datetime
    open: float
    high: float
    low: float
    close: float
    volume: float
    source_timestamp: datetime | None = None


@dataclass(frozen=True, slots=True)
class HistoricalReplayConfig:
    symbols: tuple[str, ...] | None = None
    start: str = "2023-01-01"
    end: str = "2023-12-31"
    interval: str = "1d"
    endpoint: str = "http://localhost:5103"
    speed: float = 1.0
    session: MarketSession = MarketSession.REGULAR
    dry_run: bool = False
    max_candles_per_symbol: int | None = None
    replay_clock: str = "historical"
    replay_start: str | None = None
    replay_step_seconds: int | None = None


@dataclass(frozen=True, slots=True)
class RealMarketReplaySummary:
    candles_replayed: int = 0
    orders_sent: int = 0
    orders_failed: int = 0
    symbols: tuple[str, ...] = field(default_factory=tuple)


@dataclass(frozen=True, slots=True)
class GatewaySubmissionResult:
    accepted: bool
    reason: str | None = None


class HistoricalMarketDataProvider(Protocol):
    def download(self, source_symbol: str, *, start: str, end: str, interval: str) -> list[HistoricalCandle]:
        ...


class GatewayClient(Protocol):
    def submit_order(self, payload: dict[str, object]) -> GatewaySubmissionResult:
        ...


ProgressCallback = Callable[[str, dict[str, object]], None]


class StaticHistoricalMarketDataProvider:
    """Deterministic provider used by tests and offline demos."""

    def __init__(self, candles_by_source_symbol: dict[str, Iterable[HistoricalCandle]]) -> None:
        self._candles_by_source_symbol = {
            symbol.upper(): sorted(candles, key=lambda item: item.timestamp)
            for symbol, candles in candles_by_source_symbol.items()
        }

    def download(self, source_symbol: str, *, start: str, end: str, interval: str) -> list[HistoricalCandle]:
        del interval
        start_dt = _parse_boundary(start)
        end_dt = _parse_boundary(end)
        candles = self._candles_by_source_symbol.get(source_symbol.upper(), [])
        return [
            candle
            for candle in candles
            if (start_dt is None or candle.timestamp >= start_dt)
            and (end_dt is None or candle.timestamp < end_dt)
        ]


class YFinanceHistoricalMarketDataProvider:
    """Downloads OHLCV candles using yfinance and normalizes them for replay."""

    def __init__(self, downloader: Callable[..., object] | None = None, *, auto_adjust: bool = False) -> None:
        self._downloader = downloader
        self._auto_adjust = auto_adjust

    def download(self, source_symbol: str, *, start: str, end: str, interval: str) -> list[HistoricalCandle]:
        downloader = self._downloader or self._load_yfinance_downloader()
        frame = downloader(
            source_symbol,
            start=start,
            end=end,
            interval=interval,
            auto_adjust=self._auto_adjust,
            progress=False,
            threads=False,
            multi_level_index=False,
        )
        return self._normalize_frame(frame)

    @staticmethod
    def _load_yfinance_downloader() -> Callable[..., object]:
        try:
            import yfinance as yf  # type: ignore
        except ImportError as error:  # pragma: no cover - exercised only when dependency is missing at runtime
            raise RuntimeError(
                "yfinance is required for real-market-simulator. Install apps/tooling dependencies first."
            ) from error

        return yf.download

    @staticmethod
    def _normalize_frame(frame: object) -> list[HistoricalCandle]:
        if hasattr(frame, "empty") and bool(getattr(frame, "empty")):
            return []

        candles: list[HistoricalCandle] = []
        if hasattr(frame, "iterrows"):
            for timestamp, row in frame.iterrows():  # type: ignore[attr-defined]
                candle = _candle_from_row(timestamp, row)
                if candle is not None:
                    candles.append(candle)
            return sorted(candles, key=lambda item: item.timestamp)

        if isinstance(frame, Iterable):
            for record in frame:
                if not isinstance(record, dict):
                    continue
                timestamp = record.get("timestamp") or record.get("Date") or record.get("Datetime")
                candle = _candle_from_row(timestamp, record)
                if candle is not None:
                    candles.append(candle)
            return sorted(candles, key=lambda item: item.timestamp)

        return []


class HttpGatewayClient:
    def __init__(self, endpoint: str, *, timeout: float = 10.0) -> None:
        self._endpoint = endpoint.rstrip("/")
        self._client = httpx.Client(timeout=timeout)

    def submit_order(self, payload: dict[str, object]) -> GatewaySubmissionResult:
        response = self._client.post(f"{self._endpoint}/api/orders", json=payload)
        if response.status_code < 400:
            return GatewaySubmissionResult(True)
        return GatewaySubmissionResult(False, _extract_gateway_reason(response))

    def close(self) -> None:
        self._client.close()


class InMemoryGatewayClient:
    def __init__(self) -> None:
        self.payloads: list[dict[str, object]] = []

    def submit_order(self, payload: dict[str, object]) -> GatewaySubmissionResult:
        self.payloads.append(payload)
        return GatewaySubmissionResult(True)


class SyntheticOrderFactory:
    def __init__(self, catalog: InstrumentCatalog | None = None) -> None:
        self._catalog = catalog or InstrumentCatalog.default()
        self._rotation = 0

    def orders_from_candle(self, config: RealMarketConfig, candle: HistoricalCandle) -> list[OrderRequest]:
        prices = self._intracandle_path(candle)
        quantity = self._quantity_for_candle(config.instrument, candle, len(prices), config.volume_scale)
        orders: list[OrderRequest] = []

        for price in prices:
            maker = PARTICIPANTS[self._rotation % len(PARTICIPANTS)]
            taker = PARTICIPANTS[(self._rotation + 1) % len(PARTICIPANTS)]
            maker_side = "Buy" if self._rotation % 2 == 0 else "Sell"
            taker_side = "Sell" if maker_side == "Buy" else "Buy"
            submitted_at = _format_timestamp(self._submitted_at(config, candle))
            order_price = quantize_step(max(price, config.instrument.tick_size), config.instrument.tick_size, config.instrument.price_precision)

            orders.append(
                OrderRequest.create(
                    account_id=maker.account_id,
                    symbol=config.instrument.symbol,
                    instrument_id=config.instrument.instrument_id,
                    side=maker_side,
                    quantity=quantity,
                    price=order_price,
                    order_type="Limit",
                    client_order_suffix="real-market-maker",
                    execution_instructions=self._execution_instructions(config, candle),
                    submitted_at=submitted_at,
                )
            )
            orders.append(
                OrderRequest.create(
                    account_id=taker.account_id,
                    symbol=config.instrument.symbol,
                    instrument_id=config.instrument.instrument_id,
                    side=taker_side,
                    quantity=quantity,
                    price=order_price,
                    order_type="Limit",
                    client_order_suffix="real-market-taker",
                    execution_instructions=self._execution_instructions(config, candle),
                    submitted_at=submitted_at,
                )
            )
            self._rotation += 1

        return orders

    @staticmethod
    def _intracandle_path(candle: HistoricalCandle) -> tuple[float, float, float, float]:
        if candle.close >= candle.open:
            return candle.open, candle.low, candle.high, candle.close
        return candle.open, candle.high, candle.low, candle.close

    @staticmethod
    def _quantity_for_candle(
        instrument: InstrumentDefinition,
        candle: HistoricalCandle,
        steps: int,
        volume_scale: float,
    ) -> float:
        scaled_volume = max(candle.volume * volume_scale, instrument.min_quantity)
        raw_quantity = scaled_volume / max(steps, 1)
        if instrument.max_quantity is not None:
            raw_quantity = min(raw_quantity, instrument.max_quantity)
        raw_quantity = max(raw_quantity, instrument.min_quantity)
        return quantize_step(raw_quantity, instrument.lot_size, instrument.quantity_precision)

    @staticmethod
    def _submitted_at(config: RealMarketConfig, candle: HistoricalCandle) -> datetime:
        if candle.source_timestamp is not None and candle.source_timestamp != candle.timestamp:
            return candle.timestamp
        return _submitted_at_for_session(candle.timestamp, config.session)

    @staticmethod
    def _execution_instructions(config: RealMarketConfig, candle: HistoricalCandle) -> dict[str, str]:
        source_candle_time = candle.source_timestamp or candle.timestamp
        return {
            "source": "RealMarketSimulator",
            "sourceSymbol": config.source_symbol,
            "sourceCandleTime": _format_timestamp(source_candle_time),
            "bookProfile": config.instrument.book_mode.title().replace("_", ""),
            "matchingEnabled": "true",
            "separateBook": "true" if config.instrument.separate_book else "false",
            "assetClass": config.instrument.asset_class,
            "market": config.instrument.market,
        }


def build_default_symbol_mappings(catalog: InstrumentCatalog | None = None) -> dict[str, SymbolMapping]:
    catalog = catalog or InstrumentCatalog.default()
    candidates = {
        "BTC-USD": "BTC-USD",
        "ETH-USD": "ETH-USD",
        "SOL-USD": "SOL-USD",
        "PETR4": "PETR4.SA",
        "PETR4F": "PETR4.SA",
        "VALE3": "VALE3.SA",
        "ITUB4": "ITUB4.SA",
        "BOVA11": "BOVA11.SA",
        "IVVB11": "IVVB11.SA",
        "AAPL34": "AAPL34.SA",
        "MSFT34": "MSFT34.SA",
        "USD-BRL": "BRL=X",
    }
    return {
        symbol: SymbolMapping(symbol=symbol, source_symbol=source_symbol)
        for symbol, source_symbol in candidates.items()
        if catalog.get(symbol) is not None
    }


def build_real_market_configs(
    *,
    catalog: InstrumentCatalog | None = None,
    symbols: tuple[str, ...] | None = None,
    session: MarketSession = MarketSession.REGULAR,
) -> list[RealMarketConfig]:
    catalog = catalog or InstrumentCatalog.default()
    mappings = build_default_symbol_mappings(catalog)
    requested = [symbol.upper() for symbol in symbols] if symbols else list(mappings)
    configs: list[RealMarketConfig] = []

    for symbol in requested:
        mapping = mappings.get(symbol)
        instrument = catalog.get(symbol)
        if mapping is None or instrument is None:
            continue
        if instrument.status in {"HALTED", "SUSPENDED", "DISABLED", "AUCTION"}:
            continue
        if instrument.status == "AFTER_MARKET_ONLY" and session != MarketSession.AFTER_MARKET:
            continue
        if session not in instrument.allowed_sessions:
            continue
        configs.append(RealMarketConfig(symbol=symbol, source_symbol=mapping.source_symbol, instrument=instrument, session=session))

    return configs


def run_real_market_simulation(
    *,
    replay_config: HistoricalReplayConfig,
    provider: HistoricalMarketDataProvider | None = None,
    gateway: GatewayClient | None = None,
    catalog: InstrumentCatalog | None = None,
    sleep: Callable[[float], None] = time.sleep,
    progress: ProgressCallback | None = None,
) -> RealMarketReplaySummary:
    catalog = catalog or InstrumentCatalog.default()
    provider = provider or YFinanceHistoricalMarketDataProvider()
    own_gateway = gateway is None
    gateway = gateway or HttpGatewayClient(replay_config.endpoint)
    factory = SyntheticOrderFactory(catalog=catalog)
    configs = build_real_market_configs(
        catalog=catalog,
        symbols=replay_config.symbols,
        session=replay_config.session,
    )

    candles_replayed = 0
    orders_sent = 0
    orders_failed = 0

    try:
        for config in configs:
            _emit(progress, "symbol_start", symbol=config.symbol, source_symbol=config.source_symbol)
            candles = provider.download(
                config.source_symbol,
                start=replay_config.start,
                end=replay_config.end,
                interval=replay_config.interval,
            )
            if replay_config.max_candles_per_symbol is not None:
                candles = candles[: replay_config.max_candles_per_symbol]
            candles = _apply_replay_clock(candles, replay_config=replay_config, session=config.session)
            _emit(progress, "candles_loaded", symbol=config.symbol, source_symbol=config.source_symbol, count=len(candles))

            for candle in candles:
                candles_replayed += 1
                orders = factory.orders_from_candle(config, candle)
                _emit(
                    progress,
                    "candle_start",
                    symbol=config.symbol,
                    timestamp=_format_timestamp(candle.timestamp),
                    open=candle.open,
                    high=candle.high,
                    low=candle.low,
                    close=candle.close,
                    volume=candle.volume,
                    order_count=len(orders),
                )
                for order in orders:
                    payload = order.to_payload()
                    submission = GatewaySubmissionResult(True) if replay_config.dry_run else gateway.submit_order(payload)
                    if submission.accepted:
                        orders_sent += 1
                        _emit(
                            progress,
                            "order_sent",
                            symbol=config.symbol,
                            side=payload["side"],
                            quantity=payload["quantity"],
                            price=payload["price"],
                            orders_sent=orders_sent,
                            dry_run=replay_config.dry_run,
                        )
                    else:
                        orders_failed += 1
                        _emit(
                            progress,
                            "order_failed",
                            symbol=config.symbol,
                            side=payload["side"],
                            quantity=payload["quantity"],
                            price=payload["price"],
                            orders_failed=orders_failed,
                            reason=submission.reason,
                        )
                    if replay_config.speed > 0:
                        sleep(replay_config.speed)
            _emit(progress, "symbol_finished", symbol=config.symbol, candles_replayed=candles_replayed, orders_sent=orders_sent)
    finally:
        if own_gateway and hasattr(gateway, "close"):
            gateway.close()  # type: ignore[attr-defined]

    return RealMarketReplaySummary(
        candles_replayed=candles_replayed,
        orders_sent=orders_sent,
        orders_failed=orders_failed,
        symbols=tuple(config.symbol for config in configs),
    )


def _candle_from_row(timestamp: object, row: object) -> HistoricalCandle | None:
    open_value = _row_value(row, "Open")
    high_value = _row_value(row, "High")
    low_value = _row_value(row, "Low")
    close_value = _row_value(row, "Close")
    volume_value = _row_value(row, "Volume")

    if any(value is None for value in (open_value, high_value, low_value, close_value, volume_value)):
        return None

    values = [float(open_value), float(high_value), float(low_value), float(close_value), float(volume_value)]
    if any(math.isnan(value) for value in values):
        return None

    parsed_timestamp = _parse_timestamp(timestamp)
    return HistoricalCandle(
        timestamp=parsed_timestamp,
        open=values[0],
        high=values[1],
        low=values[2],
        close=values[3],
        volume=values[4],
    )


def _row_value(row: object, name: str) -> object | None:
    if isinstance(row, dict):
        return _as_scalar(row.get(name) if name in row else row.get(name.lower()))
    try:
        return _as_scalar(row[name])  # type: ignore[index]
    except Exception:
        return _as_scalar(getattr(row, name, None))


def _as_scalar(value: object | None) -> object | None:
    if value is None:
        return None
    if hasattr(value, "iloc") and hasattr(value, "__len__"):
        try:
            if len(value) == 0:  # type: ignore[arg-type]
                return None
            return value.iloc[0]  # type: ignore[attr-defined]
        except Exception:
            return value
    return value


def _parse_timestamp(value: object) -> datetime:
    if isinstance(value, datetime):
        parsed = value
    else:
        parsed = datetime.fromisoformat(str(value).replace("Z", "+00:00"))

    if parsed.tzinfo is None:
        return parsed.replace(tzinfo=UTC)
    return parsed.astimezone(UTC)


def _parse_boundary(value: str | None) -> datetime | None:
    if not value:
        return None
    return _parse_timestamp(value)


def _format_timestamp(value: datetime) -> str:
    return value.astimezone(UTC).isoformat().replace("+00:00", "Z")


def _apply_replay_clock(
    candles: list[HistoricalCandle],
    *,
    replay_config: HistoricalReplayConfig,
    session: MarketSession,
) -> list[HistoricalCandle]:
    if replay_config.replay_clock.lower() == "historical":
        return candles

    if replay_config.replay_clock.lower() != "compressed-now":
        raise ValueError(f"Unsupported replay_clock: {replay_config.replay_clock}")

    if not candles:
        return []

    start = (
        _parse_timestamp(replay_config.replay_start)
        if replay_config.replay_start
        else _submitted_at_for_session(datetime.now(UTC), session)
    )
    step_seconds = replay_config.replay_step_seconds or _default_replay_step_seconds(replay_config.interval)

    replayed: list[HistoricalCandle] = []
    for index, candle in enumerate(sorted(candles, key=lambda item: item.timestamp)):
        replayed.append(
            HistoricalCandle(
                timestamp=start + index * timedelta(seconds=step_seconds),
                open=candle.open,
                high=candle.high,
                low=candle.low,
                close=candle.close,
                volume=candle.volume,
                source_timestamp=candle.source_timestamp or candle.timestamp,
            )
        )
    return replayed


def _default_replay_step_seconds(interval: str) -> int:
    normalized = interval.strip().lower()
    defaults = {
        "1d": 60,
        "1h": 15,
        "30m": 10,
        "15m": 5,
        "5m": 2,
        "1m": 1,
    }
    return defaults.get(normalized, 60)


def _submitted_at_for_session(value: datetime, session: MarketSession) -> datetime:
    base = value.astimezone(UTC)
    hour_by_session = {
        MarketSession.REGULAR: 13,
        MarketSession.AFTER_MARKET: 20,
        MarketSession.AUCTION: 12,
        MarketSession.CLOSED: 22,
    }
    minute_by_session = {
        MarketSession.REGULAR: 0,
        MarketSession.AFTER_MARKET: 0,
        MarketSession.AUCTION: 50,
        MarketSession.CLOSED: 0,
    }
    return base.replace(
        hour=hour_by_session[session],
        minute=minute_by_session[session],
        second=0,
        microsecond=0,
    )


def _extract_gateway_reason(response: httpx.Response) -> str:
    try:
        payload = response.json()
    except ValueError:
        return f"HTTP {response.status_code}"

    if isinstance(payload, dict):
        reason = payload.get("reason") or payload.get("title") or payload.get("detail")
        if reason:
            return str(reason)

    return f"HTTP {response.status_code}"


def _emit(progress: ProgressCallback | None, event: str, **payload: object) -> None:
    if progress is not None:
        progress(event, payload)
