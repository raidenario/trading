"""
Continuous order flow generator that sends fake orders to the Gateway API.

This creates realistic-looking trading activity by generating buy/sell orders
from multiple simulated accounts across configured markets.
"""
from __future__ import annotations

import random
import time
from datetime import UTC, datetime
from uuid import uuid4

import httpx
from rich.console import Console
from rich.table import Table

console = Console()

DEMO_ACCOUNTS = [
    "11111111-1111-1111-1111-111111111111",
    "22222222-2222-2222-2222-222222222222",
    "33333333-3333-3333-3333-333333333333",
]


def generate_order_payload(
    symbol: str = "BTC-USD",
    reference_price: float = 50_000.0,
) -> dict:
    side = random.choice(["Buy", "Sell"])
    spread_pct = random.uniform(-0.005, 0.005)
    price = round(reference_price * (1 + spread_pct), 2)
    quantity = round(random.uniform(0.01, 1.5), 4)

    return {
        "orderId": str(uuid4()),
        "accountId": random.choice(DEMO_ACCOUNTS),
        "symbol": symbol,
        "side": side,
        "type": "Limit",
        "quantity": quantity,
        "price": price,
        "timeInForce": "Gtc",
        "clientOrderId": f"sim-{uuid4().hex[:8]}",
        "submittedAt": datetime.now(UTC).isoformat(),
        "schemaVersion": 1,
    }


MARKET_PRICES = {
    "BTC-USD": 50_000.0,
    "ETH-USD": 3_500.0,
    "SOL-USD": 125.0,
}


def run_order_flow(
    endpoint: str = "http://localhost:5103",
    symbols: list[str] | None = None,
    rate: float = 2.0,
    count: int | None = None,
    dry_run: bool = False,
):
    """
    Continuously send fake orders to the gateway API.

    Args:
        endpoint: Base URL of the gateway API.
        symbols: List of symbols to trade (defaults to all markets).
        rate: Orders per second.
        count: Total orders to send (None = infinite).
        dry_run: If True, print orders without sending.
    """
    symbols = symbols or list(MARKET_PRICES.keys())
    interval = 1.0 / rate if rate > 0 else 1.0
    sent = 0

    console.print(f"[bold green]Order Flow Generator[/bold green]")
    console.print(f"  Endpoint: {endpoint}")
    console.print(f"  Symbols:  {', '.join(symbols)}")
    console.print(f"  Rate:     {rate} orders/sec")
    console.print(f"  Count:    {'infinite' if count is None else count}")
    console.print(f"  Dry Run:  {dry_run}")
    console.print()

    client = httpx.Client(timeout=10.0) if not dry_run else None

    try:
        while count is None or sent < count:
            symbol = random.choice(symbols)
            ref_price = MARKET_PRICES.get(symbol, 100.0)
            payload = generate_order_payload(symbol, ref_price)

            if dry_run:
                console.print(f"[dim]{sent+1:>5}[/dim] [cyan]{payload['side']:>4}[/cyan] "
                              f"{payload['quantity']:>8.4f} {symbol} @ {payload['price']:>12.2f} "
                              f"[dim](account {payload['accountId'][:8]}...)[/dim]")
            else:
                try:
                    resp = client.post(f"{endpoint}/api/orders", json=payload)  # type: ignore
                    status_icon = "[green]OK[/green]" if resp.status_code < 400 else f"[red]{resp.status_code}[/red]"
                    console.print(f"[dim]{sent+1:>5}[/dim] {status_icon} "
                                  f"[cyan]{payload['side']:>4}[/cyan] "
                                  f"{payload['quantity']:>8.4f} {symbol} @ {payload['price']:>12.2f}")
                except httpx.RequestError as e:
                    console.print(f"[red]ERROR[/red] {e}")

            sent += 1
            time.sleep(interval)

    except KeyboardInterrupt:
        console.print(f"\n[yellow]Stopped after {sent} orders.[/yellow]")
    finally:
        if client:
            client.close()
