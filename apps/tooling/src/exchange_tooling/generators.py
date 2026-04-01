from __future__ import annotations

import random
from dataclasses import dataclass

from .models import OrderRequest


DEMO_ACCOUNTS = (
    "11111111-1111-1111-1111-111111111111",
    "22222222-2222-2222-2222-222222222222",
    "33333333-3333-3333-3333-333333333333",
)
REFERENCE_PRICES = {
    "BTC-USD": 50_000.0,
    "ETH-USD": 3_500.0,
    "SOL-USD": 125.0,
}


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
    symbols: tuple[str, ...] = ("BTC-USD", "ETH-USD", "SOL-USD")
    _rotation: int = 0

    def next_order(self) -> OrderRequest:
        symbol = random.choice(self.symbols)
        side = random.choice(("Buy", "Sell"))
        quantity = round(random.uniform(0.01, 2.0), 4)
        reference_price = REFERENCE_PRICES[symbol]
        price = round(reference_price + random.uniform(-0.01, 0.01) * reference_price, 2)

        return OrderRequest.create(
            account_id=random.choice(DEMO_ACCOUNTS),
            symbol=symbol,
            side=side,
            quantity=quantity,
            price=price,
        )

    def next_crossing_pair(self) -> tuple[OrderRequest, ...]:
        symbol = random.choice(self.symbols)
        reference_price = REFERENCE_PRICES[symbol]
        quantity = round(random.uniform(0.01, 0.35), 4)
        price = round(reference_price + random.uniform(-0.002, 0.002) * reference_price, 2)
        maker_side = random.choice(("Buy", "Sell"))

        maker = PARTICIPANTS[self._rotation % len(PARTICIPANTS)]
        taker = PARTICIPANTS[(self._rotation + 1) % len(PARTICIPANTS)]
        observer = PARTICIPANTS[(self._rotation + 2) % len(PARTICIPANTS)]
        self._rotation += 1

        maker_order = OrderRequest.create(
            account_id=maker.account_id,
            symbol=symbol,
            side=maker_side,
            quantity=quantity,
            price=price,
            client_order_suffix=f"{maker.name.lower().split()[0]}-maker")

        taker_order = OrderRequest.create(
            account_id=taker.account_id,
            symbol=symbol,
            side="Sell" if maker_side == "Buy" else "Buy",
            quantity=quantity,
            price=price,
            client_order_suffix=f"{taker.name.lower().split()[0]}-taker")

        # Injeta ocasionalmente uma ordem passiva do terceiro usuario para manter os tres participantes vivos no book.
        if random.random() < 0.35:
            observer_side = random.choice(("Buy", "Sell"))
            observer_price = round(reference_price + random.uniform(-0.004, 0.004) * reference_price, 2)
            observer_quantity = round(random.uniform(0.01, 0.25), 4)
            observer_order = OrderRequest.create(
                account_id=observer.account_id,
                symbol=symbol,
                side=observer_side,
                quantity=observer_quantity,
                price=observer_price,
                client_order_suffix=f"{observer.name.lower().split()[0]}-observer")
            return maker_order, observer_order, taker_order

        return maker_order, taker_order
