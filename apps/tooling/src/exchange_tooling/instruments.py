from __future__ import annotations

from dataclasses import dataclass
from datetime import UTC, datetime
from decimal import Decimal, ROUND_HALF_UP
from enum import Enum


class MarketSession(str, Enum):
    REGULAR = "REGULAR"
    AFTER_MARKET = "AFTER_MARKET"
    CLOSED = "CLOSED"
    AUCTION = "AUCTION"


@dataclass(frozen=True, slots=True)
class InstrumentDefinition:
    instrument_id: str
    symbol: str
    asset_class: str
    book_mode: str
    status: str
    base_price: float
    tick_size: float
    lot_size: float
    min_quantity: float
    max_quantity: float | None
    price_precision: int
    quantity_precision: int
    allowed_order_types: tuple[str, ...]
    allowed_sessions: tuple[MarketSession, ...]
    separate_book: bool = False


@dataclass(frozen=True, slots=True)
class ValidationResult:
    is_valid: bool
    reason: str | None = None


DEFAULT_INSTRUMENTS: tuple[InstrumentDefinition, ...] = (
    InstrumentDefinition("aaaaaaaa-0000-0000-0000-000000000001", "BTC-USD", "Crypto", "SPOT_STANDARD", "ACTIVE", 50_000.0, 0.01, 0.00000001, 0.00000001, 100.0, 2, 8, ("Limit", "Market"), (MarketSession.REGULAR,)),
    InstrumentDefinition("aaaaaaaa-0000-0000-0000-000000000002", "ETH-USD", "Crypto", "SPOT_STANDARD", "ACTIVE", 3_500.0, 0.01, 0.00000001, 0.00000001, 1_000.0, 2, 8, ("Limit", "Market"), (MarketSession.REGULAR,)),
    InstrumentDefinition("aaaaaaaa-0000-0000-0000-000000000003", "SOL-USD", "Crypto", "SPOT_STANDARD", "ACTIVE", 125.0, 0.0001, 0.00000001, 0.00000001, 1_000.0, 4, 8, ("Limit", "Market"), (MarketSession.REGULAR,)),
    InstrumentDefinition("aaaaaaaa-0000-0000-0000-000000000101", "PETR4", "Equity", "SPOT_STANDARD", "ACTIVE", 37.10, 0.01, 100.0, 100.0, 1_000_000.0, 2, 0, ("Limit", "Market"), (MarketSession.REGULAR,)),
    InstrumentDefinition("aaaaaaaa-0000-0000-0000-000000000102", "VALE3", "Equity", "SPOT_STANDARD", "ACTIVE", 62.45, 0.01, 100.0, 100.0, 1_000_000.0, 2, 0, ("Limit", "Market"), (MarketSession.REGULAR,)),
    InstrumentDefinition("aaaaaaaa-0000-0000-0000-000000000103", "ITUB4", "Equity", "SPOT_STANDARD", "ACTIVE", 31.80, 0.01, 100.0, 100.0, 1_000_000.0, 2, 0, ("Limit", "Market"), (MarketSession.REGULAR,)),
    InstrumentDefinition("aaaaaaaa-0000-0000-0000-000000000111", "PETR4F", "Equity", "SPOT_FRACTIONAL", "ACTIVE", 37.10, 0.01, 1.0, 1.0, 100_000.0, 2, 0, ("Limit", "Market"), (MarketSession.REGULAR,), True),
    InstrumentDefinition("aaaaaaaa-0000-0000-0000-000000000201", "BOVA11", "Etf", "SPOT_EXTENDED_HOURS", "ACTIVE", 118.35, 0.01, 1.0, 1.0, 500_000.0, 2, 0, ("Limit", "Market"), (MarketSession.REGULAR, MarketSession.AFTER_MARKET)),
    InstrumentDefinition("aaaaaaaa-0000-0000-0000-000000000202", "SMAL11", "Etf", "SPOT_EXTENDED_HOURS", "HALTED", 102.20, 0.01, 1.0, 1.0, 500_000.0, 2, 0, ("Limit", "Market"), (MarketSession.REGULAR, MarketSession.AFTER_MARKET)),
    InstrumentDefinition("aaaaaaaa-0000-0000-0000-000000000203", "IVVB11", "Etf", "SPOT_EXTENDED_HOURS", "ACTIVE", 315.50, 0.01, 1.0, 1.0, 500_000.0, 2, 0, ("Limit", "Market"), (MarketSession.REGULAR, MarketSession.AFTER_MARKET)),
    InstrumentDefinition("aaaaaaaa-0000-0000-0000-000000000301", "AAPL34", "Bdr", "SPOT_EXTENDED_HOURS", "AFTER_MARKET_ONLY", 52.10, 0.01, 1.0, 1.0, 500_000.0, 2, 0, ("Limit", "Market"), (MarketSession.REGULAR, MarketSession.AFTER_MARKET)),
    InstrumentDefinition("aaaaaaaa-0000-0000-0000-000000000302", "MSFT34", "Bdr", "SPOT_EXTENDED_HOURS", "ACTIVE", 61.70, 0.01, 1.0, 1.0, 500_000.0, 2, 0, ("Limit", "Market"), (MarketSession.REGULAR, MarketSession.AFTER_MARKET)),
    InstrumentDefinition("aaaaaaaa-0000-0000-0000-000000000303", "GOGL34", "Bdr", "SPOT_EXTENDED_HOURS", "AUCTION", 48.90, 0.01, 1.0, 1.0, 500_000.0, 2, 0, ("Limit", "Market"), (MarketSession.AUCTION,)),
    InstrumentDefinition("aaaaaaaa-0000-0000-0000-000000000401", "USD-BRL", "Fx", "SPOT_STANDARD", "ACTIVE", 5.14, 0.0001, 1.0, 1.0, 1_000_000.0, 4, 0, ("Limit", "Market"), (MarketSession.REGULAR,)),
    InstrumentDefinition("aaaaaaaa-0000-0000-0000-000000000501", "GOLD-SPOT", "Commodity", "DISABLED", "DISABLED", 245.35, 0.05, 0.001, 0.001, 10_000.0, 2, 3, ("Limit",), (MarketSession.CLOSED,)),
)


class InstrumentCatalog:
    def __init__(self, instruments: tuple[InstrumentDefinition, ...]) -> None:
        self._items = instruments
        self._by_symbol = {item.symbol.upper(): item for item in instruments}
        self._by_id = {item.instrument_id: item for item in instruments}

    @classmethod
    def default(cls) -> "InstrumentCatalog":
        return cls(DEFAULT_INSTRUMENTS)

    def get(self, symbol: str) -> InstrumentDefinition | None:
        return self._by_symbol.get(symbol.upper())

    def get_by_instrument_id(self, instrument_id: str) -> InstrumentDefinition | None:
        return self._by_id.get(instrument_id)

    def all(self) -> tuple[InstrumentDefinition, ...]:
        return self._items

    def filter(
        self,
        *,
        symbols: tuple[str, ...] | None = None,
        asset_class: str | None = None,
        asset_classes: tuple[str, ...] | None = None,
        book_mode: str | None = None,
        session: MarketSession | None = None,
    ) -> list[InstrumentDefinition]:
        requested_symbols = {item.upper() for item in symbols} if symbols else None
        requested_asset_classes = (
            {item.lower() for item in asset_classes}
            if asset_classes
            else {asset_class.lower()} if asset_class
            else None
        )
        requested_book_mode = book_mode.upper() if book_mode else None

        results: list[InstrumentDefinition] = []
        for item in self._items:
            if requested_symbols and item.symbol.upper() not in requested_symbols:
                continue
            if requested_asset_classes and item.asset_class.lower() not in requested_asset_classes:
                continue
            if requested_book_mode and item.book_mode.upper() != requested_book_mode:
                continue
            if session and not self._supports_session(item, session):
                continue
            results.append(item)
        return results

    def normalize_payload(self, payload: dict[str, object]) -> dict[str, object]:
        normalized = dict(payload)
        symbol = str(normalized.get("symbol", "")).upper()
        instrument_id = normalized.get("instrumentId")

        definition = None
        if instrument_id:
            definition = self.get_by_instrument_id(str(instrument_id))
        if definition is None and symbol:
            definition = self.get(symbol)

        if definition is not None:
            normalized["symbol"] = definition.symbol
            normalized["instrumentId"] = definition.instrument_id

        if "submittedAt" not in normalized:
            normalized["submittedAt"] = datetime.now(UTC).isoformat()

        return normalized

    def validate_payload(self, payload: dict[str, object], *, session: MarketSession) -> ValidationResult:
        normalized = self.normalize_payload(payload)
        definition = self.get(str(normalized["symbol"]))
        if definition is None:
            return ValidationResult(False, "Instrument was not found.")

        quantity = float(normalized["quantity"])
        price = normalized.get("price")
        order_type = str(normalized.get("type", "Limit"))

        if definition.book_mode == "DISABLED" or definition.status in {"HALTED", "SUSPENDED", "DISABLED"}:
            return ValidationResult(False, "Instrument is disabled for trading.")
        if definition.status == "AUCTION":
            return ValidationResult(False, "Auction placeholder mode is not available yet.")
        if definition.status == "AFTER_MARKET_ONLY" and session != MarketSession.AFTER_MARKET:
            return ValidationResult(False, "Instrument accepts orders only in after-market.")
        if session not in definition.allowed_sessions:
            return ValidationResult(False, f"Session {session.value} is not allowed for {definition.symbol}.")
        if order_type not in definition.allowed_order_types:
            return ValidationResult(False, f"Order type {order_type} is not allowed for {definition.symbol}.")
        if quantity < definition.min_quantity:
            return ValidationResult(False, "Quantity is below the instrument minimum.")
        if definition.max_quantity is not None and quantity > definition.max_quantity:
            return ValidationResult(False, "Quantity is above the instrument maximum.")
        if not _is_multiple(quantity, definition.lot_size, definition.quantity_precision):
            return ValidationResult(False, "Quantity does not respect lot size.")
        if not _has_precision(quantity, definition.quantity_precision):
            return ValidationResult(False, "Quantity precision is invalid.")
        if order_type == "Limit":
            if price is None:
                return ValidationResult(False, "Limit orders require a price.")
            if not _has_precision(float(price), definition.price_precision):
                return ValidationResult(False, "Price precision is invalid.")
            if not _is_multiple(float(price), definition.tick_size, definition.price_precision):
                return ValidationResult(False, "Price does not respect tick size.")

        return ValidationResult(True)

    def _supports_session(self, definition: InstrumentDefinition, session: MarketSession) -> bool:
        if definition.status == "AFTER_MARKET_ONLY":
            return session == MarketSession.AFTER_MARKET
        if definition.status in {"HALTED", "SUSPENDED", "DISABLED", "AUCTION"}:
            return False
        return session in definition.allowed_sessions


def quantize_step(value: float, step: float, precision: int) -> float:
    decimal_value = Decimal(str(value))
    decimal_step = Decimal(str(step))
    units = (decimal_value / decimal_step).quantize(Decimal("1"), rounding=ROUND_HALF_UP)
    quantized = units * decimal_step
    return float(quantized.quantize(Decimal(10) ** -precision))


def _has_precision(value: float, precision: int) -> bool:
    return quantize_step(value, 10 ** -precision if precision > 0 else 1.0, precision) == round(value, precision)


def _is_multiple(value: float, step: float, precision: int) -> bool:
    quantized = quantize_step(value, step, precision)
    return abs(quantized - value) < max(step / 1000, 1e-9)
