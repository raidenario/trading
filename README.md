# Exchange Platform

Uma plataforma de exchange simulada com arquitetura poliglota, event-driven e CQRS.

Este projeto demonstra a construção de um sistema financeiro completo usando a linguagem certa para cada tarefa:

| Linguagem | Papel |
|-----------|-------|
| **C# / .NET** | APIs REST, domínio de ordens, contas, saldos, ledger, integração com PostgreSQL/Redis/Kafka |
| **Rust** | Motor de cruzamento (Matching Engine) — order book FIFO em memória com latência mínima |
| **Elixir** | Gateway de tempo real — distribuição de eventos via WebSocket/Phoenix Channels |
| **Python** | Tooling — simulador de mercado, gerador de ordens, load test e replay |
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
   │          PostgreSQL          │
   │          Redis               │
   └──────────────────────────────┘
```

Para diagramas detalhados em Mermaid, veja [architecture-diagrams.md](libs/docs/architecture-diagrams.md).

---

## Serviços

| Serviço | Porta Docker | Porta Local | Descrição |
|---------|-------------|-------------|-----------|
| `gateway-api` | 8080 | 5103 | Criação de contas, funding, ordens |
| `query-api` | 8081 | 5267 | Read model reativo: histórico, ticker, trades, market overview |
| `ledger-service` | 8082 | 5075 | Projeção contábil e emissor de `ledger-events` |
| `matching-engine` | 7000 | 7000 | Order book e matching FIFO |
| `realtime-gateway` | 4000 | 4000 | WebSocket por símbolo |
| `frontend` | 3000 | 3000 | Dashboard e trading |
| `postgres` | 5432 | — | Banco de dados |
| `redis` | 6379 | — | Cache |
| `kafka` | 9092 | — | Mensageria |

---

## Contas Demo (Seed)

| Nome | Account ID | Saldos Iniciais |
|------|-----------|-----------------|
| Alice Trader | `11111111-1111-1111-1111-111111111111` | 100k USD, 5 BTC, 50 ETH |
| Bob Market | `22222222-2222-2222-2222-222222222222` | 250k USD, 10 BTC, 500 SOL |
| Charlie Whale | `33333333-3333-3333-3333-333333333333` | 1M USD, 50 BTC, 200 ETH, 2k SOL |

---

## Como Rodar

### Opção 1: Docker Compose (recomendado)

```bash
docker compose -f infra/compose/docker-compose.yml up --build
```

Isso sobe todos os serviços + PostgreSQL + Redis + Kafka.

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

## Simulador de Mercado (Python)

```bash
cd apps/tooling
pip install -e .

# Simular tickers e candles contínuos
exchange-tooling simulate

# Enviar ordens fake continuamente
exchange-tooling flow --rate 2 --endpoint http://localhost:5103

# Burst de 50 ordens
exchange-tooling load --count 50

# Modo dry-run (sem enviar)
exchange-tooling flow --dry-run
```

---

## Contratos

Os contratos compartilhados estão em `libs/contracts/`:

**Commands:** `CreateAccountCommand`, `FundAccountCommand`, `CreateOrderCommand`, `CancelOrderCommand`

**Events:** `AccountCreated`, `AccountFunded`, `OrderAccepted`, `OrderRejected`, `FundsReserved`, `TradeExecuted`, `OrderFilled`, `OrderPartiallyFilled`, `BookUpdated`, `TickerUpdated`, `CandleUpdated`

**JSON Schemas:** `libs/contracts/schemas/v1/`

---

## Estrutura do Monorepo

```
apps/
  gateway-api/        # C# - API de entrada
  query-api/          # C# - API de leitura
  ledger-service/     # C# - Contabilidade
  matching-engine/    # Rust - Motor de cruzamento
  realtime-gateway/   # Elixir - WebSocket
  tooling/            # Python - Simulador e ferramentas
  frontend/           # React/TS - Interface web
libs/
  contracts/          # Contratos compartilhados (C# + JSON schemas)
  docs/               # Documentação arquitetural e diagramas
  dotnet/             # Bibliotecas .NET compartilhadas
infra/
  compose/            # docker-compose.yml
  docker/             # Dockerfiles
  postgres/           # SQL migrations e seed
tests/
  dotnet/             # Testes unitários e de integração
```

---

## Tópicos Kafka

| Tópico | Descrição |
|--------|-----------|
| `order-commands` | Comandos de criação e cancelamento de ordens |
| `matching-events` | Aceite/rejeição de ordens e trades executados |
| `ledger-events` | Deltas de saldo/reserva emitidos pelo ledger |
| `marketdata-events` | Book e ticker updates |
| `account-events` | Criação de conta e funding |

## Recuperação do Kafka

Se aparecer erro de `different ClusterID`, `Unknown topic or partition` ou o producer parar de publicar, execute:

```bash
reset-kafka.bat
```

O script agora remove os volumes nomeados de `kafka` e `zookeeper`, sobe o broker limpo e recria os tópicos obrigatórios.

---

## Próximos Passos

- [ ] Persistir projections e ledger em PostgreSQL em vez de memória local
- [ ] Outbox/inbox pattern para idempotência entre serviços
- [ ] Validação de risco pré-trade e ledger assíncrono pós-trade
- [ ] Market data incremental (ticker/candles) via Redis Streams
- [ ] Observabilidade (OpenTelemetry, Prometheus, Grafana)
- [ ] Testes de contrato entre linguagens (Pact)
- [ ] Autenticação JWT e API gateway (YARP/Kong)
- [ ] Deploy para Kubernetes com Helm charts

---

## Documentação

- [Arquitetura detalhada](libs/docs/architecture.md)
- [Diagramas Mermaid](libs/docs/architecture-diagrams.md)
