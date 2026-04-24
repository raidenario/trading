# realtime-gateway

Serviço Elixir/Phoenix responsável exclusivamente por fanout realtime via WebSocket/Channels.

## O que existe nesta base

- `GET /health`
- `socket "/socket"`
- canal `market:SYMBOL`
- endpoint interno `POST /internal/events` para receber envelopes já consumidos pela Query API
- eventos realtime `ticker_update`, `trade_update`, `book_update` e `candle_update`
- módulo `RealtimeGateway.MarketEventRouter` para centralizar broadcasts

## Subscriptions

- tópico: `market:BTC-USD`
- eventos:
  - `ticker_update`
  - `trade_update`
  - `book_update`
  - `candle_update`

## Rodando

```bash
mix deps.get
mix phx.server
```

Por padrão o endpoint HTTP escuta em `http://localhost:4000`. Para trocar a porta:

```bash
PORT=4010 mix phx.server
```

No PowerShell:

```powershell
$env:PORT = "4010"
mix phx.server
```

O realtime-gateway nao consome Kafka diretamente no ambiente local Windows. A Query API, que já consome Kafka via .NET, encaminha os envelopes públicos para:

```text
POST /internal/events
```

Isso evita dependências NIF nativas no Elixir e mantém o Phoenix focado em fanout WebSocket.

Se a Query API registrar timeout ao encaminhar eventos, primeiro valide:

```bash
curl http://localhost:4000/health
```

Sem esse health check respondendo, nenhum evento encaminhado pela Query API chega ao WebSocket.

## Exemplo de fluxo

1. cliente conecta em `/socket`
2. cliente entra em `market:BTC-USD`
3. `matching-engine` publica `BookUpdated`/`TickerUpdated` em `marketdata-events`
4. `matching-engine` publica `TradeExecuted` em `matching-events`
5. `query-api` consome Kafka e encaminha o envelope para `POST /internal/events`
6. `realtime-gateway` normaliza o envelope e propaga no canal do símbolo

Os eventos enviados ao frontend usam payloads snake_case:

```text
TickerUpdated  -> market:SYMBOL / ticker_update
BookUpdated    -> market:SYMBOL / book_update
TradeExecuted  -> market:SYMBOL / trade_update
CandleUpdated  -> market:SYMBOL / candle_update
```

Os eventos de demonstração `demo:ticker` e `demo:trade` continuam disponíveis para testes manuais do canal.

## Papel arquitetural

Este serviço não faz validação financeira nem matching. Ele apenas distribui para muitos clientes eventos já aprovados e materializados pelos serviços centrais.
