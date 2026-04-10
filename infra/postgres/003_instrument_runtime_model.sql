-- Exchange Platform - instrument runtime model
-- Adds instrument-aware trading rules, session config and runtime status.

CREATE TABLE instrument_trading_rules (
    instrument_id          UUID PRIMARY KEY REFERENCES instruments(instrument_id),
    rule_profile           VARCHAR(32)   NOT NULL,
    min_quantity           DECIMAL(28,8) NOT NULL,
    max_quantity           DECIMAL(28,8),
    tick_size              DECIMAL(28,8) NOT NULL,
    lot_size               DECIMAL(28,8) NOT NULL,
    price_precision        INTEGER       NOT NULL,
    quantity_precision     INTEGER       NOT NULL,
    allowed_order_types    JSONB         NOT NULL DEFAULT '[]'::jsonb,
    allowed_sessions       JSONB         NOT NULL DEFAULT '[]'::jsonb,
    matching_enabled       BOOLEAN       NOT NULL DEFAULT TRUE
);

CREATE TABLE instrument_market_config (
    instrument_id                UUID PRIMARY KEY REFERENCES instruments(instrument_id),
    regular_session_start        TIME NOT NULL,
    regular_session_end          TIME NOT NULL,
    after_market_session_start   TIME,
    after_market_session_end     TIME,
    auction_session_start        TIME,
    auction_session_end          TIME,
    separate_book                BOOLEAN NOT NULL DEFAULT FALSE
);

CREATE TABLE instrument_status (
    instrument_id    UUID PRIMARY KEY REFERENCES instruments(instrument_id),
    trading_status   VARCHAR(32)  NOT NULL,
    updated_at       TIMESTAMPTZ  NOT NULL,
    notes            TEXT
);

INSERT INTO instruments (
    instrument_id, symbol, asset_class, segment, market, isin, base_asset, quote_asset,
    price_precision, quantity_precision, tick_size, lot_size, trading_status,
    settlement_type, delivery_type, payment_type, metadata
)
VALUES
    ('aaaaaaaa-0000-0000-0000-000000000101', 'PETR4', 'Equity', 'Cash', 'Spot', 'BRPETRACNOR9', 'PETR4', 'BRL', 2, 0, 0.01, 100, 'Active', 'Spot', 'Physical', 'Dvp', '{"country":"BR","book":"cash"}'::jsonb),
    ('aaaaaaaa-0000-0000-0000-000000000102', 'VALE3', 'Equity', 'Cash', 'Spot', 'BRVALEACNOR0', 'VALE3', 'BRL', 2, 0, 0.01, 100, 'Active', 'Spot', 'Physical', 'Dvp', '{"country":"BR","book":"cash"}'::jsonb),
    ('aaaaaaaa-0000-0000-0000-000000000103', 'ITUB4', 'Equity', 'Cash', 'Spot', 'BRITUBACNPR1', 'ITUB4', 'BRL', 2, 0, 0.01, 100, 'Active', 'Spot', 'Physical', 'Dvp', '{"country":"BR","book":"cash"}'::jsonb),
    ('aaaaaaaa-0000-0000-0000-000000000111', 'PETR4F', 'Equity', 'Cash', 'Spot', NULL, 'PETR4', 'BRL', 2, 0, 0.01, 1, 'Active', 'Spot', 'Physical', 'Dvp', '{"country":"BR","book":"fractional"}'::jsonb),
    ('aaaaaaaa-0000-0000-0000-000000000201', 'BOVA11', 'Etf', 'Cash', 'Spot', 'BRBOVAETF000', 'BOVA11', 'BRL', 2, 0, 0.01, 1, 'Active', 'Spot', 'Physical', 'Dvp', '{"country":"BR","theme":"broad-market"}'::jsonb),
    ('aaaaaaaa-0000-0000-0000-000000000202', 'SMAL11', 'Etf', 'Cash', 'Spot', 'BRSMALETF000', 'SMAL11', 'BRL', 2, 0, 0.01, 1, 'Halted', 'Spot', 'Physical', 'Dvp', '{"country":"BR","theme":"small-caps"}'::jsonb),
    ('aaaaaaaa-0000-0000-0000-000000000203', 'IVVB11', 'Etf', 'Cash', 'Spot', 'BRIVVBETF000', 'IVVB11', 'BRL', 2, 0, 0.01, 1, 'Active', 'Spot', 'Physical', 'Dvp', '{"country":"BR","theme":"international"}'::jsonb),
    ('aaaaaaaa-0000-0000-0000-000000000301', 'AAPL34', 'Bdr', 'Cash', 'Spot', 'BRAAPLBDR004', 'AAPL', 'BRL', 2, 0, 0.01, 1, 'AfterMarketOnly', 'Spot', 'Physical', 'Dvp', '{"country":"BR","underlying":"AAPL"}'::jsonb),
    ('aaaaaaaa-0000-0000-0000-000000000302', 'MSFT34', 'Bdr', 'Cash', 'Spot', 'BRMSFTBDR004', 'MSFT', 'BRL', 2, 0, 0.01, 1, 'Active', 'Spot', 'Physical', 'Dvp', '{"country":"BR","underlying":"MSFT"}'::jsonb),
    ('aaaaaaaa-0000-0000-0000-000000000303', 'GOGL34', 'Bdr', 'Cash', 'Spot', 'BRGOGLBDR004', 'GOOGL', 'BRL', 2, 0, 0.01, 1, 'Auction', 'Spot', 'Physical', 'Dvp', '{"country":"BR","underlying":"GOOGL"}'::jsonb),
    ('aaaaaaaa-0000-0000-0000-000000000401', 'USD-BRL', 'Fx', 'Cash', 'Spot', NULL, 'USD', 'BRL', 4, 0, 0.0001, 1, 'Active', 'Spot', 'Cash', 'Dvp', '{"country":"BR","marketType":"simulated-fx"}'::jsonb),
    ('aaaaaaaa-0000-0000-0000-000000000501', 'GOLD-SPOT', 'Commodity', 'Cash', 'Spot', NULL, 'XAU', 'USD', 2, 3, 0.05, 0.001, 'Disabled', 'Spot', 'Cash', 'Dvp', '{"synthetic":"true","commodity":"gold"}'::jsonb)
ON CONFLICT (instrument_id) DO NOTHING;

INSERT INTO instrument_trading_rules (
    instrument_id, rule_profile, min_quantity, max_quantity, tick_size, lot_size,
    price_precision, quantity_precision, allowed_order_types, allowed_sessions, matching_enabled
)
VALUES
    ('aaaaaaaa-0000-0000-0000-000000000001', 'SpotStandard', 0.00000001, 100.00000000, 0.01, 0.00000001, 2, 8, '["Limit","Market"]'::jsonb, '["Regular"]'::jsonb, TRUE),
    ('aaaaaaaa-0000-0000-0000-000000000002', 'SpotStandard', 0.00000001, 1000.00000000, 0.01, 0.00000001, 2, 8, '["Limit","Market"]'::jsonb, '["Regular"]'::jsonb, TRUE),
    ('aaaaaaaa-0000-0000-0000-000000000003', 'SpotStandard', 0.00000001, 1000.00000000, 0.0001, 0.00000001, 4, 8, '["Limit","Market"]'::jsonb, '["Regular"]'::jsonb, TRUE),
    ('aaaaaaaa-0000-0000-0000-000000000101', 'SpotStandard', 100, 1000000, 0.01, 100, 2, 0, '["Limit","Market"]'::jsonb, '["Regular"]'::jsonb, TRUE),
    ('aaaaaaaa-0000-0000-0000-000000000102', 'SpotStandard', 100, 1000000, 0.01, 100, 2, 0, '["Limit","Market"]'::jsonb, '["Regular"]'::jsonb, TRUE),
    ('aaaaaaaa-0000-0000-0000-000000000103', 'SpotStandard', 100, 1000000, 0.01, 100, 2, 0, '["Limit","Market"]'::jsonb, '["Regular"]'::jsonb, TRUE),
    ('aaaaaaaa-0000-0000-0000-000000000111', 'SpotFractional', 1, 100000, 0.01, 1, 2, 0, '["Limit","Market"]'::jsonb, '["Regular"]'::jsonb, TRUE),
    ('aaaaaaaa-0000-0000-0000-000000000201', 'SpotExtendedHours', 1, 500000, 0.01, 1, 2, 0, '["Limit","Market"]'::jsonb, '["Regular","AfterMarket"]'::jsonb, TRUE),
    ('aaaaaaaa-0000-0000-0000-000000000202', 'SpotExtendedHours', 1, 500000, 0.01, 1, 2, 0, '["Limit","Market"]'::jsonb, '["Regular","AfterMarket"]'::jsonb, TRUE),
    ('aaaaaaaa-0000-0000-0000-000000000203', 'SpotExtendedHours', 1, 500000, 0.01, 1, 2, 0, '["Limit","Market"]'::jsonb, '["Regular","AfterMarket"]'::jsonb, TRUE),
    ('aaaaaaaa-0000-0000-0000-000000000301', 'SpotExtendedHours', 1, 500000, 0.01, 1, 2, 0, '["Limit","Market"]'::jsonb, '["Regular","AfterMarket"]'::jsonb, TRUE),
    ('aaaaaaaa-0000-0000-0000-000000000302', 'SpotExtendedHours', 1, 500000, 0.01, 1, 2, 0, '["Limit","Market"]'::jsonb, '["Regular","AfterMarket"]'::jsonb, TRUE),
    ('aaaaaaaa-0000-0000-0000-000000000303', 'SpotExtendedHours', 1, 500000, 0.01, 1, 2, 0, '["Limit","Market"]'::jsonb, '["Auction"]'::jsonb, TRUE),
    ('aaaaaaaa-0000-0000-0000-000000000401', 'SpotStandard', 1, 1000000, 0.0001, 1, 4, 0, '["Limit","Market"]'::jsonb, '["Regular"]'::jsonb, TRUE),
    ('aaaaaaaa-0000-0000-0000-000000000501', 'Disabled', 0.001, 10000, 0.05, 0.001, 2, 3, '["Limit"]'::jsonb, '["Closed"]'::jsonb, FALSE)
ON CONFLICT (instrument_id) DO UPDATE SET
    rule_profile = EXCLUDED.rule_profile,
    min_quantity = EXCLUDED.min_quantity,
    max_quantity = EXCLUDED.max_quantity,
    tick_size = EXCLUDED.tick_size,
    lot_size = EXCLUDED.lot_size,
    price_precision = EXCLUDED.price_precision,
    quantity_precision = EXCLUDED.quantity_precision,
    allowed_order_types = EXCLUDED.allowed_order_types,
    allowed_sessions = EXCLUDED.allowed_sessions,
    matching_enabled = EXCLUDED.matching_enabled;

INSERT INTO instrument_market_config (
    instrument_id, regular_session_start, regular_session_end,
    after_market_session_start, after_market_session_end,
    auction_session_start, auction_session_end, separate_book
)
VALUES
    ('aaaaaaaa-0000-0000-0000-000000000001', '00:00', '23:59', NULL, NULL, NULL, NULL, FALSE),
    ('aaaaaaaa-0000-0000-0000-000000000002', '00:00', '23:59', NULL, NULL, NULL, NULL, FALSE),
    ('aaaaaaaa-0000-0000-0000-000000000003', '00:00', '23:59', NULL, NULL, NULL, NULL, FALSE),
    ('aaaaaaaa-0000-0000-0000-000000000101', '13:00', '20:00', NULL, NULL, '12:45', '13:00', FALSE),
    ('aaaaaaaa-0000-0000-0000-000000000102', '13:00', '20:00', NULL, NULL, '12:45', '13:00', FALSE),
    ('aaaaaaaa-0000-0000-0000-000000000103', '13:00', '20:00', NULL, NULL, '12:45', '13:00', FALSE),
    ('aaaaaaaa-0000-0000-0000-000000000111', '13:00', '20:00', NULL, NULL, '12:45', '13:00', TRUE),
    ('aaaaaaaa-0000-0000-0000-000000000201', '13:00', '20:00', '20:00', '21:30', '12:45', '13:00', FALSE),
    ('aaaaaaaa-0000-0000-0000-000000000202', '13:00', '20:00', '20:00', '21:30', '12:45', '13:00', FALSE),
    ('aaaaaaaa-0000-0000-0000-000000000203', '13:00', '20:00', '20:00', '21:30', '12:45', '13:00', FALSE),
    ('aaaaaaaa-0000-0000-0000-000000000301', '13:00', '20:00', '20:00', '21:30', '12:45', '13:00', FALSE),
    ('aaaaaaaa-0000-0000-0000-000000000302', '13:00', '20:00', '20:00', '21:30', '12:45', '13:00', FALSE),
    ('aaaaaaaa-0000-0000-0000-000000000303', '13:00', '20:00', '20:00', '21:30', '12:45', '13:00', FALSE),
    ('aaaaaaaa-0000-0000-0000-000000000401', '13:00', '20:00', NULL, NULL, NULL, NULL, FALSE),
    ('aaaaaaaa-0000-0000-0000-000000000501', '13:00', '20:00', NULL, NULL, NULL, NULL, FALSE)
ON CONFLICT (instrument_id) DO UPDATE SET
    regular_session_start = EXCLUDED.regular_session_start,
    regular_session_end = EXCLUDED.regular_session_end,
    after_market_session_start = EXCLUDED.after_market_session_start,
    after_market_session_end = EXCLUDED.after_market_session_end,
    auction_session_start = EXCLUDED.auction_session_start,
    auction_session_end = EXCLUDED.auction_session_end,
    separate_book = EXCLUDED.separate_book;

INSERT INTO instrument_status (instrument_id, trading_status, updated_at, notes)
VALUES
    ('aaaaaaaa-0000-0000-0000-000000000001', 'Active', '2026-04-01T00:00:00Z', NULL),
    ('aaaaaaaa-0000-0000-0000-000000000002', 'Active', '2026-04-01T00:00:00Z', NULL),
    ('aaaaaaaa-0000-0000-0000-000000000003', 'Active', '2026-04-01T00:00:00Z', NULL),
    ('aaaaaaaa-0000-0000-0000-000000000101', 'Active', '2026-04-01T00:00:00Z', NULL),
    ('aaaaaaaa-0000-0000-0000-000000000102', 'Active', '2026-04-01T00:00:00Z', NULL),
    ('aaaaaaaa-0000-0000-0000-000000000103', 'Active', '2026-04-01T00:00:00Z', NULL),
    ('aaaaaaaa-0000-0000-0000-000000000111', 'Active', '2026-04-01T00:00:00Z', NULL),
    ('aaaaaaaa-0000-0000-0000-000000000201', 'Active', '2026-04-01T00:00:00Z', NULL),
    ('aaaaaaaa-0000-0000-0000-000000000202', 'Halted', '2026-04-01T00:00:00Z', 'Study-project halt scenario'),
    ('aaaaaaaa-0000-0000-0000-000000000203', 'Active', '2026-04-01T00:00:00Z', NULL),
    ('aaaaaaaa-0000-0000-0000-000000000301', 'AfterMarketOnly', '2026-04-01T00:00:00Z', 'Simplified after-market only mode'),
    ('aaaaaaaa-0000-0000-0000-000000000302', 'Active', '2026-04-01T00:00:00Z', NULL),
    ('aaaaaaaa-0000-0000-0000-000000000303', 'Auction', '2026-04-01T00:00:00Z', 'Auction placeholder'),
    ('aaaaaaaa-0000-0000-0000-000000000401', 'Active', '2026-04-01T00:00:00Z', NULL),
    ('aaaaaaaa-0000-0000-0000-000000000501', 'Disabled', '2026-04-01T00:00:00Z', 'Disabled commodity book')
ON CONFLICT (instrument_id) DO UPDATE SET
    trading_status = EXCLUDED.trading_status,
    updated_at = EXCLUDED.updated_at,
    notes = EXCLUDED.notes;
