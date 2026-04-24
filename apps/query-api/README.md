# query-api

API de leitura com read models iniciais para histórico, balances e market summary.

Também atua como ponte para o realtime local: depois de consumir eventos Kafka públicos, encaminha os envelopes para o `realtime-gateway` em `POST /internal/events`.

## Endpoints

- `GET /health`
- `GET /api/history/orders?accountId=...`
- `GET /api/balances/{accountId}`
- `GET /api/markets/{symbol}/ticker`
- `GET /api/markets/{symbol}/candles?interval=1m&limit=300`

## Realtime forwarding

Configurações:

- `RealtimeGateway:BaseUrl` - padrão `http://localhost:4000`
- `RealtimeGateway:Enabled` - padrão `true`

Tópicos encaminhados:

- `marketdata-events`
- `matching-events`

O realtime-gateway normaliza esses envelopes e publica em canais Phoenix `market:SYMBOL`.
