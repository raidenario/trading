# ADR 0002: Defer Active Clearing, Settlement, and Risk Workflows

## Status

Accepted

## Context

The platform currently has working flows for:

- account creation and funding
- order submission
- FIFO matching by symbol
- trade execution publication
- balance reservation and balance updates
- query-side projections and realtime market data

The next architectural goal is to move toward a B3-like post-trade model. However, implementing full clearing, netting, settlement, risk, and custody workflows in the same change would significantly increase blast radius and jeopardize the current end-to-end flow.

## Decision

Introduce the core domain and persistence structures now, but keep the following concerns passive and nullable:

- settlement obligations and batches
- netting sets
- clearing sessions
- risk snapshots
- custody movements

Do not attach active business processing to those objects in this phase.

## Consequences

Positive:

- preserves current order/matching/ledger behavior
- enables incremental migration toward explicit post-trade stages
- gives contracts and schemas stable identifiers for future services
- keeps topic names and service boundaries intact

Negative:

- some tables exist without active producers/consumers yet
- settlement and clearing invariants are still not enforced
- balances remain operationally simpler than a real exchange/clearing stack

## Follow-Up

Phase 2 should add:

- trade execution to allocation orchestration
- clearing-session lifecycle
- settlement-obligation generation
- risk snapshot production and consumption
- netting/gross settlement strategy implementation
- custody and depository movement workflows
