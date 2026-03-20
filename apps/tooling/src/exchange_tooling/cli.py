"""
Exchange Platform CLI - Unified command interface.

Usage:
    exchange-tooling fake-order [--symbol BTC-USD]
    exchange-tooling simulate   [--endpoint URL] [--rate N] [--symbols BTC-USD,ETH-USD]
    exchange-tooling flow       [--endpoint URL] [--rate N] [--count N] [--dry-run]
    exchange-tooling load       [--endpoint URL] [--rate N] [--count N] [--dry-run]
    exchange-tooling replay     PATH [--endpoint URL] [--speed N] [--dry-run]
"""
from __future__ import annotations

import argparse
import json
import sys
import time

from .generators import OrderGenerator
from .load_generator import LoadGenerator
from .replay import replay_file


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description="Exchange Platform Tooling - Market Simulator, Order Generator & Load Tester"
    )
    subparsers = parser.add_subparsers(dest="command", required=True)

    # fake-order: emit a single fake order payload
    fake_order = subparsers.add_parser("fake-order", help="Emit a single fake order payload")
    fake_order.add_argument("--symbol", default="BTC-USD")

    # simulate: run continuous market data simulation
    simulate = subparsers.add_parser("simulate", help="Run continuous market data simulation (tickers + candles)")
    simulate.add_argument("--interval", type=float, default=1.0, help="Seconds between ticks")
    simulate.add_argument("--candle-interval", type=int, default=60, help="Seconds per candle")
    simulate.add_argument("--symbols", default="BTC-USD,ETH-USD,SOL-USD", help="Comma-separated symbols")

    # flow: continuous order flow to API
    flow = subparsers.add_parser("flow", help="Send continuous fake orders to the Gateway API")
    flow.add_argument("--endpoint", default="http://localhost:5103")
    flow.add_argument("--rate", type=float, default=2.0, help="Orders per second")
    flow.add_argument("--count", type=int, default=None, help="Total orders (default: infinite)")
    flow.add_argument("--symbols", default="BTC-USD,ETH-USD,SOL-USD", help="Comma-separated symbols")
    flow.add_argument("--dry-run", action="store_true")

    # load: fixed-count burst
    load = subparsers.add_parser("load", help="Generate a fixed batch of synthetic orders")
    load.add_argument("--endpoint", default="http://localhost:5103")
    load.add_argument("--rate", type=int, default=5)
    load.add_argument("--count", type=int, default=25)
    load.add_argument("--dry-run", action="store_true")

    # replay
    replay = subparsers.add_parser("replay", help="Replay orders from a JSONL file")
    replay.add_argument("path")
    replay.add_argument("--endpoint", default="http://localhost:5103")
    replay.add_argument("--speed", type=float, default=1.0)
    replay.add_argument("--dry-run", action="store_true")

    return parser


def main() -> None:
    parser = build_parser()
    args = parser.parse_args()

    if args.command == "fake-order":
        generator = OrderGenerator(symbols=(args.symbol,))
        print(json.dumps(generator.next_order().to_payload(), indent=2))
        return

    if args.command == "simulate":
        from .market_simulator import PriceEngine, MarketConfig, DEFAULT_MARKETS
        from rich.console import Console
        from rich.live import Live
        from rich.table import Table

        console = Console()
        symbols = [s.strip().upper() for s in args.symbols.split(",")]
        configs = [m for m in DEFAULT_MARKETS if m.symbol in symbols]
        if not configs:
            configs = [MarketConfig(s, 100.0) for s in symbols]

        engines = [PriceEngine(config=c) for c in configs]
        candle_counter = 0

        console.print("[bold green]Market Simulator[/bold green]")
        console.print(f"  Symbols: {', '.join(symbols)}")
        console.print(f"  Tick interval: {args.interval}s")
        console.print(f"  Candle interval: {args.candle_interval}s")
        console.print()

        try:
            while True:
                table = Table(title="Live Market Data", show_header=True)
                table.add_column("Symbol", style="cyan", width=10)
                table.add_column("Last Price", justify="right", style="bold")
                table.add_column("Bid", justify="right", style="green")
                table.add_column("Ask", justify="right", style="red")
                table.add_column("24h Change", justify="right")
                table.add_column("Volume", justify="right")

                for engine in engines:
                    ticker = engine.tick()
                    change_color = "green" if ticker.change_24h >= 0 else "red"
                    table.add_row(
                        ticker.symbol,
                        f"{ticker.last_price:,.2f}",
                        f"{ticker.best_bid:,.2f}",
                        f"{ticker.best_ask:,.2f}",
                        f"[{change_color}]{ticker.change_percent_24h:+.2f}%[/{change_color}]",
                        f"{ticker.volume_24h:,.2f}",
                    )

                console.clear()
                console.print(table)

                candle_counter += 1
                if candle_counter >= args.candle_interval / args.interval:
                    console.print("\n[dim]--- Candle Close ---[/dim]")
                    for engine in engines:
                        candle = engine.close_candle("1m")
                        console.print(f"  {candle.symbol}: O={candle.open:.2f} H={candle.high:.2f} L={candle.low:.2f} C={candle.close:.2f} V={candle.volume:.2f}")
                    candle_counter = 0

                time.sleep(args.interval)

        except KeyboardInterrupt:
            console.print("\n[yellow]Simulator stopped.[/yellow]")
        return

    if args.command == "flow":
        from .order_flow import run_order_flow
        symbols = [s.strip().upper() for s in args.symbols.split(",")]
        run_order_flow(
            endpoint=args.endpoint,
            symbols=symbols,
            rate=args.rate,
            count=args.count,
            dry_run=args.dry_run,
        )
        return

    if args.command == "load":
        LoadGenerator(args.endpoint, args.rate, args.count).run(dry_run=args.dry_run)
        return

    if args.command == "replay":
        replay_file(args.path, args.endpoint, speed=args.speed, dry_run=args.dry_run)
        return


if __name__ == "__main__":
    main()
