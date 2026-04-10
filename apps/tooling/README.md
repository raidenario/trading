# tooling

Ferramentas Python para desenvolvimento local, geração de carga e replay de ordens.

## Comandos disponíveis

```bash
python -m pip install -e apps/tooling
exchange-tooling fake-order --symbol BTC-USD
exchange-tooling load --endpoint http://localhost:8080 --rate 10 --count 100 --dry-run
exchange-tooling replay apps/tooling/samples/orders.jsonl --endpoint http://localhost:8080 --speed 2.0
exchange-tooling flow --asset-class Equity --session regular --dry-run
exchange-tooling flow --asset-class Bdr --session after-market --dry-run
exchange-tooling simulate --asset-class Etf
```

## Estrutura

- `fake-order`: gera um payload compatível com `CreateOrderCommand`
- `load`: produz fluxo sintético contínuo para o `gateway-api`
- `replay`: reenvia uma trilha JSONL preservando offsets temporais
- `flow`: respeita regras de tick, lote, quantidade mínima/máxima, tipos de ordem e sessão
- filtros disponíveis: `--symbol/--symbols`, `--asset-class`, `--book-mode`, `--session`

## Instrumentos e cenários

- O catálogo padrão agora inclui crypto spot, ações spot, ETF, BDR, FX spot simulado e commodity spot sintética.
- Os cenários de replay ficam em `apps/tooling/samples/`:
  - `crypto-spot.jsonl`
  - `equity-regular.jsonl`
  - `etf-trading.jsonl`
  - `bdr-after-market.jsonl`
  - `fx-spot.jsonl`
  - `commodity-disabled.jsonl`

## Formato do replay

Cada linha de `orders.jsonl` deve ter esta forma:

```json
{"offset_seconds":0.0,"order":{"orderId":"...","accountId":"...","symbol":"BTC-USD","side":"Buy","type":"Limit","quantity":0.1,"price":50000.0,"timeInForce":"Gtc","clientOrderId":null,"submittedAt":"2026-01-01T00:00:00Z"}}
```
