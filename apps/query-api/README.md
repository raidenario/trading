# query-api

API de leitura com read models iniciais para histórico, balances e market summary.

## Endpoints

- `GET /health`
- `GET /api/history/orders?accountId=...`
- `GET /api/balances/{accountId}`
- `GET /api/markets/{symbol}/ticker`

Nesta entrega os dados são stubs em memória para deixar a separação CQRS explícita desde o início.
