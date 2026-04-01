"""
Continuous order flow generator that sends fake orders to the Gateway API.

This creates realistic-looking trading activity by generating buy/sell orders
from multiple simulated accounts across configured markets.
"""
from __future__ import annotations

import time

import httpx
from rich.console import Console

from .generators import OrderGenerator, PARTICIPANTS, REFERENCE_PRICES

console = Console()

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
    symbols = symbols or list(REFERENCE_PRICES.keys())
    interval = 1.0 / rate if rate > 0 else 1.0
    sent = 0
    generator = OrderGenerator(symbols=tuple(symbols))
    account_map = {participant.account_id: participant.name for participant in PARTICIPANTS}

    console.print(f"[bold green]Order Flow Generator[/bold green]")
    console.print(f"  Endpoint: {endpoint}")
    console.print(f"  Symbols:  {', '.join(symbols)}")
    console.print(f"  Rate:     {rate} orders/sec")
    console.print(f"  Count:    {'infinite' if count is None else count}")
    console.print(f"  Dry Run:  {dry_run}")
    console.print(f"  Accounts: {', '.join(f'{p.name}={p.account_id[:8]}' for p in PARTICIPANTS)}")
    console.print()

    client = httpx.Client(timeout=10.0) if not dry_run else None

    try:
        while count is None or sent < count:
            for order in generator.next_crossing_pair():
                if count is not None and sent >= count:
                    break

                payload = order.to_payload()
                owner = account_map.get(payload["accountId"], str(payload["accountId"])[:8])

                if dry_run:
                    console.print(
                        f"[dim]{sent+1:>5}[/dim] [cyan]{payload['side']:>4}[/cyan] "
                        f"{payload['quantity']:>8.4f} {payload['symbol']} @ {payload['price']:>12.2f} "
                        f"[dim]({owner} {str(payload['accountId'])[:8]}...)[/dim]")
                else:
                    try:
                        resp = client.post(f"{endpoint}/api/orders", json=payload)  # type: ignore
                        status_icon = "[green]OK[/green]" if resp.status_code < 400 else f"[red]{resp.status_code}[/red]"
                        console.print(
                            f"[dim]{sent+1:>5}[/dim] {status_icon} "
                            f"[cyan]{payload['side']:>4}[/cyan] "
                            f"{payload['quantity']:>8.4f} {payload['symbol']} @ {payload['price']:>12.2f} "
                            f"[dim]({owner})[/dim]")
                    except httpx.RequestError as e:
                        console.print(f"[red]ERROR[/red] {e}")

                sent += 1
                time.sleep(interval)

    except KeyboardInterrupt:
        console.print(f"\n[yellow]Stopped after {sent} orders.[/yellow]")
    finally:
        if client:
            client.close()
