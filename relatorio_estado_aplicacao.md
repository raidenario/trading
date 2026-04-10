# Relatório de Estado da Aplicação e Últimas Alterações

Este documento resume as recentes mudanças arquiteturais e implementações realizadas na plataforma de Exchange, com foco especial na introdução dos modelos inspirados na B3 e no enriquecimento dos serviços.

## 1. Introdução dos Modelos B3 (B3-inspired Core Model)

A aplicação está passando por uma evolução arquitetural significativa (conforme definido no **ADR-0002**) para suportar um fluxo de pós-negociação (clearing, settlement e risco) semelhante ao modelo da B3.

### 1.1 Novos Modelos de Domínio e Contratos
Foram introduzidos novos contratos em C# (`B3Models.cs` e `B3Taxonomy.cs`) e schemas JSON (`v1`) que definem de forma rica a taxonomia do mercado:
- **Instrumentos (`Instrument`)**: Agora possuem atributos detalhados como `AssetClass` (Crypto, Equity, Derivative, etc.), `Segment` (Cash, Futures, Options), `TickSize`, `LotSize`, `DeliveryType`, `PaymentType`, etc.
- **Participantes (`Participant`)**: Suporte a diferentes tipos de participantes (`Broker`, `Member`, `InternalDesk`).
- **Contas de Negociação (`TradingAccount`)**: Vinculação entre contas de usuários, participantes e identificadores externos.
- **Alocações e Execuções (`TradeExecution`, `TradeAllocation`)**: Separação clara entre a execução bruta na exchange e as alocações nas contas de negociação.

### 1.2 Alterações no Banco de Dados (`002_b3_core_model.sql`)
A estrutura de banco de dados (PostgreSQL) foi expandida de forma retrocompatível:
- Criação de novas tabelas base: `participants`, `instruments`, `trading_accounts`, `trade_executions`, `trade_allocations`, `positions`.
- Migração de dados de legado e retroalimentação de `orders` e `ledger_entries` com as novas referências (`instrument_id` e `trading_account_id`).
- Criação de tabelas **"passivas"** reservadas para implementação futura (Phase 2): `settlement_obligations`, `settlement_batches`, `netting_sets`, `clearing_sessions`, `risk_snapshots` e `custody_movements`. O *ADR-0002* estipulou o adiamento dos fluxos ativos dessas entidades para limitar o escopo (blast radius) e manter a estabilidade do ambiente atual de negociação.

## 2. Enriquecimento de Serviços

Para suportar o novo modelo, serviços da camada de aplicação (`Exchange.Trading.Application`) foram enriquecidos para realizar a validação e a tradução das intenções de negociação:

- **IInstrumentCatalog / StaticInstrumentCatalog**: A aplicação agora possui um catálogo de instrumentos que é consultado para resolver dados completos do instrumento alvo (buscando pelo `Symbol` enviado na ordem) durante a criação da mesma.
- **ITradingAccountResolver / DemoTradingAccountResolver**: Introdução do conceito de resolução de `TradingAccount` baseado na `AccountId` do usuário e no participante logado, garantindo a correta rastreabilidade no pós-trade.
- **OrderCommandService**: Atualizado para buscar os dados de `Instrument` e `TradingAccount` antes de injetá-los na engine de matching (`matching-engine`), repassando o contexto mais rico.
- As mensagens trocadas com o Kafka (`CreateOrderCommand`, `TradeExecuted`) agora comportam esses novos identificadores ricos do ecossistema.

## 3. Estado Atual da Aplicação

- **Estabilidade do Fluxo Central**: As mudanças introduziram os modelos sem quebrar o core de `Account Funding` -> `Order Submission` -> `Matching (FIFO)` -> `Ledger Balances`.
- **Backend (Kafka e Microsserviços)**: Ajustes recentes ("feat/true kafka orchestration and fix ledger service") demonstram esforço na estabilização dos consumidores do Ledger e da Query API integrados via Kafka.
- **Matching Engine**: Atualizações na stack de Rust (`matching-engine`) para acomodar os novos campos (`domain/order.rs`, `domain/trade.rs`) com ajustes de build para uso de CMake.
- **Infraestrutura**: Novos dados de seeding (`SIMBROKER` e instrumentos cripto) automatizados via scripts SQL no Docker Compose, facilitando o uso do ambiente de simulação e testes.

### Próximos Passos (Phase 2 projetada no ADR-0002)
A aplicação está agora preparada estruturalmente para:
1. Implementar orquestração de alocação pós-trade.
2. Iniciar fluxos de Sessões de Clearing (`clearing_sessions`).
3. Geração de Obrigações de Liquidação e Snapshots de Risco.
4. Estratégias de netting e movimentações de custódia definitivas.

---
*Relatório gerado automaticamente com base na análise dos últimos commits e arquivos não-staged no repositório.*
