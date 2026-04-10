from __future__ import annotations

from dataclasses import dataclass

from .instruments import MarketSession


@dataclass(frozen=True, slots=True)
class MarketScenario:
    name: str
    description: str
    symbols: tuple[str, ...]
    session: MarketSession


SCENARIOS: dict[str, MarketScenario] = {
    "expanded-market": MarketScenario(
        name="expanded-market",
        description="Mercado expandido com crypto, equities, ETF, BDR, FX e commodity sintética.",
        symbols=("BTC-USD", "PETR4", "VALE3", "PETR4F", "BOVA11", "MSFT34", "USD-BRL"),
        session=MarketSession.REGULAR,
    ),
    "equities": MarketScenario(
        name="equities",
        description="Cenario de acoes spot e mercado fracionario.",
        symbols=("PETR4", "VALE3", "ITUB4", "PETR4F"),
        session=MarketSession.REGULAR,
    ),
    "etf": MarketScenario(
        name="etf",
        description="Cenario de ETFs com sessao estendida simplificada.",
        symbols=("BOVA11", "IVVB11"),
        session=MarketSession.REGULAR,
    ),
    "bdr": MarketScenario(
        name="bdr",
        description="Cenario de BDRs com after-market simplificado.",
        symbols=("AAPL34", "MSFT34"),
        session=MarketSession.AFTER_MARKET,
    ),
    "fx": MarketScenario(
        name="fx",
        description="Cenario de FX spot simulado.",
        symbols=("USD-BRL",),
        session=MarketSession.REGULAR,
    ),
    "crypto": MarketScenario(
        name="crypto",
        description="Cenario legado de crypto spot.",
        symbols=("BTC-USD", "ETH-USD", "SOL-USD"),
        session=MarketSession.REGULAR,
    ),
}


def get_market_scenario(name: str) -> MarketScenario:
    key = name.strip().lower()
    if key not in SCENARIOS:
        raise KeyError(f"Unknown market scenario '{name}'.")
    return SCENARIOS[key]
