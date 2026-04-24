// ══════════════════════════════════════════════════════════
// Shared TypeScript types for the Exchange Frontend
// ══════════════════════════════════════════════════════════
// All types derive from the actual backend read-model records.
// See: libs/contracts/dotnet/Exchange.Platform.Contracts/ReadModels/

// ── Accounts ─────────────────────────────────────────────

export interface AccountSummary {
  accountId: string
  displayName: string
  email: string
  createdAt: string
}

export interface AccountBalance {
  accountId: string
  asset: string
  available: number
  reserved: number
  total: number
  asOf: string
}

// ── Instruments ──────────────────────────────────────────

export interface InstrumentSnapshot {
  instrumentId: string
  symbol: string
  assetClass: string
  segment: string
  market: string
  baseAsset: string
  quoteAsset: string
  tradingStatus: string
  tickSize: number
  lotSize: number
}

// ── Ticker ───────────────────────────────────────────────

export interface TickerSnapshot {
  symbol: string
  lastPrice: number
  bestBid: number
  bestAsk: number
  high24H: number
  low24H: number
  volume24H: number
  change24H: number
  asOf: string
}

export interface CandleSnapshot {
  symbol: string
  interval: string
  open: number
  high: number
  low: number
  close: number
  volume: number
  openTime?: string
  closeTime?: string
  openedAt?: string
  closedAt?: string
}

export interface TickerWithCandle {
  ticker: TickerSnapshot
  candle: CandleSnapshot
}

// ── Trades ───────────────────────────────────────────────

export interface RecentTrade {
  tradeId: string
  symbol: string
  price: number
  quantity: number
  side: string
  executedAt: string
}

export interface EnrichedTrade {
  tradeId: string
  instrumentId: string
  symbol: string
  buyOrderId: string
  sellOrderId: string
  buyTradingAccountId: string
  sellTradingAccountId: string
  price: number
  quantity: number
  executedAt: string
}

// ── Orders ───────────────────────────────────────────────

export interface OrderHistoryItem {
  orderId: string
  accountId: string
  symbol: string
  side: string
  type: string
  status: string
  quantity: number
  filledQuantity: number
  price: number | null
  createdAt: string
  updatedAt: string
}

export interface EnrichedOrderView {
  orderId: string
  accountId: string
  tradingAccountId: string
  instrumentId: string
  symbol: string
  side: string
  type: string
  status: string
  quantity: number
  filledQuantity: number
  openQuantity: number
  price: number | null
  sourceSystem: string
  createdAt: string
  updatedAt: string
}

// ── Positions ────────────────────────────────────────────

export interface PositionSnapshot {
  positionId: string
  tradingAccountId: string
  instrumentId: string
  symbol: string
  positionDate: string
  netQuantity: number
  averageOpenPrice: number | null
  longQuantity: number
  shortQuantity: number
  updatedAt: string
}

// ── Market Overview ──────────────────────────────────────

export interface MarketOverviewItem {
  symbol: string
  lastPrice: number
  change24h: number
  changePercent24h: number
  volume24h: number
  high24h: number
  low24h: number
  asOf: string
}

// ── Order Book ───────────────────────────────────────────

export interface BookLevel {
  price: number
  quantity: number
  order_count?: number
  orderCount?: number
}

export interface BookSnapshot {
  symbol: string
  bids: BookLevel[]
  asks: BookLevel[]
  asOf?: string
}

// ── Realtime Events (snake_case from Phoenix) ────────────

export interface RealtimeTickerUpdate {
  symbol: string
  last_price: number
  best_bid: number
  best_ask: number
  volume_24h: number
  change_24h: number
  as_of: string
}

export interface RealtimeTradeUpdate {
  symbol: string
  trade_id: string
  price: number
  quantity: number
  side: string
  buy_order_id: string
  sell_order_id: string
  executed_at: string
}

export interface RealtimeBookUpdate {
  symbol: string
  bids: BookLevel[]
  asks: BookLevel[]
  as_of: string
}

export interface RealtimeCandleUpdate {
  symbol: string
  interval: string
  open: number
  high: number
  low: number
  close: number
  volume: number
  open_time?: string
  close_time?: string
  opened_at?: string
  closed_at?: string
}

// ── Event Tape ───────────────────────────────────────────

export type RealtimeEventType = 'ticker_update' | 'trade_update' | 'book_update' | 'candle_update'

export interface EventTapeEntry {
  id: string
  timestamp: string
  symbol: string
  eventType: RealtimeEventType
  payload: Record<string, unknown>
}

// ── Order Submission ─────────────────────────────────────

export interface CreateOrderPayload {
  orderId: string
  accountId: string
  symbol: string
  side: 'Buy' | 'Sell'
  type: 'Limit' | 'Market'
  quantity: number
  price?: number
  timeInForce: string
  clientOrderId: string
  submittedAt: string
  schemaVersion: number
}

export interface CreateOrderResponse {
  orderId: string
  status: string
  trades: Array<{
    tradeId: string
    price: number
    quantity: number
    executedAt: string
  }>
  book: {
    symbol: string
    bids: BookLevel[]
    asks: BookLevel[]
    asOf: string
  } | null
}
