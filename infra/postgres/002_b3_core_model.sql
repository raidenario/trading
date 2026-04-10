-- Exchange Platform - B3-inspired core model extension
-- This migration is incremental and backward-compatible.

CREATE TABLE participants (
    participant_id   UUID PRIMARY KEY,
    participant_code VARCHAR(32)  NOT NULL,
    legal_name       VARCHAR(256) NOT NULL,
    display_name     VARCHAR(128) NOT NULL,
    participant_type VARCHAR(32)  NOT NULL,
    status           VARCHAR(32)  NOT NULL,
    created_at       TIMESTAMPTZ  NOT NULL,
    updated_at       TIMESTAMPTZ  NOT NULL
);

CREATE UNIQUE INDEX ux_participants_code ON participants(participant_code);

CREATE TABLE instruments (
    instrument_id        UUID PRIMARY KEY,
    symbol               VARCHAR(32)   NOT NULL,
    asset_class          VARCHAR(32)   NOT NULL,
    segment              VARCHAR(32)   NOT NULL,
    market               VARCHAR(32)   NOT NULL,
    isin                 VARCHAR(32),
    base_asset           VARCHAR(16)   NOT NULL,
    quote_asset          VARCHAR(16)   NOT NULL,
    price_precision      INTEGER       NOT NULL,
    quantity_precision   INTEGER       NOT NULL,
    tick_size            DECIMAL(28,8) NOT NULL,
    lot_size             DECIMAL(28,8) NOT NULL,
    trading_status       VARCHAR(32)   NOT NULL,
    trading_start_at     TIMESTAMPTZ,
    trading_end_at       TIMESTAMPTZ,
    expiration_date      DATE,
    contract_multiplier  DECIMAL(28,8),
    settlement_type      VARCHAR(32),
    delivery_type        VARCHAR(32),
    payment_type         VARCHAR(32),
    metadata             JSONB         NOT NULL DEFAULT '{}'::jsonb
);

CREATE UNIQUE INDEX ux_instruments_symbol ON instruments(symbol);

CREATE TABLE trading_accounts (
    trading_account_id     UUID PRIMARY KEY,
    account_id             UUID         NOT NULL REFERENCES accounts(account_id),
    participant_id         UUID         NOT NULL REFERENCES participants(participant_id),
    external_account_code  VARCHAR(64),
    status                 VARCHAR(32)  NOT NULL,
    created_at             TIMESTAMPTZ  NOT NULL,
    updated_at             TIMESTAMPTZ  NOT NULL
);

CREATE UNIQUE INDEX ux_trading_accounts_account_id ON trading_accounts(account_id);
CREATE UNIQUE INDEX ux_trading_accounts_external_code ON trading_accounts(external_account_code) WHERE external_account_code IS NOT NULL;

ALTER TABLE orders ADD COLUMN instrument_id UUID;
ALTER TABLE orders ADD COLUMN trading_account_id UUID;
ALTER TABLE orders ADD COLUMN source_system VARCHAR(32);
ALTER TABLE orders ADD COLUMN execution_instructions JSONB;
ALTER TABLE orders ADD COLUMN stop_price DECIMAL(28,8);
ALTER TABLE orders ADD COLUMN average_price DECIMAL(28,8);
ALTER TABLE orders ADD COLUMN accepted_at TIMESTAMPTZ;
ALTER TABLE orders ADD COLUMN cancelled_at TIMESTAMPTZ;

ALTER TABLE orders
    ADD CONSTRAINT fk_orders_instrument_id
    FOREIGN KEY (instrument_id) REFERENCES instruments(instrument_id);

ALTER TABLE orders
    ADD CONSTRAINT fk_orders_trading_account_id
    FOREIGN KEY (trading_account_id) REFERENCES trading_accounts(trading_account_id);

CREATE INDEX idx_orders_instrument_id ON orders(instrument_id);
CREATE INDEX idx_orders_trading_account_id ON orders(trading_account_id);

CREATE TABLE trade_executions (
    trade_execution_id       UUID PRIMARY KEY,
    trade_execution_code     VARCHAR(64)   NOT NULL,
    instrument_id            UUID          NOT NULL REFERENCES instruments(instrument_id),
    buy_order_id             UUID          NOT NULL REFERENCES orders(order_id),
    sell_order_id            UUID          NOT NULL REFERENCES orders(order_id),
    buy_trading_account_id   UUID          NOT NULL REFERENCES trading_accounts(trading_account_id),
    sell_trading_account_id  UUID          NOT NULL REFERENCES trading_accounts(trading_account_id),
    quantity                 DECIMAL(28,8) NOT NULL,
    price                    DECIMAL(28,8) NOT NULL,
    executed_at              TIMESTAMPTZ   NOT NULL,
    aggressor_side           VARCHAR(8),
    trade_source             VARCHAR(64)   NOT NULL,
    exchange_execution_id    VARCHAR(128),
    metadata                 JSONB         NOT NULL DEFAULT '{}'::jsonb
);

CREATE UNIQUE INDEX ux_trade_executions_code ON trade_executions(trade_execution_code);
CREATE INDEX idx_trade_executions_instrument_executed_at ON trade_executions(instrument_id, executed_at DESC);

CREATE TABLE trade_allocations (
    trade_allocation_id   UUID PRIMARY KEY,
    trade_execution_id    UUID          NOT NULL REFERENCES trade_executions(trade_execution_id),
    trading_account_id    UUID          NOT NULL REFERENCES trading_accounts(trading_account_id),
    side                  VARCHAR(8)    NOT NULL,
    allocated_quantity    DECIMAL(28,8) NOT NULL,
    allocation_status     VARCHAR(32)   NOT NULL,
    created_at            TIMESTAMPTZ   NOT NULL
);

CREATE INDEX idx_trade_allocations_trade_execution_id ON trade_allocations(trade_execution_id);
CREATE INDEX idx_trade_allocations_trading_account_id ON trade_allocations(trading_account_id);

CREATE TABLE positions (
    position_id          UUID PRIMARY KEY,
    trading_account_id   UUID          NOT NULL REFERENCES trading_accounts(trading_account_id),
    instrument_id        UUID          NOT NULL REFERENCES instruments(instrument_id),
    position_date        DATE          NOT NULL,
    net_quantity         DECIMAL(28,8) NOT NULL,
    avg_open_price       DECIMAL(28,8),
    long_quantity        DECIMAL(28,8) NOT NULL DEFAULT 0,
    short_quantity       DECIMAL(28,8) NOT NULL DEFAULT 0,
    updated_at           TIMESTAMPTZ   NOT NULL
);

CREATE UNIQUE INDEX ux_positions_account_instrument_date ON positions(trading_account_id, instrument_id, position_date);
CREATE INDEX idx_positions_trading_account_instrument_date ON positions(trading_account_id, instrument_id, position_date DESC);

ALTER TABLE ledger_entries ADD COLUMN trading_account_id UUID;
ALTER TABLE ledger_entries ADD COLUMN balance_bucket VARCHAR(32);
ALTER TABLE ledger_entries ADD COLUMN direction VARCHAR(16);
ALTER TABLE ledger_entries ADD COLUMN reference_type VARCHAR(32);
ALTER TABLE ledger_entries ADD COLUMN reference_id VARCHAR(128);
ALTER TABLE ledger_entries ADD COLUMN metadata JSONB;

ALTER TABLE ledger_entries
    ADD CONSTRAINT fk_ledger_entries_trading_account_id
    FOREIGN KEY (trading_account_id) REFERENCES trading_accounts(trading_account_id);

CREATE INDEX idx_ledger_entries_trading_account_id ON ledger_entries(trading_account_id);
CREATE INDEX idx_ledger_entries_reference_type_id ON ledger_entries(reference_type, reference_id);

CREATE TABLE settlement_obligations (
    settlement_obligation_id UUID PRIMARY KEY,
    trading_account_id       UUID          NOT NULL REFERENCES trading_accounts(trading_account_id),
    instrument_id            UUID          NOT NULL REFERENCES instruments(instrument_id),
    quantity                 DECIMAL(28,8) NOT NULL,
    amount                   DECIMAL(28,8),
    status                   VARCHAR(32)   NOT NULL,
    metadata                 JSONB         NOT NULL DEFAULT '{}'::jsonb,
    created_at               TIMESTAMPTZ   NOT NULL DEFAULT NOW()
);
COMMENT ON TABLE settlement_obligations IS 'reserved for future implementation';

CREATE TABLE settlement_batches (
    settlement_batch_id UUID PRIMARY KEY,
    batch_code          VARCHAR(64) NOT NULL,
    status              VARCHAR(32) NOT NULL,
    metadata            JSONB       NOT NULL DEFAULT '{}'::jsonb,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
COMMENT ON TABLE settlement_batches IS 'reserved for future implementation';

CREATE TABLE netting_sets (
    netting_set_id   UUID PRIMARY KEY,
    participant_id   UUID        NOT NULL REFERENCES participants(participant_id),
    status           VARCHAR(32) NOT NULL,
    metadata         JSONB       NOT NULL DEFAULT '{}'::jsonb,
    created_at       TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
COMMENT ON TABLE netting_sets IS 'reserved for future implementation';

CREATE TABLE clearing_sessions (
    clearing_session_id UUID PRIMARY KEY,
    session_code        VARCHAR(64) NOT NULL,
    status              VARCHAR(32) NOT NULL,
    metadata            JSONB       NOT NULL DEFAULT '{}'::jsonb,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
COMMENT ON TABLE clearing_sessions IS 'reserved for future implementation';

CREATE TABLE risk_snapshots (
    risk_snapshot_id   UUID PRIMARY KEY,
    trading_account_id UUID        NOT NULL REFERENCES trading_accounts(trading_account_id),
    status             VARCHAR(32) NOT NULL,
    metadata           JSONB       NOT NULL DEFAULT '{}'::jsonb,
    captured_at        TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
COMMENT ON TABLE risk_snapshots IS 'reserved for future implementation';

CREATE TABLE custody_movements (
    custody_movement_id UUID PRIMARY KEY,
    trading_account_id  UUID          NOT NULL REFERENCES trading_accounts(trading_account_id),
    instrument_id       UUID          NOT NULL REFERENCES instruments(instrument_id),
    quantity            DECIMAL(28,8) NOT NULL,
    status              VARCHAR(32)   NOT NULL,
    metadata            JSONB         NOT NULL DEFAULT '{}'::jsonb,
    created_at          TIMESTAMPTZ   NOT NULL DEFAULT NOW()
);
COMMENT ON TABLE custody_movements IS 'reserved for future implementation';

INSERT INTO participants (
    participant_id,
    participant_code,
    legal_name,
    display_name,
    participant_type,
    status,
    created_at,
    updated_at
)
VALUES (
    '99999999-0000-0000-0000-000000000001',
    'SIMBROKER',
    'Simulator Brokerage Ltda',
    'Simulator Broker',
    'Broker',
    'Active',
    '2026-04-01T00:00:00Z',
    '2026-04-01T00:00:00Z'
)
ON CONFLICT (participant_id) DO NOTHING;

INSERT INTO instruments (
    instrument_id,
    symbol,
    asset_class,
    segment,
    market,
    isin,
    base_asset,
    quote_asset,
    price_precision,
    quantity_precision,
    tick_size,
    lot_size,
    trading_status,
    settlement_type,
    delivery_type,
    payment_type,
    metadata
)
VALUES
    ('aaaaaaaa-0000-0000-0000-000000000001', 'BTC-USD', 'Crypto', 'Crypto', 'Spot', NULL, 'BTC', 'USD', 2, 8, 0.01, 0.00000001, 'Active', 'Spot', 'None', 'Dvp', '{"category":"major"}'::jsonb),
    ('aaaaaaaa-0000-0000-0000-000000000002', 'ETH-USD', 'Crypto', 'Crypto', 'Spot', NULL, 'ETH', 'USD', 2, 8, 0.01, 0.00000001, 'Active', 'Spot', 'None', 'Dvp', '{"category":"major"}'::jsonb),
    ('aaaaaaaa-0000-0000-0000-000000000003', 'SOL-USD', 'Crypto', 'Crypto', 'Spot', NULL, 'SOL', 'USD', 2, 8, 0.0001, 0.00000001, 'Active', 'Spot', 'None', 'Dvp', '{"category":"growth"}'::jsonb)
ON CONFLICT (instrument_id) DO NOTHING;

INSERT INTO trading_accounts (
    trading_account_id,
    account_id,
    participant_id,
    external_account_code,
    status,
    created_at,
    updated_at
)
VALUES
    ('bbbbbbbb-0000-0000-0000-000000000001', '11111111-1111-1111-1111-111111111111', '99999999-0000-0000-0000-000000000001', 'SIM-ALICE', 'Active', '2026-04-01T00:00:00Z', '2026-04-01T00:00:00Z'),
    ('bbbbbbbb-0000-0000-0000-000000000002', '22222222-2222-2222-2222-222222222222', '99999999-0000-0000-0000-000000000001', 'SIM-BOB', 'Active', '2026-04-01T00:00:00Z', '2026-04-01T00:00:00Z'),
    ('bbbbbbbb-0000-0000-0000-000000000003', '33333333-3333-3333-3333-333333333333', '99999999-0000-0000-0000-000000000001', 'SIM-CHARLIE', 'Active', '2026-04-01T00:00:00Z', '2026-04-01T00:00:00Z')
ON CONFLICT (trading_account_id) DO NOTHING;

UPDATE orders
SET instrument_id = instruments.instrument_id
FROM instruments
WHERE orders.instrument_id IS NULL
  AND instruments.symbol = orders.symbol;

UPDATE orders
SET trading_account_id = trading_accounts.trading_account_id
FROM trading_accounts
WHERE orders.trading_account_id IS NULL
  AND trading_accounts.account_id = orders.account_id;

UPDATE orders
SET source_system = COALESCE(source_system, 'Api'),
    execution_instructions = COALESCE(execution_instructions, '{}'::jsonb);

UPDATE ledger_entries
SET reference_type = COALESCE(reference_type, 'Funding'),
    reference_id = COALESCE(reference_id, reference),
    balance_bucket = COALESCE(balance_bucket, 'Available'),
    direction = COALESCE(direction, CASE WHEN amount >= 0 THEN 'Credit' ELSE 'Debit' END),
    metadata = COALESCE(metadata, '{}'::jsonb);

INSERT INTO trade_executions (
    trade_execution_id,
    trade_execution_code,
    instrument_id,
    buy_order_id,
    sell_order_id,
    buy_trading_account_id,
    sell_trading_account_id,
    quantity,
    price,
    executed_at,
    trade_source,
    metadata
)
SELECT
    trade_id,
    'legacy-' || trade_id::text,
    COALESCE(buy_order.instrument_id, sell_order.instrument_id),
    trades.buy_order_id,
    trades.sell_order_id,
    buy_order.trading_account_id,
    sell_order.trading_account_id,
    trades.quantity,
    trades.price,
    trades.executed_at,
    'LegacyBackfill',
    jsonb_build_object('legacy_trade_id', trades.trade_id::text)
FROM trades
JOIN orders AS buy_order ON buy_order.order_id = trades.buy_order_id
JOIN orders AS sell_order ON sell_order.order_id = trades.sell_order_id
ON CONFLICT (trade_execution_id) DO NOTHING;

INSERT INTO trade_allocations (
    trade_allocation_id,
    trade_execution_id,
    trading_account_id,
    side,
    allocated_quantity,
    allocation_status,
    created_at
)
SELECT
    uuid_generate_v4(),
    trade_execution_id,
    buy_trading_account_id,
    'Buy',
    quantity,
    'Allocated',
    executed_at
FROM trade_executions
WHERE buy_trading_account_id IS NOT NULL
UNION ALL
SELECT
    uuid_generate_v4(),
    trade_execution_id,
    sell_trading_account_id,
    'Sell',
    quantity,
    'Allocated',
    executed_at
FROM trade_executions
WHERE sell_trading_account_id IS NOT NULL;
