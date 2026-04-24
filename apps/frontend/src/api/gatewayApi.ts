// ══════════════════════════════════════════════════════════
// Gateway API client — commands (orders, accounts, funding)
// ══════════════════════════════════════════════════════════
// Base path uses the Vite proxy: /api → http://localhost:5103

import { fetchJson, postJson } from './client'
import type {
  AccountSummary,
  AccountBalance,
  CreateOrderPayload,
  CreateOrderResponse,
  OrderHistoryItem,
} from '../types'

export const getAccounts = () =>
  fetchJson<AccountSummary[]>('/api/accounts')

export const getAccountBalances = (accountId: string) =>
  fetchJson<AccountBalance[]>(`/api/accounts/${accountId}/balances`)

export const createOrder = (payload: CreateOrderPayload) =>
  postJson<CreateOrderResponse>('/api/orders', payload)

export const cancelOrder = (orderId: string, accountId: string, symbol: string) =>
  postJson<{ orderId: string; status: string }>(`/api/orders/${orderId}/cancel`, {
    accountId,
    symbol,
    requestedAt: new Date().toISOString(),
  })

export const getOrders = (accountId?: string) => {
  const qs = accountId ? `?accountId=${accountId}` : ''
  return fetchJson<OrderHistoryItem[]>(`/api/orders${qs}`)
}

export const fundAccount = (accountId: string, asset: string, amount: number) =>
  postJson<unknown>(`/api/accounts/${accountId}/fund`, {
    asset,
    amount,
    referenceId: `web-${Date.now()}`,
  })
