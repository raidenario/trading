const GATEWAY = '';
const QUERY   = '/query-api';
const LEDGER  = '/ledger-api';

async function fetchJson<T>(url: string, options?: RequestInit): Promise<T> {
  const res = await fetch(url, options);
  if (!res.ok) throw new Error(`HTTP ${res.status}`);
  return res.json();
}

export interface AccountSummary {
  accountId: string;
  displayName: string;
  email: string;
  createdAt: string;
}

export interface AccountBalance {
  accountId: string;
  asset: string;
  available: number;
  reserved: number;
  total: number;
  asOf: string;
}

export interface OrderView {
  orderId: string;
  accountId: string;
  symbol: string;
  side: string;
  type: string;
  status: string;
  quantity: number;
  filledQuantity: number;
  price: number | null;
  createdAt: string;
  updatedAt: string;
}

export interface TickerData {
  ticker: {
    symbol: string;
    lastPrice: number;
    bestBid: number;
    bestAsk: number;
    high24h: number;
    low24h: number;
    volume24h: number;
    changePercent24h: number;
    asOf: string;
  };
  candle: {
    symbol: string;
    interval: string;
    open: number;
    high: number;
    low: number;
    close: number;
    volume: number;
  };
}

export interface RecentTrade {
  tradeId: string;
  symbol: string;
  price: number;
  quantity: number;
  side: string;
  executedAt: string;
}

export interface MarketOverview {
  symbol: string;
  lastPrice: number;
  change24h: number;
  changePercent24h: number;
  volume24h: number;
  high24h: number;
  low24h: number;
}

// ── Accounts ──
export const getAccounts = () =>
  fetchJson<AccountSummary[]>(`${GATEWAY}/api/accounts`);

export const getBalances = (accountId: string) =>
  fetchJson<AccountBalance[]>(`${GATEWAY}/api/accounts/${accountId}/balances`);

export const fundAccount = (accountId: string, asset: string, amount: number) =>
  fetchJson<unknown>(`${GATEWAY}/api/accounts/${accountId}/fund`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ asset, amount }),
  });

// ── Orders ──
export const createOrder = (payload: Record<string, unknown>) =>
  fetchJson<unknown>(`${GATEWAY}/api/orders`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(payload),
  });

export const getOrders = (accountId?: string) =>
  fetchJson<OrderView[]>(`${GATEWAY}/api/orders${accountId ? `?accountId=${accountId}` : ''}`);

// ── Query side ──
export const getTicker = (symbol: string) =>
  fetchJson<TickerData>(`${QUERY}/api/markets/${symbol}/ticker`);

export const getRecentTrades = (symbol: string, limit = 20) =>
  fetchJson<RecentTrade[]>(`${QUERY}/api/trades/recent?symbol=${symbol}&limit=${limit}`);

export const getMarketOverview = () =>
  fetchJson<MarketOverview[]>(`${QUERY}/api/markets/overview`);
