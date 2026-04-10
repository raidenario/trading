from __future__ import annotations

import json
import unittest
from pathlib import Path

from exchange_tooling.instruments import InstrumentCatalog, MarketSession


class InstrumentCatalogSourceTests(unittest.TestCase):
    def test_catalog_can_be_loaded_from_json_fixture(self) -> None:
        fixture = [
            {
                "instrument_id": "fixture-001",
                "symbol": "TEST3",
                "asset_class": "Equity",
                "market": "BR_EQUITIES",
                "book_mode": "SPOT_STANDARD",
                "status": "ACTIVE",
                "base_price": 10.5,
                "tick_size": 0.01,
                "lot_size": 100.0,
                "min_quantity": 100.0,
                "max_quantity": 100000.0,
                "price_precision": 2,
                "quantity_precision": 0,
                "allowed_order_types": ["Limit", "Market"],
                "allowed_sessions": ["REGULAR"],
                "separate_book": False,
            }
        ]

        path = Path(__file__).parent / "instrument-fixture-test.json"
        path.write_text(json.dumps(fixture), encoding="utf-8")

        try:
            catalog = InstrumentCatalog.from_json(path)
            definition = catalog.get("TEST3")

            self.assertIsNotNone(definition)
            self.assertEqual("fixture-001", definition.instrument_id)
            self.assertEqual("BR_EQUITIES", definition.market)
        finally:
            path.unlink(missing_ok=True)

    def test_catalog_filters_by_market(self) -> None:
        catalog = InstrumentCatalog.default()

        equities = catalog.filter(market="BR_EQUITIES", session=MarketSession.REGULAR)
        crypto = catalog.filter(market="CRYPTO_SPOT", session=MarketSession.REGULAR)

        self.assertTrue(any(item.symbol == "PETR4" for item in equities))
        self.assertTrue(all(item.market == "BR_EQUITIES" for item in equities))
        self.assertTrue(any(item.symbol == "BTC-USD" for item in crypto))
        self.assertTrue(all(item.market == "CRYPTO_SPOT" for item in crypto))


if __name__ == "__main__":
    unittest.main()
