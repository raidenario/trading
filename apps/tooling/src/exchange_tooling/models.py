from __future__ import annotations

from dataclasses import asdict, dataclass
from datetime import UTC, datetime
from uuid import uuid4


@dataclass(slots=True)
class OrderRequest:
    order_id: str
    account_id: str
    symbol: str
    instrument_id: str | None
    side: str
    order_type: str
    quantity: float
    price: float | None
    time_in_force: str
    submitted_at: str
    client_order_id: str | None = None
    execution_instructions: dict[str, str] | None = None

    @classmethod
    def create(
        cls,
        account_id: str,
        symbol: str,
        instrument_id: str | None,
        side: str,
        quantity: float,
        price: float | None,
        order_type: str = "Limit",
        time_in_force: str = "Gtc",
        client_order_suffix: str | None = None,
        execution_instructions: dict[str, str] | None = None,
    ) -> "OrderRequest":
        return cls(
            order_id=str(uuid4()),
            account_id=account_id,
            symbol=symbol.upper(),
            instrument_id=instrument_id,
            side=side,
            order_type=order_type,
            quantity=quantity,
            price=price,
            time_in_force=time_in_force,
            submitted_at=datetime.now(UTC).isoformat(),
            client_order_id=f"sim-{client_order_suffix}-{uuid4().hex[:6]}" if client_order_suffix else f"sim-{uuid4().hex[:8]}",
            execution_instructions=execution_instructions,
        )

    def to_payload(self) -> dict[str, object]:
        payload = asdict(self)
        payload["side"] = self.side.capitalize()
        payload["type"] = self.order_type.capitalize()
        payload["timeInForce"] = self.time_in_force.capitalize()
        payload["orderId"] = payload.pop("order_id")
        payload["accountId"] = payload.pop("account_id")
        payload["instrumentId"] = payload.pop("instrument_id")
        payload["clientOrderId"] = payload.pop("client_order_id")
        payload["executionInstructions"] = payload.pop("execution_instructions")
        payload["submittedAt"] = payload.pop("submitted_at")
        return payload


@dataclass(slots=True)
class ReplayEntry:
    offset_seconds: float
    order: OrderRequest
