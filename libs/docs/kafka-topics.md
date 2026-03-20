# Exchange Platform — Kafka Topics

Este documento detalha os tópicos Kafka usados na plataforma e o papel de cada um.

## Visão Geral

```
┌────────────────┐     ┌────────────────────┐     ┌─────────────────┐
│  Producers     │────▶│   Kafka Topics     │────▶│  Consumers      │
└────────────────┘     └────────────────────┘     └─────────────────┘
```

## Tópicos

### `order-commands` (3 partições)

- **Produtor:** `gateway-api`
- **Consumidores:** `matching-engine`
- **Conteúdo:** Comandos de criação e cancelamento de ordens
- **Particionamento:** Por `symbol` (garante ordens do mesmo par no mesmo partition)
- **Payload exemplo:**
```json
{
  "type": "CreateOrderCommand",
  "orderId": "uuid",
  "accountId": "uuid",
  "symbol": "BTC-USD",
  "side": "Buy",
  "orderType": "Limit",
  "quantity": 0.5,
  "price": 50000.00,
  "timeInForce": "Gtc",
  "submittedAt": "2026-03-20T10:00:00Z"
}
```

### `matching-events` (3 partições)

- **Produtor:** `matching-engine`
- **Consumidores:** `ledger-service`, `query-api`, `realtime-gateway`
- **Conteúdo:** Resultados do matching — trades executados e atualizações do book
- **Eventos:** `TradeExecuted`, `BookUpdated`, `OrderFilled`, `OrderPartiallyFilled`

### `ledger-events` (3 partições)

- **Produtor:** `ledger-service`
- **Consumidores:** `query-api`
- **Conteúdo:** Mudanças de saldo, reservas de fundos, settlements
- **Eventos:** `FundsReserved`, `AccountFunded`

### `marketdata-events` (3 partições)

- **Produtor:** `matching-engine`
- **Consumidores:** `realtime-gateway`, `query-api`
- **Conteúdo:** Atualizações de preço e dados de mercado
- **Eventos:** `TickerUpdated`, `CandleUpdated`

### `account-events` (2 partições)

- **Produtor:** `gateway-api`
- **Consumidores:** `ledger-service`, `query-api`
- **Conteúdo:** Eventos de ciclo de vida de conta
- **Eventos:** `AccountCreated`, `AccountFunded`
