from __future__ import annotations

import random
from dataclasses import dataclass

from .instruments import InstrumentCatalog, InstrumentDefinition, MarketSession, quantize_step
from .models import OrderRequest


DEMO_ACCOUNTS = (
    "11111111-1111-1111-1111-111111111111",
    "22222222-2222-2222-2222-222222222222",
    "33333333-3333-3333-3333-333333333333",
)
REFERENCE_PRICES = {item.symbol: item.base_price for item in InstrumentCatalog.default().all()}


@dataclass(frozen=True, slots=True)
class MarketParticipant:
    account_id: str
    name: str


PARTICIPANTS = (
    MarketParticipant(DEMO_ACCOUNTS[0], "Alice Trader"),
    MarketParticipant(DEMO_ACCOUNTS[1], "Bob Market"),
    MarketParticipant(DEMO_ACCOUNTS[2], "Charlie Whale"),
)

@dataclass(slots=True)
class OrderGenerator:
    catalog: InstrumentCatalog | None = None
    symbols: tuple[str, ...] | None = None
    asset_classes: tuple[str, ...] | None = None
    book_modes: tuple[str, ...] | None = None
    session: MarketSession = MarketSession.REGULAR
    _rotation: int = 0

    def __post_init__(self) -> None:
        self.catalog = self.catalog or InstrumentCatalog.default()

    def next_order(self) -> OrderRequest:
        instrument = random.choice(self._eligible_instruments())
        side = random.choice(("Buy", "Sell"))
        quantity = self._random_quantity(instrument)
        order_type = self._random_order_type(instrument)
        price = self._random_price(instrument) if order_type == "Limit" else None

        return OrderRequest.create(
            account_id=random.choice(DEMO_ACCOUNTS),
            symbol=instrument.symbol,
            instrument_id=instrument.instrument_id,
            side=side,
            quantity=quantity,
            price=price,
            order_type=order_type,
            execution_instructions=self._execution_instructions(instrument),
        )

    def next_crossing_pair(self) -> tuple[OrderRequest, ...]:
        instrument = random.choice([item for item in self._eligible_instruments() if "Limit" in item.allowed_order_types])
        quantity = self._random_quantity(instrument)
        price = self._random_price(instrument, drift=0.002)
        maker_side = random.choice(("Buy", "Sell"))

        maker = PARTICIPANTS[self._rotation % len(PARTICIPANTS)]
        taker = PARTICIPANTS[(self._rotation + 1) % len(PARTICIPANTS)]
        observer = PARTICIPANTS[(self._rotation + 2) % len(PARTICIPANTS)]
        self._rotation += 1

        maker_order = OrderRequest.create(
            account_id=maker.account_id,
            symbol=instrument.symbol,
            instrument_id=instrument.instrument_id,
            side=maker_side,
            quantity=quantity,
            price=price,
            client_order_suffix=f"{maker.name.lower().split()[0]}-maker",
            execution_instructions=self._execution_instructions(instrument))

        taker_order = OrderRequest.create(
            account_id=taker.account_id,
            symbol=instrument.symbol,
            instrument_id=instrument.instrument_id,
            side="Sell" if maker_side == "Buy" else "Buy",
            quantity=quantity,
            price=price,
            client_order_suffix=f"{taker.name.lower().split()[0]}-taker",
            execution_instructions=self._execution_instructions(instrument))

        # Injeta ocasionalmente uma ordem passiva do terceiro usuario para manter os tres participantes vivos no book.
        if random.random() < 0.35:
            observer_side = random.choice(("Buy", "Sell"))
            observer_price = self._random_price(instrument, drift=0.004)
            observer_quantity = self._random_quantity(instrument)
            observer_order = OrderRequest.create(
                account_id=observer.account_id,
                symbol=instrument.symbol,
                instrument_id=instrument.instrument_id,
                side=observer_side,
                quantity=observer_quantity,
                price=observer_price,
                client_order_suffix=f"{observer.name.lower().split()[0]}-observer",
                execution_instructions=self._execution_instructions(instrument))
            return maker_order, observer_order, taker_order

        return maker_order, taker_order

    def _eligible_instruments(self) -> list[InstrumentDefinition]:
        filters = self.catalog.filter(  # type: ignore[union-attr]
            symbols=self.symbols,
            asset_classes=self.asset_classes,
            session=self.session,
        )

        if self.book_modes:
            allowed_modes = {item.upper() for item in self.book_modes}
            filters = [item for item in filters if item.book_mode.upper() in allowed_modes]

        return filters or self.catalog.filter(symbols=("BTC-USD", "ETH-USD", "SOL-USD"), session=MarketSession.REGULAR)  # type: ignore[union-attr]

    @staticmethod
    def _random_order_type(instrument: InstrumentDefinition) -> str:
        if instrument.book_mode == "SPOT_FRACTIONAL" and "Market" in instrument.allowed_order_types and random.random() < 0.5:
            return "Market"
        return random.choice(instrument.allowed_order_types)

    @staticmethod
    def _random_quantity(instrument: InstrumentDefinition) -> float:
        lot_units = random.randint(1, 20)
        raw_quantity = instrument.min_quantity + instrument.lot_size * (lot_units - 1)
        if instrument.max_quantity is not None:
            raw_quantity = min(raw_quantity, instrument.max_quantity)
        return quantize_step(raw_quantity, instrument.lot_size, instrument.quantity_precision)

    @staticmethod
    def _random_price(instrument: InstrumentDefinition, drift: float = 0.01) -> float:
        base = instrument.base_price
        shifted = base + random.uniform(-drift, drift) * base
        return quantize_step(max(shifted, instrument.tick_size), instrument.tick_size, instrument.price_precision)

    @staticmethod
    def _execution_instructions(instrument: InstrumentDefinition) -> dict[str, str]:
        return {
            "bookProfile": instrument.book_mode.title().replace("_", ""),
            "matchingEnabled": "true" if instrument.book_mode != "DISABLED" else "false",
            "separateBook": "true" if instrument.separate_book else "false",
        }
