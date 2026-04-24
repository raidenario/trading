from __future__ import annotations

import unittest

from exchange_tooling.cli import build_parser, parse_session
from exchange_tooling.instruments import MarketSession
from exchange_tooling.market_scenarios import get_market_scenario


class CliTests(unittest.TestCase):
    def test_parser_supports_simulate_market_command(self) -> None:
        parser = build_parser()

        args = parser.parse_args(
            [
                "simulate-market",
                "--scenario",
                "expanded-market",
                "--endpoint",
                "http://localhost:8080",
                "--rate",
                "3",
                "--count",
                "50",
                "--dry-run",
            ]
        )

        self.assertEqual("simulate-market", args.command)
        self.assertEqual("expanded-market", args.scenario)
        self.assertEqual("http://localhost:8080", args.endpoint)
        self.assertEqual(3.0, args.rate)
        self.assertEqual(50, args.count)
        self.assertTrue(args.dry_run)

    def test_parser_supports_real_market_simulator_command(self) -> None:
        parser = build_parser()

        args = parser.parse_args(
            [
                "real-market-simulator",
                "--symbols",
                "PETR4,BTC-USD",
                "--start",
                "2023-01-01",
                "--end",
                "2023-01-31",
                "--interval",
                "1d",
                "--endpoint",
                "http://localhost:5103",
                "--speed",
                "0",
                "--replay-clock",
                "compressed-now",
                "--replay-start",
                "2026-04-24T13:00:00Z",
                "--replay-step-seconds",
                "60",
                "--dry-run",
            ]
        )

        self.assertEqual("real-market-simulator", args.command)
        self.assertEqual("PETR4,BTC-USD", args.symbols)
        self.assertEqual("2023-01-01", args.start)
        self.assertEqual("2023-01-31", args.end)
        self.assertEqual("1d", args.interval)
        self.assertEqual("http://localhost:5103", args.endpoint)
        self.assertEqual(0, args.speed)
        self.assertEqual("compressed-now", args.replay_clock)
        self.assertEqual("2026-04-24T13:00:00Z", args.replay_start)
        self.assertEqual(60, args.replay_step_seconds)
        self.assertTrue(args.dry_run)

    def test_expanded_market_scenario_contains_new_runtime_instruments(self) -> None:
        scenario = get_market_scenario("expanded-market")

        self.assertEqual("expanded-market", scenario.name)
        self.assertEqual(MarketSession.REGULAR, scenario.session)
        self.assertIn("PETR4", scenario.symbols)
        self.assertIn("BOVA11", scenario.symbols)
        self.assertIn("MSFT34", scenario.symbols)
        self.assertIn("USD-BRL", scenario.symbols)
        self.assertIn("BTC-USD", scenario.symbols)

    def test_parse_session_defaults_to_regular_when_value_is_none(self) -> None:
        self.assertEqual(MarketSession.REGULAR, parse_session(None))


if __name__ == "__main__":
    unittest.main()
