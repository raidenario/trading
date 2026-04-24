// ══════════════════════════════════════════════════════════
// Gateway API client — commands (orders, accounts, funding)
// ══════════════════════════════════════════════════════════
// Base path uses the Vite proxy: /api → http://localhost:5103

import { fetchJson, postJson } from './client'
import { config } from '../config'
import type {
  AccountSummary,
  AccountBalance,
  CreateOrderPayload,
  CreateOrderResponse,
  OrderHistoryItem,
} from '../types'

export const getAccounts = () =>
  fetchJson<AccountSummary[]>(`${config.gatewayApiBase}/accounts`)

export const getAccountBalances = (accountId: string) =>
  fetchJson<AccountBalance[]>(`${config.gatewayApiBase}/accounts/${accountId}/balances`)

export const createOrder = (payload: CreateOrderPayload) =>
  postJson<CreateOrderResponse>(`${config.gatewayApiBase}/orders`, payload)

export const cancelOrder = (orderId: string, accountId: string, symbol: string) =>
  postJson<{ orderId: string; status: string }>(`${config.gatewayApiBase}/orders/${orderId}/cancel`, {
    accountId,
    symbol,
    requestedAt: new Date().toISOString(),
  })

export const getOrders = (accountId?: string) => {
  const qs = accountId ? `?accountId=${accountId}` : ''
  return fetchJson<OrderHistoryItem[]>(`${config.gatewayApiBase}/orders${qs}`)
}

export const fundAccount = (accountId: string, asset: string, amount: number) =>
  postJson<unknown>(`${config.gatewayApiBase}/accounts/${accountId}/fund`, {
    asset,
    amount,
    referenceId: `web-${Date.now()}`,
  })
