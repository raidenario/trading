-- Exchange Platform - Initial Database Schema
-- This migration creates the core tables for the exchange platform.

CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- =====================================================
-- Accounts
-- =====================================================
CREATE TABLE accounts (
    account_id   UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    display_name VARCHAR(128) NOT NULL,
    email        VARCHAR(256) NOT NULL UNIQUE,
    status       VARCHAR(20)  NOT NULL DEFAULT 'Active',
    created_at   TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_at   TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

-- =====================================================
-- Balances (source of truth for account funds)
-- =====================================================
CREATE TABLE balances (
    balance_id   UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    account_id   UUID         NOT NULL REFERENCES accounts(account_id),
    asset        VARCHAR(10)  NOT NULL,
    available    DECIMAL(28,8) NOT NULL DEFAULT 0,
    reserved     DECIMAL(28,8) NOT NULL DEFAULT 0,
    updated_at   TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    UNIQUE(account_id, asset)
);

-- =====================================================
-- Orders
-- =====================================================
CREATE TABLE orders (
    order_id           UUID PRIMARY KEY,
    account_id         UUID         NOT NULL REFERENCES accounts(account_id),
    symbol             VARCHAR(20)  NOT NULL,
    side               VARCHAR(4)   NOT NULL CHECK (side IN ('Buy', 'Sell')),
    order_type         VARCHAR(10)  NOT NULL CHECK (order_type IN ('Limit', 'Market')),
    time_in_force      VARCHAR(3)   NOT NULL CHECK (time_in_force IN ('Gtc', 'Ioc', 'Fok')),
    quantity           DECIMAL(28,8) NOT NULL,
    limit_price        DECIMAL(28,8),
    filled_quantity    DECIMAL(28,8) NOT NULL DEFAULT 0,
    remaining_quantity DECIMAL(28,8) NOT NULL,
    status             VARCHAR(20)  NOT NULL DEFAULT 'Pending',
    rejection_reason   TEXT,
    client_order_id    VARCHAR(64),
    created_at         TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_at         TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_orders_account_id ON orders(account_id);
CREATE INDEX idx_orders_symbol     ON orders(symbol);
CREATE INDEX idx_orders_status     ON orders(status);

-- =====================================================
-- Trades
-- =====================================================
CREATE TABLE trades (
    trade_id       UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    symbol         VARCHAR(20)  NOT NULL,
    buy_order_id   UUID         NOT NULL REFERENCES orders(order_id),
    sell_order_id  UUID         NOT NULL REFERENCES orders(order_id),
    price          DECIMAL(28,8) NOT NULL,
    quantity       DECIMAL(28,8) NOT NULL,
    executed_at    TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_trades_symbol      ON trades(symbol);
CREATE INDEX idx_trades_executed_at ON trades(executed_at DESC);

-- =====================================================
-- Ledger Entries (immutable audit log)
-- =====================================================
CREATE TABLE ledger_entries (
    entry_id    UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    account_id  UUID         NOT NULL REFERENCES accounts(account_id),
    asset       VARCHAR(10)  NOT NULL,
    amount      DECIMAL(28,8) NOT NULL,
    entry_type  VARCHAR(30)  NOT NULL,
    reference   TEXT,
    created_at  TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_ledger_account ON ledger_entries(account_id);

-- =====================================================
-- Seed: Demo Accounts with Initial Balances
-- =====================================================
INSERT INTO accounts (account_id, display_name, email) VALUES
    ('11111111-1111-1111-1111-111111111111', 'Alice Trader',   'alice@exchange.local'),
    ('22222222-2222-2222-2222-222222222222', 'Bob Market',     'bob@exchange.local'),
    ('33333333-3333-3333-3333-333333333333', 'Charlie Whale',  'charlie@exchange.local');

INSERT INTO balances (account_id, asset, available, reserved) VALUES
    ('11111111-1111-1111-1111-111111111111', 'USD',  100000.00, 0),
    ('11111111-1111-1111-1111-111111111111', 'BTC',  5.0, 0),
    ('11111111-1111-1111-1111-111111111111', 'ETH',  50.0, 0),
    ('22222222-2222-2222-2222-222222222222', 'USD',  250000.00, 0),
    ('22222222-2222-2222-2222-222222222222', 'BTC',  10.0, 0),
    ('22222222-2222-2222-2222-222222222222', 'SOL',  500.0, 0),
    ('33333333-3333-3333-3333-333333333333', 'USD',  1000000.00, 0),
    ('33333333-3333-3333-3333-333333333333', 'BTC',  50.0, 0),
    ('33333333-3333-3333-3333-333333333333', 'ETH',  200.0, 0),
    ('33333333-3333-3333-3333-333333333333', 'SOL',  2000.0, 0);

INSERT INTO ledger_entries (account_id, asset, amount, entry_type, reference) VALUES
    ('11111111-1111-1111-1111-111111111111', 'USD', 100000.00, 'Deposit', 'initial-funding'),
    ('11111111-1111-1111-1111-111111111111', 'BTC', 5.0,       'Deposit', 'initial-funding'),
    ('11111111-1111-1111-1111-111111111111', 'ETH', 50.0,      'Deposit', 'initial-funding'),
    ('22222222-2222-2222-2222-222222222222', 'USD', 250000.00, 'Deposit', 'initial-funding'),
    ('22222222-2222-2222-2222-222222222222', 'BTC', 10.0,      'Deposit', 'initial-funding'),
    ('22222222-2222-2222-2222-222222222222', 'SOL', 500.0,     'Deposit', 'initial-funding'),
    ('33333333-3333-3333-3333-333333333333', 'USD', 1000000.00,'Deposit', 'initial-funding'),
    ('33333333-3333-3333-3333-333333333333', 'BTC', 50.0,      'Deposit', 'initial-funding'),
    ('33333333-3333-3333-3333-333333333333', 'ETH', 200.0,     'Deposit', 'initial-funding'),
    ('33333333-3333-3333-3333-333333333333', 'SOL', 2000.0,    'Deposit', 'initial-funding');
