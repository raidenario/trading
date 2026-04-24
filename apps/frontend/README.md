# Exchange Frontend

Trading dashboard for the Exchange Platform simulation. Connects to the Gateway API, Query API, and Realtime Gateway to display live market data, submit orders, and monitor portfolio.

## Architecture

```
Frontend (React/Vite)
  │
  ├── HTTP → Gateway API (:5103) — order submission, accounts
  ├── HTTP → Query API (:5267)   — instruments, tickers, trades, positions
  └── WS  → Realtime Gateway (:4000/socket) — Phoenix Channels
                                                market:{SYMBOL}
```

## Quick Start

```bash
# Install dependencies
npm install

# Copy env template
cp .env.example .env

# Start dev server (port 3000)
npm run dev
```

## Prerequisites

All backend services must be running:
- **Gateway API** on `http://localhost:5103`
- **Query API** on `http://localhost:5267`
- **Realtime Gateway** on `http://localhost:4000`

Use `start-local.bat` from the project root to launch all services.

## Scripts

| Command | Description |
|---------|-------------|
| `npm run dev` | Start Vite dev server |
| `npm run build` | TypeScript check + production build |
| `npm run test` | Run all tests (Vitest) |
| `npm run test:watch` | Run tests in watch mode |
| `npm run typecheck` | TypeScript type checking only |

## Project Structure

```
src/
├── api/              HTTP clients and TanStack Query hooks
│   ├── client.ts     Generic fetch wrapper with error handling
│   ├── queryApi.ts   Query API endpoints
│   ├── gatewayApi.ts Gateway API endpoints
│   └── hooks.ts      React Query hooks
├── realtime/         Phoenix WebSocket integration
│   ├── socket.ts     Socket singleton
│   ├── useMarketChannel.ts  Channel subscription hook
│   └── useEventTape.ts      Event log hook
├── components/       UI panels
│   ├── Sidebar.tsx       Instrument watchlist
│   ├── TickerBar.tsx     Market price ribbon
│   ├── TickerPanel.tsx   Asset detail (bid/ask/volume)
│   ├── CandleChart.tsx   OHLC chart (lightweight-charts)
│   ├── OrderBook.tsx     Bid/ask depth visualization
│   ├── TradesFeed.tsx    Recent trades table
│   ├── OrderTicket.tsx   Order entry form
│   ├── PortfolioPanel.tsx Positions + balances
│   ├── OrderHistory.tsx  Order history table
│   └── EventTape.tsx     Realtime event log
├── __tests__/        Test suites
│   ├── api.test.ts       API client tests
│   ├── realtime.test.ts  Realtime hook tests
│   └── components.test.tsx Component render tests
├── types.ts          Shared TypeScript interfaces
├── config.ts         Environment configuration
├── App.tsx           Main layout orchestrator
├── main.tsx          React entry point
└── index.css         Design system
```

## Endpoints Used

### Query API (`/query-api` via Vite proxy)
- `GET /api/instruments` — instrument catalog
- `GET /api/markets/{symbol}/ticker` — ticker + 1m candle
- `GET /api/markets/{symbol}/candles?interval=1m&limit=` — historical candle series
- `GET /api/markets/overview` — all market summaries
- `GET /api/trades/recent?symbol=&limit=` — recent trades
- `GET /api/positions` — trading positions
- `GET /api/orders/enriched?accountId=` — enriched order history

### Gateway API (`/api` via Vite proxy)
- `POST /api/orders` — submit orders
- `GET /api/accounts` — list accounts
- `GET /api/accounts/{id}/balances` — account balances

### Realtime Gateway (WebSocket)
- Topic: `market:{SYMBOL}` (e.g., `market:PETR4`)
- Events: `ticker_update`, `trade_update`, `book_update`, `candle_update`

## Known Limitations

1. **PnL unavailable** — The Portfolio panel cannot compute PnL because the API does not provide a mark-to-market price per position.

2. **Order Book requires realtime** — The order book populates from `book_update` events. Without the Realtime Gateway running, the book will show "No order book data."

## Technology

- React 19 + TypeScript
- Vite 6 (dev server + bundler)
- TanStack Query (data fetching/caching)
- lightweight-charts (TradingView candle charts)
- Phoenix JS client (realtime WebSocket)
- Vitest + Testing Library (tests)
