# ledger-service

Stub inicial do serviço de ledger e saldos.

## Endpoints

- `GET /health`
- `GET /api/ledger/accounts/{accountId}`
- `GET /api/ledger/accounts/{accountId}/balances`

O foco desta base é deixar o domínio financeiro separado do matching hot path, com tipos próprios para contas, balances e ledger entries.
