from __future__ import annotations

import random
from dataclasses import dataclass

from .models import OrderRequest


DEMO_ACCOUNTS = (
    "11111111-1111-1111-1111-111111111111",
    "22222222-2222-2222-2222-222222222222",
    "33333333-3333-3333-3333-333333333333",
)

@dataclass(slots=True)
class OrderGenerator:
    symbols: tuple[str, ...] = ("BTC-USD", "ETH-USD", "SOL-USD")

    def next_order(self) -> OrderRequest:
        symbol = random.choice(self.symbols)
        side = random.choice(("Buy", "Sell"))
        quantity = round(random.uniform(0.01, 2.0), 4)
        reference_price = {
            "BTC-USD": 50_000.0,
            "ETH-USD": 3_500.0,
            "SOL-USD": 125.0,
        }[symbol]
        price = round(reference_price + random.uniform(-0.01, 0.01) * reference_price, 2)

        return OrderRequest.create(
            account_id=random.choice(DEMO_ACCOUNTS),
            symbol=symbol,
            side=side,
            quantity=quantity,
            price=price,
        )
