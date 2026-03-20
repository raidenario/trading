from __future__ import annotations

from dataclasses import asdict, dataclass
from datetime import UTC, datetime
from uuid import uuid4


@dataclass(slots=True)
class OrderRequest:
    order_id: str
    account_id: str
    symbol: str
    side: str
    order_type: str
    quantity: float
    price: float | None
    time_in_force: str
    submitted_at: str
    client_order_id: str | None = None

    @classmethod
    def create(
        cls,
        account_id: str,
        symbol: str,
        side: str,
        quantity: float,
        price: float | None,
        order_type: str = "Limit",
        time_in_force: str = "Gtc",
    ) -> "OrderRequest":
        return cls(
            order_id=str(uuid4()),
            account_id=account_id,
            symbol=symbol.upper(),
            side=side,
            order_type=order_type,
            quantity=quantity,
            price=price,
            time_in_force=time_in_force,
            submitted_at=datetime.now(UTC).isoformat(),
        )

    def to_payload(self) -> dict[str, object]:
        payload = asdict(self)
        payload["side"] = self.side.capitalize()
        payload["type"] = self.order_type.capitalize()
        payload["timeInForce"] = self.time_in_force.capitalize()
        payload["orderId"] = payload.pop("order_id")
        payload["accountId"] = payload.pop("account_id")
        payload["clientOrderId"] = payload.pop("client_order_id")
        payload["submittedAt"] = payload.pop("submitted_at")
        return payload


@dataclass(slots=True)
class ReplayEntry:
    offset_seconds: float
    order: OrderRequest
