# Exchange Platform - Diagramas de Arquitetura

Este documento contém diagramas Mermaid representando os principais aspectos da plataforma.

---

## 1. Visão Geral dos Serviços

```mermaid
flowchart TB
    subgraph Frontend
        FE["Frontend React/TS :3000"]
    end

    subgraph "API Layer (C#)"
        GW["Gateway API :8080"]
        QA["Query API :8081"]
        LS["Ledger Service :8082"]
    end

    subgraph "Core Engine (Rust)"
        ME["Matching Engine :7000"]
    end

    subgraph "Realtime (Elixir)"
        RT["Realtime Gateway :4000"]
    end

    subgraph "Tooling (Python)"
        TL["Market Simulator / Order Flow"]
    end

    subgraph Infrastructure
        PG[(PostgreSQL)]
        RD[(Redis)]
        KF["Kafka"]
    end

    FE -->|REST| GW
    FE -->|REST| QA
    FE -->|WebSocket| RT

    TL -->|REST| GW

    GW --> PG
    GW --> RD
    GW -->|account-events| KF
    GW -->|order-commands| KF

    KF -->|order-commands| ME
    ME -->|matching-events| KF
    ME -->|marketdata-events| KF

    KF -->|account-events| LS
    KF -->|account-events| QA
    KF -->|matching-events| LS
    LS --> PG
    LS -->|ledger-events| KF

    KF -->|matching-events| QA
    KF -->|ledger-events| QA
    KF -->|marketdata-events| QA
    QA --> PG
    QA --> RD

    KF -->|matching-events / marketdata-events| RT
```

---

## 2. Fluxo de Criação de Ordem

```mermaid
sequenceDiagram
    actor User
    participant FE as Frontend
    participant GW as Gateway API
    participant PG as PostgreSQL
    participant KF as Kafka
    participant ME as Matching Engine
    participant LS as Ledger Service
    participant QA as Query API
    participant RT as Realtime Gateway

    User->>FE: Submete ordem de compra
    FE->>GW: POST /api/orders
    GW->>GW: Valida conta, payload e risco pré-trade
    GW->>PG: Persiste ordem aceita/pendente
    GW->>KF: Publica CreateOrder em order-commands
    KF->>ME: Entrega comando
    ME->>ME: Processa order book FIFO em memória
    ME->>KF: Publica matching-events
    ME->>KF: Publica marketdata-events
    KF->>LS: Entrega TradeExecuted / OrderFilled
    LS->>PG: Atualiza available e reserved
    LS->>KF: Publica ledger-events
    KF->>QA: Entrega matching-events
    KF->>QA: Entrega ledger-events
    KF->>QA: Entrega marketdata-events
    QA->>PG: Atualiza projeções de leitura
    QA->>QA: Atualiza cache em Redis
    KF->>RT: Entrega eventos realtime
    RT->>FE: Broadcast via WebSocket
    FE->>User: Atualiza livro, trades e saldo
```

---

## 3. Fluxo de Matching e Ledger

```mermaid
flowchart LR
    CMD["Comando CreateOrder"] --> GW["Gateway API"]
    GW -->|order-commands| KF1["Kafka"]

    subgraph "Matching Engine (Rust)"
        OB["Order Book em memória"]
        MA["Algoritmo FIFO"]
    end

    subgraph "Ledger Service (C#)"
        ST["Settlement"]
        BL["Atualização de saldos"]
        LE["Publicação de ledger-events"]
    end

    subgraph "Read Side (C#)"
        PR["Projeções de leitura"]
        CS["Cache / consultas"]
    end

    KF1 --> ME["Consumer de comandos"]
    ME --> OB
    OB --> MA
    MA -->|matching-events| KF2["Kafka"]

    KF2 --> ST
    ST --> BL
    BL --> PG["PostgreSQL"]
    BL --> LE
    LE -->|ledger-events| KF3["Kafka"]

    KF2 --> PR
    KF3 --> PR
    PR --> CS
```

---

## 4. Fluxo de Market Data e Realtime

```mermaid
flowchart TB
    ME["Matching Engine"] -->|matching-events| KF["Kafka"]
    ME -->|marketdata-events| KF

    KF -->|matching-events / marketdata-events| RT["Realtime Gateway (Elixir)"]
    KF -->|matching-events / marketdata-events| QA["Query API"]

    RT -->|Phoenix Channel| WS1["market:BTC-USD"]
    RT -->|Phoenix Channel| WS2["market:ETH-USD"]
    RT -->|Phoenix Channel| WS3["market:SOL-USD"]

    WS1 --> FE["Frontend"]
    WS2 --> FE
    WS3 --> FE

    QA -->|Read models| PG["PostgreSQL"]
    QA -->|Hot cache| RD["Redis"]
```

---

## 5. Separação por Linguagem e Responsabilidade

```mermaid
flowchart LR
    subgraph "C# / .NET"
        direction TB
        A1["APIs REST"]
        A2["Domínio de ordens"]
        A3["Contas e saldos"]
        A4["Ledger contábil"]
        A5["Contratos e eventos"]
        A6["Integração com PostgreSQL / Redis / Kafka"]
    end

    subgraph Rust
        direction TB
        B1["Order book em memória"]
        B2["Algoritmo FIFO"]
        B3["Matching de trades"]
        B4["Emissão de matching-events e marketdata-events"]
    end

    subgraph Elixir
        direction TB
        C1["Phoenix Channels"]
        C2["Fanout por símbolo"]
        C3["Broadcast realtime"]
    end

    subgraph Python
        direction TB
        D1["Simulador de mercado GBM"]
        D2["Gerador de ordens"]
        D3["Load testing"]
        D4["Replay de cenários"]
    end

    D2 --> A1
    A2 --> B1
    B3 --> A4
    B4 --> C1
```
