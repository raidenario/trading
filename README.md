# Exchange Platform

Plataforma de exchange simulada, poliglota e orientada a eventos. O repositório junta:

- entrada de ordens e contas via APIs .NET;
- matching FIFO em Rust;
- ledger e projeções de leitura em .NET;
- fanout realtime em Elixir/Phoenix;
- frontend React/TypeScript;
- tooling Python para carga, replay e simulação com dados históricos do yfinance.

O objetivo do projeto é estudar arquitetura de trading end-to-end com um fluxo realista:

```text
ordem -> gateway -> kafka -> matching -> ledger -> query api -> realtime/frontend
```

e também um fluxo de mercado histórico controlado:

```text
yfinance -> candles históricas -> ordens sintéticas -> stack inteira -> dashboard/realtime
```

## Estado Atual

O que já está funcional no código:

- `gateway-api` recebe ordens, funding e comandos básicos.
- `matching-engine` cruza ordens por símbolo em memória e publica eventos de domínio.
- `ledger-service` projeta saldos, reservas e posições.
- `query-api` materializa histórico, ticker, candles, posições e encaminha eventos públicos ao realtime.
- `realtime-gateway` distribui `ticker_update`, `trade_update`, `book_update` e `candle_update` via Phoenix Channels.
- `frontend` mostra watchlist, gráfico OHLC, trades, book, portfolio e order ticket.
- o gráfico do frontend agora carrega histórico uma vez e aplica candles realtime incrementalmente, com tipografia IBM Plex Sans.
- `tooling` já suporta `real-market-simulator` com replay clock histórico ou comprimido.

O que ainda não está implementado como produto:

- `quant-lab` ainda é scaffold de diretórios;
- clearing, settlement, risco e custody estão só preparados no modelo;
- não existe PnL institucional completo nem motor de risco pré-trade.

## Arquitetura

```text
                                     +----------------------+
                                     |  Frontend (React)    |
                                     |  watchlist/chart/ws  |
                                     +----------+-----------+
                                                |
                                 HTTP           | WebSocket
                                                v
 +---------------------+              +----------------------+
 | Gateway API (.NET)  |              | Realtime Gateway     |
 | orders/accounts     |              | Elixir + Phoenix     |
 +----------+----------+              +----------+-----------+
            |                                    ^
            | order-commands                     | POST /internal/events
            v                                    |
      +-------------------+              +-------+--------+
      | Kafka             |<-------------| Query API      |
      | commands/events   |              | read models    |
      +----+---------+----+              | HTTP + forward |
           |         ^                   +-------+--------+
           |         |                           ^
           |         | matching/market/ledger    |
           v         |                           |
 +-------------------+--------+          +-------+--------+
 | Matching Engine (Rust)     |          | Ledger Service  |
 | FIFO books + trade events  |--------->| balances/pos    |
 +----------------------------+          +-----------------+
             ^
             |
             | HTTP (tooling envia ordens)
             |
 +-----------+------------------+
 | Tooling Python               |
 | load/replay/real-market-sim  |
 +------------------------------+
```

### Fluxos principais

1. `frontend` ou `tooling` envia ordem para o `gateway-api`.
2. `gateway-api` valida, enriquece e publica em `order-commands`.
3. `matching-engine` consome, faz matching FIFO e publica:
   - `OrderAccepted` / `OrderRejected`
   - `TradeExecuted`
   - `BookUpdated`
   - `TickerUpdated`
   - `CandleUpdated`
4. `ledger-service` consome trades/eventos e projeta saldos e posições.
5. `query-api` consome os tópicos públicos, monta read models HTTP e encaminha eventos realtime para o `realtime-gateway`.
6. `realtime-gateway` empurra eventos por canal `market:SYMBOL`.
7. `frontend` carrega histórico via HTTP e faz append incremental via WebSocket.

## Stack por Serviço

| Serviço | Stack | Porta local | Papel |
|---|---|---:|---|
| `apps/gateway-api` | C# / ASP.NET | `5103` | entrada HTTP para ordens, contas e funding |
| `apps/query-api` | C# / ASP.NET | `5267` | read side, histórico, candles, ticker, posições |
| `apps/ledger-service` | C# / ASP.NET | `5075` | saldos, reservas, posição e `ledger-events` |
| `apps/matching-engine` | Rust | `7000` | order book FIFO e eventos de mercado |
| `apps/realtime-gateway` | Elixir / Phoenix | `4000` | WebSocket, channels `market:SYMBOL` |
| `apps/frontend` | React / TS / Vite | `3000` | dashboard operacional |
| `apps/tooling` | Python | n/a | carga, replay e simuladores |
| `apps/quant-lab` | Python (scaffold) | n/a | terreno preparado para analytics/quant |

Infra local:

| Componente | Porta |
|---|---:|
| PostgreSQL | `5432` |
| Redis | `6379` |
| Kafka | `9092` |
| Zookeeper | `2181` |

## Monorepo

```text
apps/
  frontend/           dashboard web
  gateway-api/        API de entrada
  ledger-service/     serviço de ledger e posições
  matching-engine/    matching FIFO em Rust
  quant-lab/          scaffold para analytics/quant
  query-api/          consultas, projeções e bridge realtime
  realtime-gateway/   Phoenix Channels
  tooling/            simuladores e replay
libs/
  contracts/          commands, events e schemas compartilhados
  docs/               arquitetura, schema, ADRs e tópicos Kafka
  dotnet/             bibliotecas .NET compartilhadas
infra/
  compose/            docker compose local
  docker/             Dockerfiles
  postgres/           migrations e seed
tests/
  dotnet/             regressão .NET
```

## Componentes em Detalhe

### 1. Gateway API

Responsável por:

- receber `CreateOrderCommand` e cancelamentos;
- expor endpoints de conta e saldo;
- validar request shape;
- enriquecer ordens com `instrument_id` e `trading_account_id`;
- publicar em `order-commands`.

Pontos de entrada:

- `GET /health`
- `POST /api/orders`
- `POST /api/orders/{orderId}/cancel`
- `GET /api/orders`
- `GET /api/orders/{orderId}`

Mais detalhes: [apps/gateway-api/README.md](apps/gateway-api/README.md)

### 2. Matching Engine

Responsável por:

- manter books por símbolo em memória;
- aplicar prioridade preço-tempo;
- gerar trades e market data;
- preservar `submittedAt`/tempo efetivo do replay quando presente;
- agregar `CandleUpdated` por janela de 1 minuto.

Mais detalhes: [apps/matching-engine/README.md](apps/matching-engine/README.md)

### 3. Ledger Service

Responsável por:

- reservas, débito/crédito e projeções de saldo;
- posição por instrumento/conta;
- emissão de `ledger-events`.

Mais detalhes: [apps/ledger-service/README.md](apps/ledger-service/README.md)

### 4. Query API

Responsável por:

- endpoints de leitura;
- histórico de ordens;
- ticker e candles históricas;
- posições e balances;
- bridge HTTP para o `realtime-gateway`.

Endpoints principais:

- `GET /health`
- `GET /api/history/orders?accountId=...`
- `GET /api/balances/{accountId}`
- `GET /api/markets/{symbol}/ticker`
- `GET /api/markets/{symbol}/candles?interval=1m&limit=300`

Mais detalhes: [apps/query-api/README.md](apps/query-api/README.md)

### 5. Realtime Gateway

Responsável por:

- aceitar envelopes públicos em `POST /internal/events`;
- transformar envelopes de domínio em eventos websocket;
- fanout por tópico `market:SYMBOL`.

Eventos publicados ao frontend:

- `ticker_update`
- `trade_update`
- `book_update`
- `candle_update`

Mais detalhes: [apps/realtime-gateway/README.md](apps/realtime-gateway/README.md)

### 6. Frontend

Responsável por:

- watchlist e market overview;
- gráfico de candles OHLC;
- order ticket;
- trades feed, order book, portfolio e event tape;
- merge de histórico HTTP com realtime websocket.

Mais detalhes: [apps/frontend/README.md](apps/frontend/README.md)

### 7. Tooling

Responsável por:

- geração de ordens fake;
- carga contínua;
- replay de JSONL;
- cenários multi-instrumento;
- `real-market-simulator` com candles do yfinance.

Mais detalhes: [apps/tooling/README.md](apps/tooling/README.md)

### 8. Quant Lab

Hoje o `quant-lab` ainda não entrega funcionalidades executáveis. A estrutura existe para receber:

- analytics de PnL e drawdown;
- fator de risco e exposição;
- backtests e métricas de estratégia;
- integração com o replay histórico do simulator.

Status documentado em: [apps/quant-lab/README.md](apps/quant-lab/README.md)

## Contratos e Eventos

Os contratos compartilhados vivem em `libs/contracts/`.

### Commands

- `CreateAccountCommand`
- `FundAccountCommand`
- `CreateOrderCommand`
- `CancelOrderCommand`

### Events

- `AccountCreated`
- `AccountFunded`
- `OrderAccepted`
- `OrderRejected`
- `TradeExecuted`
- `OrderFilled`
- `OrderPartiallyFilled`
- `FundsReserved`
- `BookUpdated`
- `TickerUpdated`
- `CandleUpdated`

## Kafka

| Tópico | Partições | Produtor | Consumidor principal |
|---|---:|---|---|
| `order-commands` | `3` | gateway-api | matching-engine |
| `matching-events` | `3` | matching-engine | ledger-service, query-api |
| `ledger-events` | `3` | ledger-service | query-api |
| `marketdata-events` | `3` | matching-engine | query-api |
| `account-events` | `2` | gateway-api | ledger-service, query-api |

Mais detalhes e payloads: [libs/docs/kafka-topics.md](libs/docs/kafka-topics.md)

## Banco de Dados e Modelo

O repositório já carrega um modelo inspirado em estruturas de mercado mais completas:

- `participants`
- `instruments`
- `trading_accounts`
- `trade_executions`
- `trade_allocations`
- `positions`

Também existem tabelas passivas, preparadas para fases futuras:

- `settlement_obligations`
- `settlement_batches`
- `netting_sets`
- `clearing_sessions`
- `risk_snapshots`
- `custody_movements`

Migrations principais:

- `001_init.sql`
- `002_b3_core_model.sql`
- `003_instrument_runtime_model.sql`

Mais detalhes: [libs/docs/schema.md](libs/docs/schema.md)

## Como Rodar

Há dois modos principais:

1. local, com cada serviço rodando no host;
2. Docker, com a stack inteira via `docker compose up`.

### Rodando Local no Windows

Script principal:

```powershell
.\start-local.bat
```

Ele:

- sobe Kafka/Postgres/Redis com Docker;
- inicia Gateway API, Query API, Ledger Service, Matching Engine e Realtime Gateway;
- abre cada serviço em uma janela separada.

Para abrir frontend em aba separada com Windows Terminal:

```powershell
.\start-local.cmd
```

### Rodando Local Manualmente

Pré-requisitos:

- .NET SDK
- Rust/Cargo
- Elixir/Mix
- Node.js 20+
- Python 3.11+
- Docker Desktop para a infra local

### Infra local

```powershell
docker compose -f infra/compose/docker-compose.yml up postgres redis zookeeper kafka kafka-init -d
```

### Backend local

```powershell
dotnet restore ExchangePlatform.slnx
dotnet run --project apps\gateway-api\src\Exchange.Gateway.Api
dotnet run --project apps\query-api\src\Exchange.Query.Api
dotnet run --project apps\ledger-service\src\Exchange.Ledger.Api
```

### Matching Engine local

```powershell
cd apps\matching-engine
cargo run --bin matching-engine-service
```

### Realtime Gateway local

```powershell
cd apps\realtime-gateway
mix deps.get
$env:PORT = "4000"
mix phx.server
```

### Frontend local

```powershell
cd apps\frontend
npm install
npm run dev
```

### Tooling local

```powershell
python -m pip install -e apps/tooling
```

### Rodando Via Docker

O compose agora sobe a stack principal inteira com as mesmas portas do modo local:

- frontend: `http://localhost:3000`
- gateway-api: `http://localhost:5103`
- query-api: `http://localhost:5267`
- ledger-service: `http://localhost:5075`
- realtime-gateway: `http://localhost:4000`
- Kafka: `localhost:9092` e `localhost:29092`

```powershell
.\start-trading-docker.cmd
```

No Ubuntu/Linux:

```bash
bash ./start-trading-docker-linux.cmd
```

Ou diretamente:

```bash
docker compose -f infra/compose/docker-compose.yml up --build -d
```

Para derrubar:

```bash
docker compose -f infra/compose/docker-compose.yml down
```

Observações do modo Docker:

- o frontend containerizado usa proxy interno para `gateway-api`, `query-api` e `/socket`;
- o realtime passa pelo `realtime-gateway` e chega ao browser via WebSocket na mesma origem do frontend;
- o `tooling` também foi containerizado, mas fica fora do `up` padrão em um profile separado.

### Tooling Via Docker

Para usar o tooling sem Python local:

```powershell
docker compose -f infra/compose/docker-compose.yml --profile tools run --rm tooling --help
```

Exemplo com replay real:

```powershell
docker compose -f infra/compose/docker-compose.yml --profile tools run --rm tooling real-market-simulator `
  --symbols PETR4,VALE3,BTC-USD `
  --start 2023-01-01 `
  --end 2023-02-01 `
  --interval 1d `
  --endpoint http://gateway-api:8080 `
  --replay-clock compressed-now `
  --replay-step-seconds 60 `
  --speed 0.25
```

## Demo Integrada Recomendada

Depois de subir a stack, rode:

```powershell
python -m exchange_tooling.cli real-market-simulator `
  --symbols PETR4,VALE3,BTC-USD `
  --start 2023-01-01 `
  --end 2023-02-01 `
  --interval 1d `
  --endpoint http://localhost:5103 `
  --replay-clock compressed-now `
  --replay-step-seconds 60 `
  --speed 0.25
```

Esse fluxo:

1. baixa candles históricas do Yahoo Finance;
2. converte cada candle em trajetória de preços sintética;
3. emite ordens compatíveis com `CreateOrderCommand`;
4. usa a stack inteira sem bypass no matching;
5. alimenta gráfico, ticker, trades e event tape do frontend.

## Health Checks

Use estes endpoints para validar a stack:

- [http://localhost:5103/health](http://localhost:5103/health)
- [http://localhost:5267/health](http://localhost:5267/health)
- [http://localhost:5075/health](http://localhost:5075/health)
- [http://localhost:4000/health](http://localhost:4000/health)
- `matching-engine`: valide por `docker compose ps`, logs, ou pelo fluxo de eventos Kafka. O binário atual roda como worker consumidor e não expõe health HTTP utilizável em `:7000`.
- frontend: [http://localhost:3000](http://localhost:3000)

## Testes

### .NET

```powershell
dotnet test tests\dotnet\Exchange.Trading.Domain.Tests\Exchange.Trading.Domain.Tests.csproj
```

### Matching Engine

```powershell
cd apps\matching-engine
cargo test
```

### Tooling

```powershell
python -m unittest discover apps\tooling\tests
```

Para o teste integrado do replay:

```powershell
$env:EXCHANGE_TOOLING_RUN_INTEGRATION = "1"
python -m unittest apps.tooling.tests.test_real_market_simulator_integration
```

### Frontend

```powershell
cd apps\frontend
npm test
npm run typecheck
npm run build
```

## Problemas Comuns

### Realtime não recebe eventos

Valide antes:

```powershell
Invoke-RestMethod http://localhost:4000/health
```

Se o `realtime-gateway` não estiver realmente escutando, a `query-api` vai logar timeout ao encaminhar eventos.

### Frontend mostra só uma candle

Isso normalmente indica um destes cenários:

- `query-api` ainda não foi reiniciada e está sem o endpoint `/candles`;
- `real-market-simulator` foi rodado sem `--replay-clock compressed-now`;
- o replay gerou poucos candles para o símbolo;
- o realtime não está chegando ao frontend.

Se a stack estiver em Docker, confirme também:

- o frontend em `http://localhost:3000` responde;
- `http://localhost:3000/query-api/health` responde;
- o replay Docker está apontando para `http://gateway-api:8080`, não para `localhost:5103` dentro do container do tooling.

### Kafka quebrado por cluster id / topics

Use:

```powershell
.\reset-kafka.bat
```

## Documentação Relacionada

Arquitetura e decisões:

- [libs/docs/architecture.md](libs/docs/architecture.md)
- [libs/docs/architecture-diagrams.md](libs/docs/architecture-diagrams.md)
- [libs/docs/adr-0002-defer-clearing-settlement.md](libs/docs/adr-0002-defer-clearing-settlement.md)

Modelo e contratos:

- [libs/docs/schema.md](libs/docs/schema.md)
- [libs/docs/kafka-topics.md](libs/docs/kafka-topics.md)
- [libs/contracts/README.md](libs/contracts/README.md)

Readmes por serviço:

- [apps/gateway-api/README.md](apps/gateway-api/README.md)
- [apps/query-api/README.md](apps/query-api/README.md)
- [apps/ledger-service/README.md](apps/ledger-service/README.md)
- [apps/matching-engine/README.md](apps/matching-engine/README.md)
- [apps/realtime-gateway/README.md](apps/realtime-gateway/README.md)
- [apps/frontend/README.md](apps/frontend/README.md)
- [apps/tooling/README.md](apps/tooling/README.md)
- [apps/quant-lab/README.md](apps/quant-lab/README.md)

## Roadmap Realista

Próximo passo técnico coerente:

1. consolidar histórico multi-intervalo de candles no backend;
2. ligar o `quant-lab` ao replay e às projeções do `query-api`;
3. adicionar métricas de estratégia, exposição e PnL;
4. implementar risco pré-trade antes de falar em ambiente mais sério.
