# Exchange Platform — Diagramas de Arquitetura

Este documento contém diagramas Mermaid representando os principais aspectos da plataforma.

---

## 1. Visão Geral dos Serviços

```mermaid
graph TB
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

    GW -->|Commands| KF
    GW --> PG
    GW --> RD

    KF -->|order-commands| ME
    ME -->|matching-events| KF

    KF -->|matching-events| LS
    KF -->|matching-events| QA
    KF -->|marketdata-events| RT

    LS --> PG
    QA --> PG
    QA --> RD

    TL -->|REST| GW
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
    participant RT as Realtime Gateway

    User->>FE: Submete ordem de compra
    FE->>GW: POST /api/orders
    GW->>GW: Valida conta e saldo
    GW->>PG: Persiste ordem (Pending)
    GW->>KF: Publica em order-commands
    KF->>ME: Consome comando
    ME->>ME: Processa order book (FIFO)
    ME->>KF: Publica TradeExecuted + BookUpdated
    KF->>LS: Consome TradeExecuted
    LS->>PG: Atualiza saldos
    KF->>RT: Consome marketdata-events
    RT->>FE: Broadcast via WebSocket
    FE->>User: Atualiza ticker e trades
```

---

## 3. Fluxo de Matching e Ledger

```mermaid
flowchart LR
    subgraph "Matching Engine (Rust)"
        OB["Order Book"]
        MA["Match Algorithm"]
    end

    subgraph "Ledger (C#)"
        BA["Balance Check"]
        RE["Reserve Funds"]
        SE["Settle Trade"]
    end

    Order["Nova Ordem"] --> BA
    BA -->|Saldo OK| RE
    RE -->|Fundos Reservados| OB
    OB --> MA
    MA -->|Trade Executado| SE
    SE -->|Credita ativo| Balance["Saldo Atualizado"]
    MA -->|Sem match| Rest["Ordem no Book"]
```

---

## 4. Fluxo de Market Data e Realtime

```mermaid
flowchart TB
    ME["Matching Engine"] -->|TradeExecuted| KF["Kafka: marketdata-events"]
    ME -->|BookUpdated| KF
    ME -->|TickerUpdated| KF

    KF --> RT["Realtime Gateway (Elixir)"]
    KF --> QA["Query API"]

    RT -->|Phoenix Channel| WS1["market:BTC-USD"]
    RT -->|Phoenix Channel| WS2["market:ETH-USD"]
    RT -->|Phoenix Channel| WS3["market:SOL-USD"]

    WS1 --> FE["Frontend WebSocket"]
    WS2 --> FE
    WS3 --> FE

    QA -->|Cache| RD["Redis"]
    QA -->|Read Models| PG["PostgreSQL"]
```

---

## 5. Separação por Linguagem e Responsabilidade

```mermaid
graph LR
    subgraph "C# / .NET"
        direction TB
        A1["APIs REST"]
        A2["Dominio de Ordens"]
        A3["Contas e Saldos"]
        A4["Ledger Contabil"]
        A5["Contratos e Eventos"]
        A6["Integracao com Infra"]
    end

    subgraph "Rust"
        direction TB
        B1["Order Book em Memoria"]
        B2["Algoritmo FIFO"]
        B3["Matching de Trades"]
        B4["Geracao de Eventos"]
    end

    subgraph "Elixir"
        direction TB
        C1["WebSocket Channels"]
        C2["Broadcast por Simbolo"]
        C3["Fanout de Eventos"]
    end

    subgraph "Python"
        direction TB
        D1["Simulador de Mercado GBM"]
        D2["Gerador de Ordens"]
        D3["Load Testing"]
        D4["Replay de Cenarios"]
    end

    A1 --> B1
    B4 --> C1
    D2 --> A1
```
