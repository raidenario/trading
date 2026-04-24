from __future__ import annotations

import os
import time
import unittest
from datetime import UTC, datetime

import httpx

from exchange_tooling.real_market_simulator import (
    HistoricalCandle,
    HistoricalReplayConfig,
    StaticHistoricalMarketDataProvider,
    run_real_market_simulation,
)


@unittest.skipUnless(
    os.environ.get("EXCHANGE_TOOLING_RUN_INTEGRATION") == "1",
    "set EXCHANGE_TOOLING_RUN_INTEGRATION=1 with Gateway, Kafka, Matching, Ledger and Query running",
)
class RealMarketSimulatorIntegrationTests(unittest.TestCase):
    def test_historical_candles_flow_through_full_exchange_stack(self) -> None:
        gateway_endpoint = os.environ.get("EXCHANGE_GATEWAY_ENDPOINT", "http://localhost:5103")
        query_endpoint = os.environ.get("EXCHANGE_QUERY_ENDPOINT", "http://localhost:5267")
        ledger_endpoint = os.environ.get("EXCHANGE_LEDGER_ENDPOINT", "http://localhost:5075")

        with httpx.Client(timeout=5.0) as client:
            self.assertEqual(200, client.get(f"{gateway_endpoint}/health").status_code)
            self.assertEqual(200, client.get(f"{query_endpoint}/health").status_code)
            self.assertEqual(200, client.get(f"{ledger_endpoint}/health").status_code)

        provider = StaticHistoricalMarketDataProvider(
            {
                "PETR4.SA": [
                    HistoricalCandle(
                        datetime(2023, 1, 2, 13, 0, tzinfo=UTC),
                        open=25.10,
                        high=25.80,
                        low=24.90,
                        close=25.50,
                        volume=80_000_000,
                    )
                ]
            }
        )

        summary = run_real_market_simulation(
            replay_config=HistoricalReplayConfig(
                symbols=("PETR4",),
                start="2023-01-01",
                end="2023-01-03",
                interval="1d",
                endpoint=gateway_endpoint,
                speed=0,
            ),
            provider=provider,
        )

        self.assertEqual(1, summary.candles_replayed)
        self.assertEqual(8, summary.orders_sent)
        self.assertEqual(0, summary.orders_failed)

        deadline = time.time() + 20
        trades: list[object] = []
        balances: list[object] = []
        with httpx.Client(timeout=5.0) as client:
            while time.time() < deadline:
                trades_response = client.get(f"{query_endpoint}/api/trades/recent?symbol=PETR4&limit=20")
                balances_response = client.get(
                    f"{ledger_endpoint}/api/ledger/accounts/11111111-1111-1111-1111-111111111111/balances"
                )
                trades = trades_response.json() if trades_response.status_code == 200 else []
                balances = balances_response.json() if balances_response.status_code == 200 else []
                if trades and balances:
                    break
                time.sleep(0.5)

        self.assertTrue(trades, "Query API did not project PETR4 trades from matching-events")
        self.assertTrue(balances, "Ledger did not expose balances after consuming order/trade events")


if __name__ == "__main__":
    unittest.main()
