from __future__ import annotations

import unittest
from datetime import datetime

from exchange_tooling.generators import OrderGenerator
from exchange_tooling.instruments import InstrumentCatalog, MarketSession


class InstrumentCatalogTests(unittest.TestCase):
    def test_catalog_filters_by_asset_class_book_mode_and_session(self) -> None:
        catalog = InstrumentCatalog.default()

        equities = catalog.filter(asset_class="Equity")
        fractional = catalog.filter(book_mode="SPOT_FRACTIONAL")
        regular = catalog.filter(session=MarketSession.REGULAR)

        self.assertTrue(any(item.symbol == "PETR4" for item in equities))
        self.assertTrue(any(item.symbol == "PETR4F" for item in fractional))
        self.assertTrue(all(MarketSession.REGULAR in item.allowed_sessions for item in regular))

    def test_generator_produces_orders_that_respect_trading_rules(self) -> None:
        catalog = InstrumentCatalog.default()
        generator = OrderGenerator(catalog=catalog, symbols=("PETR4", "AAPL34", "BTC-USD", "PETR4F"))

        for _ in range(25):
            order = generator.next_order()
            definition = catalog.get(order.symbol)
            self.assertIsNotNone(definition)
            validation = catalog.validate_payload(order.to_payload(), session=MarketSession.REGULAR)

            self.assertTrue(validation.is_valid, validation.reason)
            if order.price is not None:
                self.assertEqual(round(order.price, definition.price_precision), order.price)

    def test_generator_respects_after_market_filter(self) -> None:
        catalog = InstrumentCatalog.default()
        after_market_generator = OrderGenerator(
            catalog=catalog,
            session=MarketSession.AFTER_MARKET,
            asset_classes=("Bdr",),
        )

        for _ in range(10):
            order = after_market_generator.next_order()
            self.assertIn(order.symbol, {"AAPL34", "MSFT34"})
            validation = catalog.validate_payload(order.to_payload(), session=MarketSession.AFTER_MARKET)
            self.assertTrue(validation.is_valid, validation.reason)

    def test_generator_stamps_submitted_at_inside_requested_session_window(self) -> None:
        catalog = InstrumentCatalog.default()

        regular_order = OrderGenerator(
            catalog=catalog,
            symbols=("PETR4",),
            session=MarketSession.REGULAR,
        ).next_order()
        regular_time = datetime.fromisoformat(regular_order.submitted_at.replace("Z", "+00:00")).time()

        after_market_order = OrderGenerator(
            catalog=catalog,
            symbols=("MSFT34",),
            session=MarketSession.AFTER_MARKET,
        ).next_order()
        after_market_time = datetime.fromisoformat(after_market_order.submitted_at.replace("Z", "+00:00")).time()

        self.assertGreaterEqual((regular_time.hour, regular_time.minute), (13, 0))
        self.assertLess((regular_time.hour, regular_time.minute), (20, 0))
        self.assertGreaterEqual((after_market_time.hour, after_market_time.minute), (20, 0))
        self.assertLess((after_market_time.hour, after_market_time.minute), (21, 30))


if __name__ == "__main__":
    unittest.main()
