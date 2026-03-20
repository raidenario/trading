# realtime-gateway

Serviço Elixir/Phoenix responsável exclusivamente por fanout realtime via WebSocket/Channels.

## O que existe nesta base

- `GET /health`
- `socket "/socket"`
- canal `market:SYMBOL`
- eventos de demonstração `ticker_update` e `trade_update`
- módulo `RealtimeGateway.MarketEventRouter` para centralizar broadcasts

## Subscriptions

- tópico: `market:BTC-USD`
- eventos:
  - `ticker_update`
  - `trade_update`

## Rodando

```bash
mix deps.get
mix phx.server
```

## Exemplo de fluxo

1. cliente conecta em `/socket`
2. cliente entra em `market:BTC-USD`
3. cliente envia `demo:ticker` ou `demo:trade`
4. canal propaga o evento para todos os assinantes do símbolo

## Papel arquitetural

Este serviço não faz validação financeira nem matching. Ele apenas distribui para muitos clientes eventos já aprovados e materializados pelos serviços centrais.
