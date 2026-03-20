# tooling

Ferramentas Python para desenvolvimento local, geração de carga e replay de ordens.

## Comandos disponíveis

```bash
python -m pip install -e apps/tooling
exchange-tooling fake-order --symbol BTC-USD
exchange-tooling load --endpoint http://localhost:8080 --rate 10 --count 100 --dry-run
exchange-tooling replay apps/tooling/samples/orders.jsonl --endpoint http://localhost:8080 --speed 2.0
```

## Estrutura

- `fake-order`: gera um payload compatível com `CreateOrderCommand`
- `load`: produz fluxo sintético contínuo para o `gateway-api`
- `replay`: reenvia uma trilha JSONL preservando offsets temporais

## Formato do replay

Cada linha de `orders.jsonl` deve ter esta forma:

```json
{"offset_seconds":0.0,"order":{"orderId":"...","accountId":"...","symbol":"BTC-USD","side":"Buy","type":"Limit","quantity":0.1,"price":50000.0,"timeInForce":"Gtc","clientOrderId":null,"submittedAt":"2026-01-01T00:00:00Z"}}
```
