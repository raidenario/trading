// ══════════════════════════════════════════════════════════
// Query API client — read-side projections
// ══════════════════════════════════════════════════════════
// Base path uses the Vite proxy: /query-api → http://localhost:5267
// The proxy rewrites the /query-api prefix away.

import { fetchJson } from './client'
import type {
  InstrumentSnapshot,
  CandleSnapshot,
  TickerWithCandle,
  MarketOverviewItem,
  RecentTrade,
  EnrichedTrade,
  PositionSnapshot,
  OrderHistoryItem,
  EnrichedOrderView,
} from '../types'

const BASE = '/query-api'

export const getInstruments = () =>
  fetchJson<InstrumentSnapshot[]>(`${BASE}/api/instruments`)

export const getTicker = (symbol: string) =>
  fetchJson<TickerWithCandle>(`${BASE}/api/markets/${encodeURIComponent(symbol)}/ticker`)

export const getCandles = (symbol: string, interval = '1m', limit = 300) =>
  fetchJson<CandleSnapshot[]>(
    `${BASE}/api/markets/${encodeURIComponent(symbol)}/candles?interval=${encodeURIComponent(interval)}&limit=${limit}`
  )

export const getMarketOverview = () =>
  fetchJson<MarketOverviewItem[]>(`${BASE}/api/markets/overview`)

export const getRecentTrades = (symbol: string, limit = 50) =>
  fetchJson<RecentTrade[]>(`${BASE}/api/trades/recent?symbol=${encodeURIComponent(symbol)}&limit=${limit}`)

export const getEnrichedTrades = (symbol: string, limit = 50) =>
  fetchJson<EnrichedTrade[]>(`${BASE}/api/trades/enriched?symbol=${encodeURIComponent(symbol)}&limit=${limit}`)

export const getPositions = (tradingAccountId?: string) => {
  const qs = tradingAccountId ? `?tradingAccountId=${tradingAccountId}` : ''
  return fetchJson<PositionSnapshot[]>(`${BASE}/api/positions${qs}`)
}

export const getOrderHistory = (accountId?: string) => {
  const qs = accountId ? `?accountId=${accountId}` : ''
  return fetchJson<OrderHistoryItem[]>(`${BASE}/api/history/orders${qs}`)
}

export const getEnrichedOrders = (accountId?: string) => {
  const qs = accountId ? `?accountId=${accountId}` : ''
  return fetchJson<EnrichedOrderView[]>(`${BASE}/api/orders/enriched${qs}`)
}

export const getBalances = (accountId: string) =>
  fetchJson<Array<{ accountId: string; asset: string; available: number; reserved: number; asOf: string }>>(
    `${BASE}/api/balances/${accountId}`
  )
