# Exchange Platform

Uma plataforma de exchange simulada com arquitetura poliglota, event-driven e CQRS, inspirada nos modelos de pós-negociação da B3.

Este projeto demonstra a construção de um sistema financeiro completo usando a linguagem certa para cada tarefa:

| Linguagem | Papel |
|-----------|-------|
| **C# / .NET** | APIs REST, domínio de ordens, contas, saldos, ledger, integração com PostgreSQL/Redis/Kafka |
| **Rust** | Motor de cruzamento (Matching Engine) — order book FIFO em memória com latência mínima |
| **Elixir** | Gateway de tempo real — distribuição de eventos via WebSocket/Phoenix Channels |
| **Python** | Tooling — simulador de mercado multi-classe, gerador de ordens, load test, replay e cenários |
| **React/TS** | Frontend — dashboard, formulário de ordens, ticker, trades em tempo real |

---

## Arquitetura

```
                    ┌──────────────────────┐
                    │    Frontend (React)  │
                    │        :3000         │
                    └──────┬───────┬───────┘
                      REST │       │ WebSocket
              ┌────────────┘       └─────────────┐
              ▼                                  ▼
   ┌──────────────────┐                ┌────────────────────┐
   │ Gateway API (C#) │                │ Realtime GW (Elixir)│
   │     :8080        │                │     :4000           │
   └───────┬──────────┘                └────────▲───────────┘
           │                                     │
           ▼                                     │
   ┌──────────────┐    ┌─────────┐    ┌──────────┴──────────┐
   │    Kafka     │◄──▶│ Matching│    │ marketdata-events   │
   │              │    │ Engine  │────┘                      │
   │ order-cmds   │───▶│ (Rust)  │                          │
   │ matching-evts│    │  :7000  │                          │
   │ ledger-evts  │◀───│         │                          │
   └──────┬───────┘    └─────────┘                          │
          │                                                  │
   ┌──────▼──────┐  ┌──────────────┐                        │
   │ Ledger (C#) │  │ Query API    │◄───────────────────────┘
   │   :8082     │  │ (C#) :8081   │
   └──────┬──────┘  └──────┬───────┘
          │                │
          ▼                ▼
   ┌──────────────────────────────┐
   │     PostgreSQL  ·  Redis    │
   └──────────────────────────────┘
```

Para diagramas detalhados em Mermaid, veja [architecture-diagrams.md](libs/docs/architecture-diagrams.md).

---

## Fluxo Simplificado

1. **Frontend** envia requisição REST para a **Gateway API** criando conta e depositando fundos.
2. **Gateway API** valida, enriquece a ordem com `InstrumentId` e `TradingAccountId`, e publica no tópico Kafka `order-commands`.
3. **Matching Engine (Rust)** escuta `order-commands`, cruza ordens FIFO no order book, e publica resultados em `matching-events` e `marketdata-events`.
4. **Ledger Service (C#)** escuta `matching-events`, calcula deltas de saldo (reserva/débito/crédito), grava no PostgreSQL e publica em `ledger-events`.
5. **Query API (C#)** escuta `matching-events`, `ledger-events` e `marketdata-events`, monta projeções de leitura (trades, ticker, posições) via Redis/PostgreSQL.
6. **Realtime Gateway (Elixir)** escuta `marketdata-events` e empurra atualizações via WebSocket para o **Frontend** em tempo real.
7. **Tooling (Python)** simula mercado ativo enviando ordens sintéticas para a Gateway API.

---

## Serviços

| Serviço | Porta Docker | Porta Local | Descrição |
|---------|-------------|-------------|-----------|
| `gateway-api` | 8080 | 5103 | Criação de contas, funding, ordens (enriquecidas com instrument/trading account) |
| `query-api` | 8081 | 5267 | Read model reativo: histórico, ticker, trades, posições, instrumentos |
| `ledger-service` | 8082 | 5075 | Projeção contábil, posições e emissor de `ledger-events` |
| `matching-engine` | 7000 | 7000 | Order book FIFO e matching por símbolo |
| `realtime-gateway` | 4000 | 4000 | WebSocket por símbolo (Phoenix Channels) |
| `frontend` | 3000 | 3000 | Dashboard e trading (React/TypeScript/Vite) |
| `postgres` | 5432 | — | Banco de dados relacional (3 migrations) |
| `redis` | 6379 | — | Cache de projeções |
| `kafka` | 9092 | — | Mensageria event-driven (Confluent 7.6) |

---

## Modelo de Domínio B3-Inspired

A plataforma implementa um modelo de dados inspirado na B3 (ADR-0002), com as seguintes entidades:

### Entidades Ativas (em uso)

| Entidade | Descrição |
|----------|-----------|
| `Instrument` | Ativo negociável com metadata rica: asset class, segmento, mercado, tick size, lot size, precisão, ISIN |
| `Participant` | Abstração de corretora/membro (broker/member) |
| `TradingAccount` | Conta de negociação vinculada a um participant e uma account |
| `TradeExecution` | Registro explícito de execução no matching engine |
| `TradeAllocation` | Alocação por lado (compra/venda) para cada execução |
| `Position` | Projeção de posição por instrumento e conta de negociação |
| `LedgerEntry` (enriquecido) | Auditoria com bucket, direction, reference type e metadata |

### Entidades Passivas (reservadas para Phase 2)

| Entidade | Finalidade futura |
|----------|-------------------|
| `settlement_obligations` | Obrigações de liquidação |
| `settlement_batches` | Lotes de liquidação |
| `netting_sets` | Conjuntos de compensação |
| `clearing_sessions` | Sessões de clearing |
| `risk_snapshots` | Snapshots de risco |
| `custody_movements` | Movimentações de custódia |

---

## Catálogo de Instrumentos

O sistema suporta múltiplas classes de ativos com regras de negociação específicas:

| Classe | Instrumentos | Mercado | Sessões | Lot Size |
|--------|-------------|---------|---------|----------|
| **Crypto** | BTC-USD, ETH-USD, SOL-USD | CRYPTO_SPOT | Regular | 0.00000001 |
| **Equity** | PETR4, VALE3, ITUB4 | BR_EQUITIES | Regular | 100 (lote padrão) |
| **Equity (Fracionário)** | PETR4F | BR_EQUITIES | Regular | 1 (book separado) |
| **ETF** | BOVA11, SMAL11, IVVB11 | BR_ETF | Regular + After-Market | 1 |
| **BDR** | AAPL34, MSFT34, GOGL34 | BR_BDR | Regular + After-Market | 1 |
| **FX** | USD-BRL | FX_SPOT | Regular | 1 |
| **Commodity** | GOLD-SPOT | SYNTHETIC_COMMODITIES | Disabled | 0.001 |

Cada instrumento possui:
- **Trading Rules**: `tick_size`, `lot_size`, `min/max_quantity`, `allowed_order_types`, `allowed_sessions`, `matching_enabled`
- **Market Config**: horários de sessão (regular, after-market, auction), `separate_book`
- **Status**: `Active`, `Halted`, `Auction`, `AfterMarketOnly`, `Disabled`

---

## Banco de Dados (PostgreSQL)

O schema é composto por 3 migrations incrementais:

| Migration | Descrição |
|-----------|-----------|
| `001_init.sql` | Schema core: `accounts`, `balances`, `orders`, `trades`, `ledger_entries` + seed de contas demo |
| `002_b3_core_model.sql` | Modelo B3: `participants`, `instruments`, `trading_accounts`, `trade_executions`, `trade_allocations`, `positions` + tabelas passivas + backfill de dados legados |
| `003_instrument_runtime_model.sql` | Runtime: `instrument_trading_rules`, `instrument_market_config`, `instrument_status` + seed de 15 instrumentos com regras e horários |

Todas as migrations são executadas automaticamente pelo Docker Compose via `initdb.d`.

---

## Contas Demo (Seed)

| Nome | Account ID | Trading Account | Saldos Iniciais |
|------|-----------|-----------------|-----------------|
| Alice Trader | `11111111-...111` | `SIM-ALICE` | 100k USD, 5 BTC, 50 ETH |
| Bob Market | `22222222-...222` | `SIM-BOB` | 250k USD, 10 BTC, 500 SOL |
| Charlie Whale | `33333333-...333` | `SIM-CHARLIE` | 1M USD, 50 BTC, 200 ETH, 2k SOL |

Participant padrão: `SIMBROKER` (Simulator Brokerage Ltda).

---

## Como Rodar

### Opção 1: Docker Compose (recomendado)

```bash
docker compose -f infra/compose/docker-compose.yml up --build
```

Isso sobe todos os serviços + PostgreSQL + Redis + Kafka + criação automática de tópicos.

### Opção 2: Local (desenvolvimento)

**Pré-requisitos:** .NET SDK 10, Rust/Cargo, Elixir/Mix, Node.js 20+, Python 3.11+

```bash
# .NET
dotnet restore ExchangePlatform.slnx
dotnet run --project apps/gateway-api/src/Exchange.Gateway.Api
dotnet run --project apps/query-api/src/Exchange.Query.Api
dotnet run --project apps/ledger-service/src/Exchange.Ledger.Api

# Rust
cd apps/matching-engine && cargo run --bin matching-engine-service

# Elixir
cd apps/realtime-gateway && mix deps.get && mix phx.server

# Frontend
cd apps/frontend && npm install && npm run dev

# Python Simulador
cd apps/tooling && pip install -e . && exchange-tooling simulate
```

Ou use os scripts prontos: `start-local.cmd` / `start-docker.cmd`

---

## Simulador de Mercado e Tooling (Python)

O tooling foi expandido para um sistema completo de simulação com catálogo de instrumentos, sessões de mercado e cenários pré-definidos.

### Comandos Disponíveis

```bash
cd apps/tooling
pip install -e .

# Gerar uma ordem fake (JSON stdout)
exchange-tooling fake-order --symbol PETR4
exchange-tooling fake-order --asset-class Equity

# Simulação contínua de market data (tickers + candles)
exchange-tooling simulate                              # Crypto padrão
exchange-tooling simulate --asset-class Equity          # Ações BR
exchange-tooling simulate --symbols PETR4,VALE3,BTC-USD # Símbolos específicos

# Cenários de mercado pré-definidos
exchange-tooling simulate-market --scenario expanded-market  # Todos os mercados
exchange-tooling simulate-market --scenario equities         # PETR4, VALE3, ITUB4, PETR4F
exchange-tooling simulate-market --scenario etf              # BOVA11, IVVB11
exchange-tooling simulate-market --scenario bdr              # AAPL34, MSFT34
exchange-tooling simulate-market --scenario fx               # USD-BRL
exchange-tooling simulate-market --scenario crypto           # BTC, ETH, SOL

# Fluxo contínuo de ordens para a API
exchange-tooling flow --rate 2 --endpoint http://localhost:5103
exchange-tooling flow --asset-class Equity --session regular

# Burst de ordens
exchange-tooling load --count 50 --symbols PETR4,VALE3

# Replay de ordens de arquivo JSONL
exchange-tooling replay orders.jsonl --speed 2.0

# Modo dry-run (sem enviar)
exchange-tooling flow --dry-run
```

### Filtros Disponíveis

Todos os comandos suportam filtros combinados:
- `--symbol` / `--symbols` — filtrar por símbolo(s)
- `--asset-class` — filtrar por classe (Crypto, Equity, Etf, Bdr, Fx, Commodity)
- `--market` — filtrar por mercado (CRYPTO_SPOT, BR_EQUITIES, BR_ETF, BR_BDR, FX_SPOT)
- `--book-mode` — filtrar por modo de book (SPOT_STANDARD, SPOT_FRACTIONAL, SPOT_EXTENDED_HOURS)
- `--session` — sessão de mercado (regular, after-market, auction, closed)

### Cenários de Mercado

| Cenário | Instrumentos |
|---------|-------------|
| `expanded-market` | BTC-USD, PETR4, VALE3, BOVA11, AAPL34, USD-BRL, GOLD-SPOT |
| `equities` | PETR4, VALE3, ITUB4, PETR4F |
| `etf` | BOVA11, IVVB11 |
| `bdr` | AAPL34, MSFT34 |
| `fx` | USD-BRL |
| `crypto` | BTC-USD, ETH-USD, SOL-USD |

---

## Contratos

Os contratos compartilhados estão em `libs/contracts/`:

### Commands
`CreateAccountCommand`, `FundAccountCommand`, `CreateOrderCommand`, `CancelOrderCommand`

### Events
`AccountCreated`, `AccountFunded`, `OrderAccepted`, `OrderRejected`, `FundsReserved`, `TradeExecuted`, `OrderFilled`, `OrderPartiallyFilled`, `BookUpdated`, `TickerUpdated`, `CandleUpdated`

### Modelos B3
`B3Models.cs` — `Instrument`, `Participant`, `TradingAccount`, `TradeExecution`, `TradeAllocation`, `Position`
`B3Taxonomy.cs` — `AssetClass`, `Segment`, `DeliveryType`, `PaymentType`, `SettlementType`, `ParticipantType`

### JSON Schemas (v1)
```
libs/contracts/schemas/v1/
├── book-updated.json
├── cancel-order-command.json
├── create-account-command.json
├── create-order-command.json
├── fund-account-command.json
├── instrument.json
├── order-accepted.json
├── order-rejected.json
├── position-snapshot.json
├── ticker-updated.json
├── trade-executed.json
└── trading-account.json
```

---

## Tópicos Kafka

| Tópico | Partições | Descrição |
|--------|-----------|-----------|
| `order-commands` | 3 | Comandos de criação e cancelamento de ordens |
| `matching-events` | 3 | Aceite/rejeição de ordens e trades executados |
| `ledger-events` | 3 | Deltas de saldo/reserva emitidos pelo ledger |
| `marketdata-events` | 3 | Book updates, ticker updates |
| `account-events` | 2 | Criação de conta e funding |

Os tópicos são criados automaticamente pelo container `kafka-init` no Docker Compose.

---

## Testes

O projeto possui suítes de teste organizadas em `tests/dotnet/`:

| Projeto de Teste | Alvo |
|-----------------|------|
| `Exchange.Trading.Domain.Tests` | Entidades, Value Objects e regras de domínio |
| `Exchange.Trading.Application.Tests` | Serviços de aplicação (ordens, contas) |
| `Exchange.Platform.Contracts.Tests` | Contratos e serialização |
| `Exchange.Query.Api.Tests` | Projeções e endpoints de leitura |
| `Exchange.Ledger.Api.Tests` | Processamento de ledger |
| `Exchange.Database.Tests` | Migrations e schema |

```bash
dotnet test ExchangePlatform.slnx
```

---

## Recuperação do Kafka

Se aparecer erro de `different ClusterID`, `Unknown topic or partition` ou o producer parar de publicar:

```bash
reset-kafka.bat
```

O script remove os volumes nomeados de `kafka` e `zookeeper`, sobe o broker limpo e recria os tópicos obrigatórios.

---

## Estrutura do Monorepo

```
apps/
  gateway-api/        # C# - API de entrada (enriquecimento instrument/trading account)
  query-api/          # C# - API de leitura (projeções, instrumentos, posições)
  ledger-service/     # C# - Contabilidade e posições
  matching-engine/    # Rust - Motor de cruzamento FIFO
  realtime-gateway/   # Elixir - WebSocket (Phoenix Channels)
  tooling/            # Python - Simulador multi-classe, cenários e load test
  frontend/           # React/TS - Interface web (Vite)
libs/
  contracts/          # Contratos compartilhados (C# + JSON schemas v1)
    dotnet/           # B3Models, B3Taxonomy, Commands, Events, ReadModels
    schemas/v1/       # JSON schemas para validação cross-language
  docs/               # Documentação arquitetural, ADRs e diagramas
  dotnet/             # Bibliotecas .NET compartilhadas
    Exchange.Trading.Domain/        # Entidades e Value Objects
    Exchange.Trading.Application/   # Serviços de aplicação
    Exchange.Trading.Infrastructure/# Repositórios, Kafka, Matching
    Exchange.Ledger.Domain/         # Domínio do ledger
infra/
  compose/            # docker-compose.yml (todos os serviços + infra)
  docker/             # Dockerfiles (7 serviços)
  postgres/           # SQL migrations (001, 002, 003) e seed
tests/
  dotnet/             # Testes unitários e de integração (6 projetos)
```

---

## Decisões Arquiteturais (ADRs)

| ADR | Título | Status |
|-----|--------|--------|
| ADR-0002 | [Defer Active Clearing, Settlement, and Risk Workflows](libs/docs/adr-0002-defer-clearing-settlement.md) | Accepted |

---

## Próximos Passos

### Fase Atual — Consolidação
- [ ] Persistir projeções e ledger em PostgreSQL em vez de memória local
- [ ] Outbox/inbox pattern para idempotência entre serviços
- [ ] Market data incremental (ticker/candles) via Redis Streams

### Phase 2 — Pós-Trade (ADR-0002)
- [ ] Orquestração de alocação pós-trade
- [ ] Sessões de Clearing ativas
- [ ] Geração de obrigações de liquidação
- [ ] Snapshots de risco e exposição
- [ ] Netting/gross settlement strategy
- [ ] Movimentações de custódia

### Phase 3 — Produção
- [ ] Validação de risco pré-trade
- [ ] Autenticação JWT e API gateway (YARP/Kong)
- [ ] Observabilidade (OpenTelemetry, Prometheus, Grafana)
- [ ] Testes de contrato entre linguagens (Pact)
- [ ] Deploy para Kubernetes com Helm charts

---

## Documentação

- [Arquitetura detalhada](libs/docs/architecture.md)
- [Diagramas Mermaid](libs/docs/architecture-diagrams.md)
- [ADR-0002: Defer Clearing/Settlement](libs/docs/adr-0002-defer-clearing-settlement.md)
- [Tópicos Kafka](libs/docs/kafka-topics.md)
- [Contratos README](libs/contracts/README.md)
