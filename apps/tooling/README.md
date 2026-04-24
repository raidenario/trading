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
exchange-tooling simulate-market --scenario expanded-market --endpoint http://localhost:8080 --rate 3
exchange-tooling real-market-simulator --symbols PETR4,BTC-USD --start 2023-01-01 --end 2023-01-31 --interval 1d --dry-run
exchange-tooling real-market-simulator --symbols PETR4 --start 2023-01-01 --end 2023-02-01 --interval 1d --replay-clock compressed-now --replay-step-seconds 60
```

## Estrutura

- `fake-order`: gera um payload compatível com `CreateOrderCommand`
- `load`: produz fluxo sintético contínuo para o `gateway-api`
- `replay`: reenvia uma trilha JSONL preservando offsets temporais
- `flow`: respeita regras de tick, lote, quantidade mínima/máxima, tipos de ordem e sessão
- `simulate-market`: envia fluxo de ordens para a API usando cenários prontos do mercado expandido
- `real-market-simulator`: baixa candles históricos via yfinance e transforma cada candle em ordens sintéticas enviadas ao Gateway API
- filtros disponíveis: `--symbol/--symbols`, `--asset-class`, `--book-mode`, `--session`

## Real market simulator

O `real-market-simulator` usa dados históricos reais como roteiro de mercado, mas mantém o sistema inteiro em modo simulado. O fluxo é:

```text
yfinance -> candles OHLCV -> ordens sintéticas -> Gateway API -> Kafka -> Matching Engine -> Ledger -> Query API
```

Ele nao publica candles diretamente no matching engine. O matching engine recebe apenas ordens compatíveis com `CreateOrderCommand`, preservando o caminho normal do sistema:

- Gateway valida símbolo, tick, lote, sessão e resolve `InstrumentId`/`TradingAccountId`.
- Gateway publica em `order-commands`.
- Matching Engine cruza as ordens e publica `TradeExecuted`, `BookUpdated` e `TickerUpdated`.
- Matching Engine agora também publica `CandleUpdated` usando o tempo efetivo do replay.
- Ledger consome ordens/trades e atualiza reservas, liquidações e posições.
- Query API projeta ordens, trades, ticker, candles e posições para consulta e frontend.

Exemplo com envio real para a stack local:

```bash
exchange-tooling real-market-simulator \
  --symbols PETR4,VALE3,BTC-USD \
  --start 2023-01-01 \
  --end 2023-02-01 \
  --interval 1d \
  --endpoint http://localhost:5103 \
  --replay-clock compressed-now \
  --replay-step-seconds 60 \
  --speed 0.25
```

Para testar sem enviar ordens:

```bash
exchange-tooling real-market-simulator --symbols PETR4 --start 2023-01-01 --end 2023-01-10 --dry-run
```

Se o comando parecer parado, rode primeiro com uma janela pequena ou com limite de candles:

```bash
exchange-tooling real-market-simulator --symbols PETR4 --start 2023-01-01 --end 2023-02-01 --max-candles-per-symbol 1 --dry-run
```

Um mês com 3 símbolos e `--speed 0.25` pode emitir centenas de ordens e levar alguns minutos. Sem `--dry-run`, o comando checa `GET /health` no Gateway antes de enviar ordens; se a stack nao estiver no ar, ele falha imediatamente com mensagem explícita.

Mapeamentos padrão de símbolos:

- `PETR4 -> PETR4.SA`
- `PETR4F -> PETR4.SA`
- `VALE3 -> VALE3.SA`
- `ITUB4 -> ITUB4.SA`
- `BOVA11 -> BOVA11.SA`
- `IVVB11 -> IVVB11.SA`
- `AAPL34 -> AAPL34.SA`
- `MSFT34 -> MSFT34.SA`
- `BTC-USD -> BTC-USD`
- `ETH-USD -> ETH-USD`
- `SOL-USD -> SOL-USD`
- `USD-BRL -> BRL=X`

Cada candle gera uma trajetória intraday sintética com quatro pontos:

```text
alta:  open -> low -> high -> close
baixa: open -> high -> low -> close
```

Cada ponto vira um par maker/taker de ordens limitadas no mesmo preço. Isso força cruzamentos no matching engine, gerando trades reais dentro da plataforma. O volume do candle é escalado para caber no ambiente local e respeita `tick_size`, `lot_size`, precisão e quantidade mínima do catálogo de instrumentos.

Observações:

- Para 2023, prefira `--interval 1d`; dados intraday antigos do Yahoo podem nao estar disponíveis.
- O modo padrão `--replay-clock historical` preserva a data original do candle.
- Para alimentar frontend e candles intraday em demo, prefira `--replay-clock compressed-now`, que comprime candles históricas em uma linha do tempo atual.
- Para exercitar o fluxo integrado em teste automatizado, suba Gateway, Kafka, Matching, Ledger e Query e rode:

```bash
set EXCHANGE_TOOLING_RUN_INTEGRATION=1
python -m unittest apps.tooling.tests.test_real_market_simulator_integration
```

## Instrumentos e cenários

- O catálogo padrão agora inclui crypto spot, ações spot, ETF, BDR, FX spot simulado e commodity spot sintética.
- O comando `simulate-market` inclui cenários prontos:
  - `expanded-market`
  - `equities`
  - `etf`
  - `bdr`
  - `fx`
  - `crypto`
- A sessão padrão depende do cenário. Exemplo:
  - `expanded-market`, `equities`, `etf`, `fx`, `crypto` usam `REGULAR`
  - `bdr` usa `AFTER_MARKET`
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
