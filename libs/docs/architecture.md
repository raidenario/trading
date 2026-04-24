# Exchange Platform Architecture

## Current Architecture

This monorepo is an exchange / broker simulator with a CQRS and event-driven split:

- `gateway-api` receives account and order commands and now enriches orders with `instrument_id` and `trading_account_id` while still accepting `symbol`-based requests.
- `matching-engine` keeps in-memory books keyed by symbol, preserves FIFO matching behavior, and now accepts/publishes optional B3-style identifiers.
- `ledger-service` keeps the current reserve/debit/credit flow functionally equivalent and now records richer ledger entries plus passive positions.
- `query-api` still serves the existing history/ticker/balance views and now also projects instruments, enriched orders, enriched trades, and positions.
- `realtime-gateway` continues broadcasting market data without topic changes.
- Kafka topics remain unchanged: `order-commands`, `matching-events`, `ledger-events`, `marketdata-events`, `account-events`.

## B3-Inspired Core Model Added Now

The codebase now includes the reference and post-trade preparation objects required for a future B3-like structure:

- `Instrument`: tradable reference data with market/segment/precision/tick/lot metadata.
- `Participant`: broker/member abstraction for future exchange participant modeling.
- `TradingAccount`: trading relationship linked to the current `account_id`.
- `Order`: enriched with `instrument_id`, `trading_account_id`, `source_system`, and execution-instruction placeholders.
- `TradeExecution`: explicit execution record separated from future clearing/settlement processing.
- `TradeAllocation`: explicit allocation model, even though allocation is still 1:1 today.
- `Position`: persisted/projection-ready position concept separated from balances.
- `LedgerEntry`: richer audit shape with bucket, direction, reference type, and metadata.
- Future passive placeholders: `settlement_obligations`, `settlement_batches`, `netting_sets`, `clearing_sessions`, `risk_snapshots`, `custody_movements`.

## What Is Implemented Now

- Default crypto instruments for `BTC-USD`, `ETH-USD`, and `SOL-USD`.
- Default participant plus one default trading account per seeded account, with runtime provisioning for newly created accounts.
- Gateway/API application-layer enrichment from `symbol` and `account_id`.
- Matching engine compatibility for old and new order payload shapes.
- Trade events carrying instrument and trading-account identifiers.
- Query-side projections for instruments, positions, enriched orders, and enriched trades.
- Ledger-side position projection and richer audit entries without changing the existing balance effect of trades.
- Incremental PostgreSQL migration `002_b3_core_model.sql` with backfill/placeholder schema.

## What Is Intentionally Postponed

The following are modeled only as passive preparation structures. No active workflow has been attached:

- final settlement workflows
- risk management and exposure controls
- collateral / margin processing
- clearinghouse guarantees
- netting or gross settlement engines
- custody / depository transfers
- liquidation instruction execution
- participant default handling
- corporate actions
- derivatives lifecycle settlement

## Compatibility Notes

- Existing order submission still accepts the old request contract shape.
- Existing matching behavior remains symbol-driven and FIFO.
- Existing market data and realtime flows remain unchanged.
- Existing balance reservation and trade settlement behavior remain functionally equivalent.
- Existing read endpoints remain available; new projections are additive.
