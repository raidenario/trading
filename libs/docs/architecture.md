# Exchange Platform — Architecture

## 1. Motivação

Este projeto modela uma **plataforma de exchange** (corretora de ativos digitais) com o objetivo de demonstrar arquitetura de sistemas de alto desempenho usando uma abordagem **poliglota** (múltiplas linguagens), **event-driven** (orientada a eventos) e **CQRS** (separação entre escrita e leitura).

A motivação é criar um projeto de portfólio que reflita decisões arquiteturais reais de fintechs e exchanges:

- **Latência ultrabaixa** no cruzamento de ordens → Rust
- **APIs e regras de negócio** bem estruturadas → C# / .NET
- **Milhares de conexões WebSocket simultâneas** → Elixir / Phoenix
- **Simulação e automação** rápida → Python

---

## 2. Papel de Cada Serviço

| Serviço | Linguagem | Porta | Responsabilidade |
|---------|-----------|-------|-----------------|
| `gateway-api` | C# | 8080 (Docker) / 5103 (local) | Porta de entrada REST. Recebe ordens, valida conta/saldo, encaminha ao matching |
| `query-api` | C# | 8081 / 5267 | Read-side. Consulta de histórico, ticker, candles, market overview |
| `ledger-service` | C# | 8082 / 5075 | Contabilidade. Saldos, reservas, extrato |
| `matching-engine` | Rust | 7000 | Motor de cruzamento. Order book FIFO em memória por nível de preço |
| `realtime-gateway` | Elixir | 4000 | Distribuição de eventos em tempo real via WebSocket (Phoenix Channels) |
| `tooling` | Python | CLI | Simulador de mercado, gerador de ordens, load test, replay |
| `frontend` | React/TS | 3000 | Interface web para operar e visualizar o sistema |

---

## 3. Papel de Cada Tecnologia de Infraestrutura

| Tecnologia | Papel | Source of Truth? |
|------------|-------|-----------------|
| **PostgreSQL** | Persistência primária de contas, saldos, ordens, trades e ledger | ✅ Sim |
| **Redis** | Cache de ticker, snapshot de market summary, pub/sub para realtime | ❌ Cache |
| **Kafka** | Mensageria assíncrona entre microserviços | N/A (transporte) |

### Source of Truth vs Cache

- **PostgreSQL** é a fonte da verdade para todos os dados persistentes
- **Redis** é usado apenas como camada de cache rápida e como veículo de pub/sub
- O **Order Book em memória (Rust)** é a fonte da verdade para o estado corrente do livro de ofertas; ele pode ser reconstruído a partir de replays de eventos se necessário

---

## 4. Write Side vs Read Side (CQRS)

```
┌─────────────┐     ┌──────────────────┐     ┌─────────────────┐
│ Write Side  │     │  Event Bus       │     │  Read Side      │
│             │────▶│  (Kafka)         │────▶│                 │
│ gateway-api │     │                  │     │ query-api       │
│ matching    │     │ order-commands   │     │ ledger-service  │
│ ledger      │     │ matching-events  │     │ Redis cache     │
│             │     │ ledger-events    │     │                 │
└─────────────┘     └──────────────────┘     └─────────────────┘
```

- **Write Side**: `gateway-api` recebe comandos, valida e publica em `order-commands`. O `matching-engine` processa e emite `matching-events` e `marketdata-events`. O `ledger-service` consome `account-events`, `order-commands` e `matching-events` para projetar saldos e emitir `ledger-events`.
- **Read Side**: `query-api` consome `order-commands`, `matching-events`, `ledger-events` e `marketdata-events` para servir histórico, ticker, trades e balances sem depender de chamadas síncronas ao ledger.

---

## 5. Tópicos Kafka

| Tópico | Produtor | Consumidor(es) | Conteúdo |
|--------|----------|----------------|----------|
| `order-commands` | gateway-api | matching-engine | Comandos de criação/cancelamento de ordens |
| `matching-events` | matching-engine | ledger, query | Aceite/rejeição de ordens e trades executados |
| `ledger-events` | ledger-service | query-api | Deltas de saldo e liberações de reserva |
| `marketdata-events` | matching-engine | realtime-gateway, query-api | Book e ticker updates |
| `account-events` | gateway-api | ledger, query | Criação de conta, funding |

---

## 6. Fluxo de Dados Macro

1. **Conta criada** → `gateway-api` → evento `AccountCreated` → PostgreSQL + Kafka
2. **Funding** → `gateway-api` → saldo atualizado → evento `AccountFunded`
3. **Ordem enviada** → `gateway-api` valida payload e publica em `order-commands`
4. **Matching** → Rust consome, processa order book em memória e gera `OrderAccepted` / `OrderRejected`, `TradeExecuted`, `BookUpdated`, `TickerUpdated`
5. **Settlement** → `ledger-service` reserva saldo a partir de `order-commands`, liquida trades a partir de `matching-events` e publica `ledger-events`
6. **Projeções** → `query-api` recebe comandos/eventos e atualiza modelos de leitura
7. **Realtime** → Elixir recebe eventos de market data e distribui via WebSocket
8. **Frontend** → consome REST + WebSocket para exibir dados ao usuário

---

## 7. Simulador Python

O módulo `tooling` em Python serve para **dar vida ao sistema** durante desenvolvimento:

- `exchange-tooling simulate` → gera tickers e candles contínuos (Geometric Brownian Motion)
- `exchange-tooling flow` → envia ordens fake continuamente para a API
- `exchange-tooling load` → burst controlado de ordens para stress test
- `exchange-tooling replay` → reproduz cenários salvos em JSONL

---

## 8. Estrutura de Balances

Cada conta mantém saldos por ativo com a seguinte modelagem:

```
┌──────────────────────────────────────────┐
│              Account Balance             │
├──────────────────────────────────────────┤
│ Available  = dinheiro livre para operar  │
│ Reserved   = dinheiro bloqueado em       │
│              ordens abertas              │
│ Total      = Available + Reserved        │
└──────────────────────────────────────────┘
```

Quando uma ordem de compra é aceita:
1. O valor necessário é **reservado** (moved de Available → Reserved)
2. Se a ordem é executada, o Reserved é consumido e o ativo é creditado
3. Se a ordem é cancelada, o Reserved volta para Available
