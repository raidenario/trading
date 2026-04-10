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
from .instruments import InstrumentCatalog, MarketSession
from .load_generator import LoadGenerator
from .market_scenarios import get_market_scenario
from .market_simulator import build_market_configs, run_market_simulation
from .replay import replay_file


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description="Exchange Platform Tooling - Market Simulator, Order Generator & Load Tester"
    )
    subparsers = parser.add_subparsers(dest="command", required=True)

    # fake-order: emit a single fake order payload
    fake_order = subparsers.add_parser("fake-order", help="Emit a single fake order payload")
    fake_order.add_argument("--symbol")
    fake_order.add_argument("--asset-class", dest="asset_class")
    fake_order.add_argument("--market")
    fake_order.add_argument("--book-mode")
    fake_order.add_argument("--session", choices=["regular", "after-market", "auction", "closed"], default="regular")

    # simulate: run continuous market data simulation
    simulate = subparsers.add_parser("simulate", help="Run continuous market data simulation (tickers + candles)")
    simulate.add_argument("--interval", type=float, default=1.0, help="Seconds between ticks")
    simulate.add_argument("--candle-interval", type=int, default=60, help="Seconds per candle")
    simulate.add_argument("--symbols", help="Comma-separated symbols")
    simulate.add_argument("--asset-class", dest="asset_class")
    simulate.add_argument("--market")
    simulate.add_argument("--book-mode")
    simulate.add_argument("--session", choices=["regular", "after-market", "auction", "closed"], default="regular")

    simulate_market = subparsers.add_parser("simulate-market", help="Run a preset market simulation with the expanded instrument catalog")
    simulate_market.add_argument("--scenario", choices=["expanded-market", "equities", "etf", "bdr", "fx", "crypto"], default="expanded-market")
    simulate_market.add_argument("--endpoint", default="http://localhost:5103")
    simulate_market.add_argument("--rate", type=float, default=2.0, help="Orders per second")
    simulate_market.add_argument("--count", type=int, default=None, help="Total orders (default: infinite)")
    simulate_market.add_argument("--session", choices=["regular", "after-market", "auction", "closed"], default=None)
    simulate_market.add_argument("--dry-run", action="store_true")

    # flow: continuous order flow to API
    flow = subparsers.add_parser("flow", help="Send continuous fake orders to the Gateway API")
    flow.add_argument("--endpoint", default="http://localhost:5103")
    flow.add_argument("--rate", type=float, default=2.0, help="Orders per second")
    flow.add_argument("--count", type=int, default=None, help="Total orders (default: infinite)")
    flow.add_argument("--symbols", help="Comma-separated symbols")
    flow.add_argument("--asset-class", dest="asset_class")
    flow.add_argument("--market")
    flow.add_argument("--book-mode")
    flow.add_argument("--session", choices=["regular", "after-market", "auction", "closed"], default="regular")
    flow.add_argument("--dry-run", action="store_true")

    # load: fixed-count burst
    load = subparsers.add_parser("load", help="Generate a fixed batch of synthetic orders")
    load.add_argument("--endpoint", default="http://localhost:5103")
    load.add_argument("--rate", type=int, default=5)
    load.add_argument("--count", type=int, default=25)
    load.add_argument("--symbols", help="Comma-separated symbols")
    load.add_argument("--asset-class", dest="asset_class")
    load.add_argument("--market")
    load.add_argument("--book-mode")
    load.add_argument("--session", choices=["regular", "after-market", "auction", "closed"], default="regular")
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
    session = parse_session(getattr(args, "session", "regular"))
    legacy_symbols = ("BTC-USD", "ETH-USD", "SOL-USD")

    if args.command == "fake-order":
        catalog = InstrumentCatalog.default()
        symbols = parse_csv(args.symbol) if args.symbol else tuple(["BTC-USD"]) if not args.asset_class and not args.book_mode else None
        generator = OrderGenerator(
            catalog=catalog,
            symbols=symbols,
            asset_classes=parse_csv(args.asset_class),
            markets=parse_csv(args.market),
            book_modes=parse_csv(args.book_mode),
            session=session,
        )
        print(json.dumps(generator.next_order().to_payload(), indent=2))
        return

    if args.command == "simulate":
        requested_symbols = parse_csv(args.symbols) or (legacy_symbols if not args.asset_class and not args.book_mode else None)
        configs = build_market_configs(
            symbols=requested_symbols,
            asset_classes=parse_csv(args.asset_class),
            markets=parse_csv(args.market),
            book_mode=args.book_mode,
            session=session,
        )
        run_market_simulation(
            configs,
            interval=args.interval,
            candle_interval=args.candle_interval,
            title="Market Simulator",
        )
        return

    if args.command == "simulate-market":
        from .order_flow import run_order_flow
        scenario = get_market_scenario(args.scenario)
        scenario_session = parse_session(args.session) if args.session else scenario.session
        run_order_flow(
            endpoint=args.endpoint,
            symbols=list(scenario.symbols),
            session=scenario_session,
            rate=args.rate,
            count=args.count,
            dry_run=args.dry_run,
        )
        return

    if args.command == "flow":
        from .order_flow import run_order_flow
        requested_symbols = parse_csv(args.symbols) or (legacy_symbols if not args.asset_class and not args.book_mode else None)
        run_order_flow(
            endpoint=args.endpoint,
            symbols=list(requested_symbols) if requested_symbols else None,
            asset_classes=parse_csv(args.asset_class),
            markets=parse_csv(args.market),
            book_modes=parse_csv(args.book_mode),
            session=session,
            rate=args.rate,
            count=args.count,
            dry_run=args.dry_run,
        )
        return

    if args.command == "load":
        requested_symbols = parse_csv(args.symbols) or (legacy_symbols if not args.asset_class and not args.book_mode else None)
        LoadGenerator(
            args.endpoint,
            args.rate,
            args.count,
            symbols=requested_symbols,
            asset_classes=parse_csv(args.asset_class),
            markets=parse_csv(args.market),
            book_modes=parse_csv(args.book_mode),
            session=session,
        ).run(dry_run=args.dry_run)
        return

    if args.command == "replay":
        replay_file(args.path, args.endpoint, speed=args.speed, dry_run=args.dry_run)
        return


def parse_csv(value: str | None) -> tuple[str, ...] | None:
    if not value:
        return None
    return tuple(item.strip().upper() for item in value.split(",") if item.strip())


def parse_session(value: str | None) -> MarketSession:
    if not value:
        return MarketSession.REGULAR
    normalized = value.replace("-", "_").upper()
    return MarketSession[normalized]


if __name__ == "__main__":
    main()
