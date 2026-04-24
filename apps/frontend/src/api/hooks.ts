// ══════════════════════════════════════════════════════════
// TanStack Query hooks — data fetching with caching
// ══════════════════════════════════════════════════════════

import { useQuery } from '@tanstack/react-query'
import * as queryApi from './queryApi'
import * as gatewayApi from './gatewayApi'

// ── Query API hooks ──────────────────────────────────────

export function useInstruments() {
  return useQuery({
    queryKey: ['instruments'],
    queryFn: queryApi.getInstruments,
    staleTime: 60_000,
  })
}

export function useTicker(symbol: string) {
  return useQuery({
    queryKey: ['ticker', symbol],
    queryFn: () => queryApi.getTicker(symbol),
    enabled: !!symbol,
    refetchInterval: 5_000,
  })
}

export function useCandles(symbol: string, interval = '1m', limit = 300) {
  return useQuery({
    queryKey: ['candles', symbol, interval, limit],
    queryFn: () => queryApi.getCandles(symbol, interval, limit),
    enabled: !!symbol,
    staleTime: 30_000,
  })
}

export function useOrderBook(symbol: string) {
  return useQuery({
    queryKey: ['order-book', symbol],
    queryFn: () => queryApi.getOrderBook(symbol),
    enabled: !!symbol,
    refetchInterval: 2_000,
  })
}

export function useMarketOverview() {
  return useQuery({
    queryKey: ['market-overview'],
    queryFn: queryApi.getMarketOverview,
    refetchInterval: 5_000,
  })
}

export function useRecentTrades(symbol: string, limit = 50) {
  return useQuery({
    queryKey: ['recent-trades', symbol, limit],
    queryFn: () => queryApi.getRecentTrades(symbol, limit),
    enabled: !!symbol,
    refetchInterval: 5_000,
  })
}

export function usePositions(tradingAccountId?: string) {
  return useQuery({
    queryKey: ['positions', tradingAccountId],
    queryFn: () => queryApi.getPositions(tradingAccountId),
    refetchInterval: 10_000,
  })
}

export function useOrderHistory(accountId?: string) {
  return useQuery({
    queryKey: ['order-history', accountId],
    queryFn: () => queryApi.getOrderHistory(accountId),
    refetchInterval: 5_000,
  })
}

export function useEnrichedOrders(accountId?: string) {
  return useQuery({
    queryKey: ['enriched-orders', accountId],
    queryFn: () => queryApi.getEnrichedOrders(accountId),
    refetchInterval: 5_000,
  })
}

// ── Gateway API hooks ────────────────────────────────────

export function useAccounts() {
  return useQuery({
    queryKey: ['accounts'],
    queryFn: gatewayApi.getAccounts,
    staleTime: 60_000,
  })
}

export function useAccountBalances(accountId: string) {
  return useQuery({
    queryKey: ['account-balances', accountId],
    queryFn: () => gatewayApi.getAccountBalances(accountId),
    enabled: !!accountId,
    refetchInterval: 10_000,
  })
}
