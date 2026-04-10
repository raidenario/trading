from __future__ import annotations

import json
import time
from collections.abc import Iterator
from urllib import error, request

from .generators import OrderGenerator
from .instruments import InstrumentCatalog, MarketSession
from .models import OrderRequest


class LoadGenerator:
    def __init__(
        self,
        endpoint: str,
        rate_per_second: int,
        total_orders: int,
        *,
        symbols: tuple[str, ...] | None = None,
        asset_classes: tuple[str, ...] | None = None,
        book_modes: tuple[str, ...] | None = None,
        session: MarketSession = MarketSession.REGULAR,
    ) -> None:
        self.endpoint = endpoint.rstrip("/")
        self.rate_per_second = max(rate_per_second, 1)
        self.total_orders = max(total_orders, 1)
        self.generator = OrderGenerator(
            catalog=InstrumentCatalog.default(),
            symbols=symbols,
            asset_classes=asset_classes,
            book_modes=book_modes,
            session=session,
        )

    def generate(self) -> Iterator[OrderRequest]:
        for _ in range(self.total_orders):
            yield self.generator.next_order()

    def run(self, dry_run: bool = False) -> None:
        interval = 1 / self.rate_per_second

        for order in self.generate():
            payload = json.dumps(order.to_payload()).encode("utf-8")

            if dry_run:
                print(json.dumps(order.to_payload()))
            else:
                self._post(payload)

            time.sleep(interval)

    def _post(self, payload: bytes) -> None:
        req = request.Request(
            f"{self.endpoint}/api/orders",
            data=payload,
            headers={"Content-Type": "application/json"},
            method="POST",
        )
        try:
            with request.urlopen(req, timeout=5) as response:
                print(response.status, response.read().decode("utf-8"))
        except error.URLError as exc:
            print(f"request failed: {exc}")
