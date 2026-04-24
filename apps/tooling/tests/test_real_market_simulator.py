from __future__ import annotations

import unittest
from datetime import UTC, datetime, timedelta

from exchange_tooling.instruments import InstrumentCatalog, MarketSession
from exchange_tooling.real_market_simulator import (
    GatewaySubmissionResult,
    HistoricalCandle,
    HistoricalReplayConfig,
    InMemoryGatewayClient,
    StaticHistoricalMarketDataProvider,
    SyntheticOrderFactory,
    YFinanceHistoricalMarketDataProvider,
    build_default_symbol_mappings,
    build_real_market_configs,
    run_real_market_simulation,
)


class RealMarketSimulatorTests(unittest.TestCase):
    def test_default_symbol_mappings_only_target_known_instruments(self) -> None:
        catalog = InstrumentCatalog.default()

        mappings = build_default_symbol_mappings(catalog)

        self.assertGreaterEqual(len(mappings), 6)
        self.assertEqual("PETR4.SA", mappings["PETR4"].source_symbol)
        self.assertEqual("BTC-USD", mappings["BTC-USD"].source_symbol)
        self.assertTrue(all(catalog.get(symbol) is not None for symbol in mappings))

    def test_build_real_market_configs_filters_existing_runtime_instruments(self) -> None:
        catalog = InstrumentCatalog.default()

        configs = build_real_market_configs(
            catalog=catalog,
            symbols=("PETR4", "VALE3", "BTC-USD", "UNKNOWN"),
            session=MarketSession.REGULAR,
        )

        self.assertEqual(["PETR4", "VALE3", "BTC-USD"], [item.symbol for item in configs])
        self.assertEqual(["PETR4.SA", "VALE3.SA", "BTC-USD"], [item.source_symbol for item in configs])

    def test_build_real_market_configs_respects_after_market_only_status(self) -> None:
        catalog = InstrumentCatalog.default()

        regular_configs = build_real_market_configs(
            catalog=catalog,
            symbols=("AAPL34",),
            session=MarketSession.REGULAR,
        )
        after_market_configs = build_real_market_configs(
            catalog=catalog,
            symbols=("AAPL34",),
            session=MarketSession.AFTER_MARKET,
        )

        self.assertEqual([], regular_configs)
        self.assertEqual(["AAPL34"], [item.symbol for item in after_market_configs])

    def test_provider_adapter_normalizes_candle_order_and_values(self) -> None:
        started_at = datetime(2023, 1, 2, 13, 0, tzinfo=UTC)
        provider = StaticHistoricalMarketDataProvider(
            {
                "PETR4.SA": [
                    HistoricalCandle(started_at + timedelta(days=1), 11, 12, 10, 11.5, 2000),
                    HistoricalCandle(started_at, 10, 11, 9.5, 10.5, 1000),
                ]
            }
        )

        candles = provider.download("PETR4.SA", start="2023-01-01", end="2023-01-05", interval="1d")

        self.assertEqual([started_at, started_at + timedelta(days=1)], [item.timestamp for item in candles])
        self.assertEqual(10, candles[0].open)
        self.assertEqual(2000, candles[1].volume)

    def test_yfinance_provider_calls_downloader_and_normalizes_rows(self) -> None:
        calls: list[dict[str, object]] = []

        def fake_downloader(*args: object, **kwargs: object) -> list[dict[str, object]]:
            calls.append({"args": args, "kwargs": kwargs})
            return [
                {
                    "timestamp": "2023-01-02T13:00:00Z",
                    "Open": 25.10,
                    "High": 25.80,
                    "Low": 24.90,
                    "Close": 25.50,
                    "Volume": 80_000_000,
                }
            ]

        provider = YFinanceHistoricalMarketDataProvider(downloader=fake_downloader)

        candles = provider.download("PETR4.SA", start="2023-01-01", end="2023-01-03", interval="1d")

        self.assertEqual("PETR4.SA", calls[0]["args"][0])
        self.assertEqual("2023-01-01", calls[0]["kwargs"]["start"])
        self.assertEqual("2023-01-03", calls[0]["kwargs"]["end"])
        self.assertEqual("1d", calls[0]["kwargs"]["interval"])
        self.assertFalse(calls[0]["kwargs"]["auto_adjust"])
        self.assertEqual(1, len(candles))
        self.assertEqual(25.50, candles[0].close)

    def test_yfinance_provider_normalizes_single_ticker_multi_index_frame(self) -> None:
        import pandas as pd

        columns = pd.MultiIndex.from_tuples(
            [
                ("Open", "BTC-USD"),
                ("High", "BTC-USD"),
                ("Low", "BTC-USD"),
                ("Close", "BTC-USD"),
                ("Volume", "BTC-USD"),
            ]
        )
        frame = pd.DataFrame(
            [[16_500.0, 16_800.0, 16_200.0, 16_700.0, 20_000.0]],
            index=[pd.Timestamp("2023-01-02T00:00:00Z")],
            columns=columns,
        )

        provider = YFinanceHistoricalMarketDataProvider(downloader=lambda *_, **__: frame)

        candles = provider.download("BTC-USD", start="2023-01-01", end="2023-01-03", interval="1d")

        self.assertEqual(1, len(candles))
        self.assertEqual(16_500.0, candles[0].open)
        self.assertEqual(16_700.0, candles[0].close)

    def test_synthetic_order_factory_converts_candles_into_valid_crossing_orders(self) -> None:
        catalog = InstrumentCatalog.default()
        config = build_real_market_configs(catalog=catalog, symbols=("PETR4",))[0]
        candle = HistoricalCandle(
            timestamp=datetime(2023, 1, 2, 13, 0, tzinfo=UTC),
            open=25.10,
            high=25.80,
            low=24.90,
            close=25.50,
            volume=80_000_000,
        )

        orders = SyntheticOrderFactory(catalog=catalog).orders_from_candle(config, candle)

        self.assertEqual(8, len(orders))
        for maker, taker in zip(orders[0::2], orders[1::2], strict=True):
            self.assertNotEqual(maker.account_id, taker.account_id)
            self.assertEqual(maker.symbol, "PETR4")
            self.assertEqual(taker.symbol, "PETR4")
            self.assertEqual(maker.price, taker.price)
            self.assertNotEqual(maker.side, taker.side)
            self.assertEqual(0, maker.quantity % 100)
            self.assertTrue(catalog.validate_payload(maker.to_payload(), session=MarketSession.REGULAR).is_valid)
            self.assertTrue(catalog.validate_payload(taker.to_payload(), session=MarketSession.REGULAR).is_valid)

    def test_synthetic_order_factory_moves_daily_candle_timestamp_inside_regular_session(self) -> None:
        catalog = InstrumentCatalog.default()
        config = build_real_market_configs(catalog=catalog, symbols=("PETR4",), session=MarketSession.REGULAR)[0]
        candle = HistoricalCandle(
            timestamp=datetime(2023, 1, 2, 0, 0, tzinfo=UTC),
            open=25.10,
            high=25.80,
            low=24.90,
            close=25.50,
            volume=80_000_000,
        )

        order = SyntheticOrderFactory(catalog=catalog).orders_from_candle(config, candle)[0]

        self.assertEqual("2023-01-02T13:00:00Z", order.submitted_at)
        self.assertTrue(catalog.validate_payload(order.to_payload(), session=MarketSession.REGULAR).is_valid)

    def test_real_market_simulation_keeps_historical_submitted_at_by_default(self) -> None:
        provider = StaticHistoricalMarketDataProvider(
            {
                "PETR4.SA": [
                    HistoricalCandle(
                        datetime(2023, 1, 2, 0, 0, tzinfo=UTC),
                        open=25.10,
                        high=25.80,
                        low=24.90,
                        close=25.50,
                        volume=80_000_000,
                    )
                ]
            }
        )
        gateway = InMemoryGatewayClient()

        run_real_market_simulation(
            replay_config=HistoricalReplayConfig(
                symbols=("PETR4",),
                start="2023-01-01",
                end="2023-01-03",
                interval="1d",
                speed=0,
            ),
            provider=provider,
            gateway=gateway,
            sleep=lambda _: None,
        )

        self.assertEqual("2023-01-02T13:00:00Z", gateway.payloads[0]["submittedAt"])

    def test_real_market_simulation_can_compress_historical_time_into_replay_clock(self) -> None:
        provider = StaticHistoricalMarketDataProvider(
            {
                "PETR4.SA": [
                    HistoricalCandle(
                        datetime(2023, 1, 2, 0, 0, tzinfo=UTC),
                        open=25.10,
                        high=25.80,
                        low=24.90,
                        close=25.50,
                        volume=80_000_000,
                    ),
                    HistoricalCandle(
                        datetime(2023, 1, 3, 0, 0, tzinfo=UTC),
                        open=25.50,
                        high=26.10,
                        low=25.40,
                        close=25.90,
                        volume=82_000_000,
                    ),
                ]
            }
        )
        gateway = InMemoryGatewayClient()

        run_real_market_simulation(
            replay_config=HistoricalReplayConfig(
                symbols=("PETR4",),
                start="2023-01-01",
                end="2023-01-04",
                interval="1d",
                speed=0,
                replay_clock="compressed-now",
                replay_start="2026-04-24T13:00:00Z",
                replay_step_seconds=60,
            ),
            provider=provider,
            gateway=gateway,
            sleep=lambda _: None,
        )

        submitted_times = [payload["submittedAt"] for payload in gateway.payloads[0::8]]
        self.assertEqual(
            ["2026-04-24T13:00:00Z", "2026-04-24T13:01:00Z"],
            submitted_times,
        )

    def test_real_market_simulation_streams_orders_one_by_one_to_gateway(self) -> None:
        catalog = InstrumentCatalog.default()
        started_at = datetime(2023, 1, 2, 13, 0, tzinfo=UTC)
        provider = StaticHistoricalMarketDataProvider(
            {
                "PETR4.SA": [
                    HistoricalCandle(started_at, 25.10, 25.80, 24.90, 25.50, 80_000_000),
                ],
                "BTC-USD": [
                    HistoricalCandle(started_at, 16_500.00, 16_800.00, 16_200.00, 16_700.00, 20_000),
                ],
            }
        )
        gateway = InMemoryGatewayClient()
        config = HistoricalReplayConfig(
            symbols=("PETR4", "BTC-USD"),
            start="2023-01-01",
            end="2023-01-03",
            interval="1d",
            speed=0,
            dry_run=False,
        )

        summary = run_real_market_simulation(
            replay_config=config,
            provider=provider,
            gateway=gateway,
            catalog=catalog,
            sleep=lambda _: None,
        )

        self.assertEqual(2, summary.candles_replayed)
        self.assertEqual(16, summary.orders_sent)
        self.assertEqual(16, len(gateway.payloads))
        self.assertEqual(["PETR4"] * 8 + ["BTC-USD"] * 8, [str(item["symbol"]) for item in gateway.payloads])
        self.assertTrue(all("orderId" in payload for payload in gateway.payloads))
        self.assertTrue(all(payload["type"] == "Limit" for payload in gateway.payloads))

    def test_real_market_simulation_reports_progress_events(self) -> None:
        catalog = InstrumentCatalog.default()
        provider = StaticHistoricalMarketDataProvider(
            {
                "BTC-USD": [
                    HistoricalCandle(
                        datetime(2023, 1, 2, 13, 0, tzinfo=UTC),
                        open=16_500.0,
                        high=16_800.0,
                        low=16_200.0,
                        close=16_700.0,
                        volume=20_000,
                    )
                ]
            }
        )
        events: list[str] = []

        run_real_market_simulation(
            replay_config=HistoricalReplayConfig(
                symbols=("BTC-USD",),
                start="2023-01-01",
                end="2023-01-03",
                interval="1d",
                speed=0,
                dry_run=True,
            ),
            provider=provider,
            gateway=InMemoryGatewayClient(),
            catalog=catalog,
            sleep=lambda _: None,
            progress=lambda event, _: events.append(event),
        )

        self.assertIn("symbol_start", events)
        self.assertIn("candles_loaded", events)
        self.assertIn("candle_start", events)
        self.assertEqual(8, events.count("order_sent"))
        self.assertIn("symbol_finished", events)

    def test_real_market_simulation_reports_gateway_rejection_reason(self) -> None:
        class RejectingGateway:
            def submit_order(self, payload: dict[str, object]) -> GatewaySubmissionResult:
                return GatewaySubmissionResult(False, "Instrument is currently outside of an allowed trading session.")

        provider = StaticHistoricalMarketDataProvider(
            {
                "PETR4.SA": [
                    HistoricalCandle(
                        datetime(2023, 1, 2, 0, 0, tzinfo=UTC),
                        open=25.10,
                        high=25.80,
                        low=24.90,
                        close=25.50,
                        volume=80_000_000,
                    )
                ]
            }
        )
        reasons: list[object] = []

        summary = run_real_market_simulation(
            replay_config=HistoricalReplayConfig(
                symbols=("PETR4",),
                start="2023-01-01",
                end="2023-01-03",
                interval="1d",
                speed=0,
            ),
            provider=provider,
            gateway=RejectingGateway(),
            sleep=lambda _: None,
            progress=lambda event, payload: reasons.append(payload.get("reason")) if event == "order_failed" else None,
        )

        self.assertEqual(0, summary.orders_sent)
        self.assertEqual(8, summary.orders_failed)
        self.assertEqual({"Instrument is currently outside of an allowed trading session."}, set(reasons))


if __name__ == "__main__":
    unittest.main()
