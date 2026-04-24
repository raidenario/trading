# Exchange Platform Schema Guide

Este documento consolida o schema atual do PostgreSQL, as migrations ativas e as garantias que o código assume hoje.

## Camadas de migration

### `001_init.sql`

Camada legada e ainda obrigatoria para bootstrap inicial:

- `accounts`
- `balances`
- `orders`
- `trades`
- `ledger_entries`

Tambem seeda as contas demo e saldos iniciais.

### `002_b3_core_model.sql`

Extende o schema legada com o modelo B3-inspired:

- `participants`
- `instruments`
- `trading_accounts`
- `trade_executions`
- `trade_allocations`
- `positions`

Tambem enriquece estruturas legadas:

- `orders.instrument_id`
- `orders.trading_account_id`
- `orders.source_system`
- `orders.execution_instructions`
- `orders.stop_price`
- `orders.average_price`
- `orders.accepted_at`
- `orders.cancelled_at`
- `ledger_entries.trading_account_id`
- `ledger_entries.balance_bucket`
- `ledger_entries.direction`
- `ledger_entries.reference_type`
- `ledger_entries.reference_id`
- `ledger_entries.metadata`

E cria as tabelas passivas reservadas para a fase futura:

- `settlement_obligations`
- `settlement_batches`
- `netting_sets`
- `clearing_sessions`
- `risk_snapshots`
- `custody_movements`

### `003_instrument_runtime_model.sql`

Adiciona o runtime operacional dos instrumentos:

- `instrument_trading_rules`
- `instrument_market_config`
- `instrument_status`

Tambem insere os instrumentos nao-crypto e sincroniza a tabela `instruments` com as regras/status runtime, para manter precisao, tick, lote e `trading_status` coerentes no bootstrap e no replay das migrations.

## Tabelas centrais

### `participants`

Representa a corretora/membro do mercado. Hoje existe um seed padrao:

- `SIMBROKER`

### `trading_accounts`

Relaciona uma `account` a um `participant`.

Invariantes atuais:

- uma `account` possui no maximo uma `trading_account` padrao
- contas demo sao seedadas na migration `002`
- contas criadas em runtime recebem uma `trading_account` default na camada de aplicacao

### `instruments`

Referencia canonica do ativo:

- taxonomia (`asset_class`, `segment`, `market`)
- assets base/cotacao
- precisao de preco/quantidade
- tick e lote
- status operacional simplificado

### `orders`

Continua aceitando o fluxo legada por `symbol`, mas a aplicacao passa a enriquecer o registro com:

- `instrument_id`
- `trading_account_id`
- `source_system`
- `execution_instructions`

### `trade_executions`

Registro explicito da execucao emitida pelo matching:

- `instrument_id`
- ordens buy/sell
- `buy_trading_account_id`
- `sell_trading_account_id`
- preco, quantidade, metadata

### `positions`

Projecao por `trading_account_id + instrument_id + position_date`:

- `net_quantity`
- `avg_open_price`
- `long_quantity`
- `short_quantity`

## Runtime de instrumentos

O comportamento de entrada de ordens depende da composicao entre:

- `instruments`
- `instrument_trading_rules`
- `instrument_market_config`
- `instrument_status`

Essa composicao e consumida pelo catalogo/validator da camada de aplicacao para:

- resolver `symbol -> instrument_id`
- validar tick, lote, min/max quantity e precisao
- validar sessoes (`Regular`, `AfterMarket`, `Auction`, `Closed`)
- enriquecer `execution_instructions`

## Seeds atuais

### Participante

- `SIMBROKER`

### Trading accounts demo

- `SIM-ALICE`
- `SIM-BOB`
- `SIM-CHARLIE`

### Instrumentos seedados

Crypto:

- `BTC-USD`
- `ETH-USD`
- `SOL-USD`

Brasil / multi-classe:

- `PETR4`
- `VALE3`
- `ITUB4`
- `PETR4F`
- `BOVA11`
- `SMAL11`
- `IVVB11`
- `AAPL34`
- `MSFT34`
- `GOGL34`
- `USD-BRL`
- `GOLD-SPOT`

## Compatibilidade e replay

As migrations continuam additive. O fluxo atual assume:

- requests legados ainda podem chegar apenas com `symbol`
- a gateway resolve `instrument_id` e `trading_account_id` antes de publicar no Kafka
- `TradeExecuted` pode carregar ou nao os campos enriquecidos, mantendo compatibilidade de payload
- replay de eventos reconstrui ordens enriquecidas, trades enriquecidos e `positions` sem depender de topicos novos

## Limites atuais

As estruturas abaixo existem apenas como preparacao para a fase futura:

- clearing ativo
- settlement ativo
- risco pre-trade
- custody
- netting

Essas partes seguem passive/nullable conforme o ADR-0002.
