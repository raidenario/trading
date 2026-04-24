// ══════════════════════════════════════════════════════════
// App.tsx — Main application shell
// ══════════════════════════════════════════════════════════
// Orchestrates all panels, realtime subscriptions,
// and data fetching. Desktop = sidebar + grid layout.

import { useState, useCallback, useMemo } from 'react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'

import {
  useInstruments,
  useMarketOverview,
  useTicker,
  useCandles,
  useRecentTrades,
  usePositions,
  useAccounts,
  useAccountBalances,
  useEnrichedOrders,
} from './api/hooks'
import { useMarketChannel } from './realtime/useMarketChannel'
import { useEventTape } from './realtime/useEventTape'

import { Sidebar } from './components/Sidebar'
import { TickerBar } from './components/TickerBar'
import { TickerPanel } from './components/TickerPanel'
import { CandleChart } from './components/CandleChart'
import { OrderBook } from './components/OrderBook'
import { TradesFeed } from './components/TradesFeed'
import { OrderTicket } from './components/OrderTicket'
import { PortfolioPanel } from './components/PortfolioPanel'
import { OrderHistory } from './components/OrderHistory'
import { EventTape } from './components/EventTape'

import type {
  BookSnapshot,
  RealtimeTickerUpdate,
  RealtimeTradeUpdate,
  RealtimeBookUpdate,
  RealtimeCandleUpdate,
  CandleSnapshot,
  RecentTrade,
  TickerWithCandle,
} from './types'

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      retry: 1,
      refetchOnWindowFocus: false,
    },
  },
})

type Tab = 'trading' | 'portfolio' | 'orders'

export default function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <AppInner />
    </QueryClientProvider>
  )
}

function AppInner() {
  // ── Global state ─────────────────────────────────────────
  const [selectedSymbol, setSelectedSymbol] = useState('PETR4')
  const [activeTab, setActiveTab] = useState<Tab>('trading')
  const [realtimeConnected, setRealtimeConnected] = useState(false)

  // ── Data fetching ────────────────────────────────────────
  const { data: instruments = [] } = useInstruments()
  const { data: overview = [] } = useMarketOverview()
  const { data: accounts = [] } = useAccounts()
  const selectedAccount = accounts[0]?.accountId ?? ''
  const { data: tickerData, isLoading: tickerLoading } = useTicker(selectedSymbol)
  const { data: historicalCandles = [] } = useCandles(selectedSymbol, '1m', 300)
  const { data: trades = [], isLoading: tradesLoading } = useRecentTrades(selectedSymbol)
  const { data: positions = [], isLoading: positionsLoading } = usePositions()
  const { data: balances = [], isLoading: balancesLoading } = useAccountBalances(selectedAccount)
  const { data: enrichedOrders = [], isLoading: ordersLoading } = useEnrichedOrders(selectedAccount)

  // ── Realtime state ───────────────────────────────────────
  const [realtimeTicker, setRealtimeTicker] = useState<RealtimeTickerUpdate | null>(null)
  const [realtimeBook, setRealtimeBook] = useState<BookSnapshot | null>(null)
  const [realtimeTrades, setRealtimeTrades] = useState<RecentTrade[]>([])
  const [realtimeCandles, setRealtimeCandles] = useState<RealtimeCandleUpdate[]>([])
  const { entries: eventTapeEntries, pushEvent } = useEventTape()

  // ── Realtime callbacks ───────────────────────────────────
  const onTicker = useCallback((data: RealtimeTickerUpdate) => {
    setRealtimeTicker(data)
    pushEvent(data.symbol, 'ticker_update', data as unknown as Record<string, unknown>)
  }, [pushEvent])

  const onTrade = useCallback((data: RealtimeTradeUpdate) => {
    const trade: RecentTrade = {
      tradeId: data.trade_id,
      symbol: data.symbol,
      price: data.price,
      quantity: data.quantity,
      side: data.side ?? 'Buy',
      executedAt: data.executed_at,
    }
    setRealtimeTrades((prev) => [trade, ...prev].slice(0, 100))
    pushEvent(data.symbol, 'trade_update', data as unknown as Record<string, unknown>)
  }, [pushEvent])

  const onBook = useCallback((data: RealtimeBookUpdate) => {
    setRealtimeBook({
      symbol: data.symbol,
      bids: data.bids,
      asks: data.asks,
      asOf: data.as_of,
    })
    pushEvent(data.symbol, 'book_update', data as unknown as Record<string, unknown>)
  }, [pushEvent])

  const onCandle = useCallback((data: RealtimeCandleUpdate) => {
    setRealtimeCandles((prev) => [...prev, data].slice(-200))
    pushEvent(data.symbol, 'candle_update', data as unknown as Record<string, unknown>)
  }, [pushEvent])

  const onJoin = useCallback(() => {
    setRealtimeConnected(true)
  }, [])

  const onError = useCallback(() => {
    setRealtimeConnected(false)
  }, [])

  // ── Subscribe to channel ─────────────────────────────────
  useMarketChannel(selectedSymbol, { onTicker, onTrade, onBook, onCandle, onJoin, onError })

  // ── Symbol change handler ────────────────────────────────
  const handleSelectSymbol = useCallback((symbol: string) => {
    setSelectedSymbol(symbol)
    setRealtimeTicker(null)
    setRealtimeBook(null)
    setRealtimeTrades([])
    setRealtimeCandles([])
    setRealtimeConnected(false)
  }, [])

  // ── Merge realtime data ──────────────────────────────────
  const mergedTicker: TickerWithCandle | null = useMemo(() => {
    if (!tickerData) return null
    if (!realtimeTicker) return tickerData
    return {
      ...tickerData,
      ticker: {
        ...tickerData.ticker,
        lastPrice: realtimeTicker.last_price,
        bestBid: realtimeTicker.best_bid,
        bestAsk: realtimeTicker.best_ask,
        volume24H: realtimeTicker.volume_24h ?? tickerData.ticker.volume24H,
        change24H: realtimeTicker.change_24h ?? tickerData.ticker.change24H,
        asOf: realtimeTicker.as_of ?? tickerData.ticker.asOf,
      },
    }
  }, [tickerData, realtimeTicker])

  const mergedTrades = useMemo(() => {
    if (realtimeTrades.length === 0) return trades
    // Prepend realtime trades, dedup by tradeId
    const ids = new Set(realtimeTrades.map((t) => t.tradeId))
    return [...realtimeTrades, ...trades.filter((t) => !ids.has(t.tradeId))].slice(0, 100)
  }, [trades, realtimeTrades])

  // ── Render ───────────────────────────────────────────────
  return (
    <div className="app-layout">
      {/* Header */}
      <header className="app-header">
        <div className="header-brand">
          <svg viewBox="0 0 24 24" fill="currentColor"><path d="M13 10V3L4 14h7v7l9-11h-7z"/></svg>
          Exchange Platform
        </div>

        <nav className="header-nav">
          <button className={activeTab === 'trading' ? 'active' : ''} onClick={() => setActiveTab('trading')} id="nav-trading">
            Trading
          </button>
          <button className={activeTab === 'portfolio' ? 'active' : ''} onClick={() => setActiveTab('portfolio')} id="nav-portfolio">
            Portfolio
          </button>
          <button className={activeTab === 'orders' ? 'active' : ''} onClick={() => setActiveTab('orders')} id="nav-orders">
            Orders
          </button>
        </nav>

        <div className="header-spacer" />

        <div className="header-status">
          <div className={`status-dot ${realtimeConnected ? 'status-dot--connected' : 'status-dot--disconnected'}`}
            title={realtimeConnected ? 'Realtime connected' : 'Realtime disconnected'} />
          <span>{realtimeConnected ? 'LIVE' : 'OFFLINE'}</span>
        </div>

        {accounts.length > 0 && (
          <div className="header-account">
            <select value={selectedAccount} id="account-selector">
              {accounts.map((a) => (
                <option key={a.accountId} value={a.accountId}>{a.displayName}</option>
              ))}
            </select>
          </div>
        )}
      </header>

      {/* Ticker Bar */}
      <TickerBar overview={overview} onSelectSymbol={handleSelectSymbol} />

      {/* Body */}
      <div className="app-body">
        {/* Sidebar */}
        <Sidebar
          instruments={instruments}
          overview={overview}
          selectedSymbol={selectedSymbol}
          onSelect={handleSelectSymbol}
        />

        {/* Main content */}
        <main className="app-main">
          {activeTab === 'trading' && (
            <TradingView
              symbol={selectedSymbol}
              accountId={selectedAccount}
              tickerData={mergedTicker}
              tickerLoading={tickerLoading}
              historicalCandles={historicalCandles}
              trades={mergedTrades}
              tradesLoading={tradesLoading}
              book={realtimeBook}
              realtimeCandles={realtimeCandles}
              eventTapeEntries={eventTapeEntries}
            />
          )}

          {activeTab === 'portfolio' && (
            <div className="panel" style={{ flex: 1 }}>
              <div className="panel-header"><span className="panel-title">Portfolio</span></div>
              <div className="panel-body">
                <PortfolioPanel
                  positions={positions}
                  balances={balances.map((b) => ({ ...b, total: b.available + b.reserved }))}
                  isLoading={positionsLoading || balancesLoading}
                />
              </div>
            </div>
          )}

          {activeTab === 'orders' && (
            <div className="panel" style={{ flex: 1 }}>
              <div className="panel-header"><span className="panel-title">Order History</span></div>
              <div className="panel-body">
                <OrderHistory orders={enrichedOrders} isLoading={ordersLoading} />
              </div>
            </div>
          )}
        </main>
      </div>
    </div>
  )
}

// ── Trading View Layout ────────────────────────────────────
interface TradingViewProps {
  symbol: string
  accountId: string
  tickerData: TickerWithCandle | null
  tickerLoading: boolean
  historicalCandles: CandleSnapshot[]
  trades: RecentTrade[]
  tradesLoading: boolean
  book: BookSnapshot | null
  realtimeCandles: RealtimeCandleUpdate[]
  eventTapeEntries: ReturnType<typeof useEventTape>['entries']
}

function TradingView({
  symbol,
  accountId,
  tickerData,
  tickerLoading,
  historicalCandles,
  trades,
  tradesLoading,
  book,
  realtimeCandles,
  eventTapeEntries,
}: TradingViewProps) {
  return (
    <>
      {/* Row 1: Ticker + Chart */}
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 300px', gap: 'var(--sp-2)' }}>
        {/* Chart + Ticker */}
        <div className="panel" style={{ minHeight: 380 }}>
          <div className="panel-header">
            <span className="panel-title">{symbol} — Chart</span>
            <span className="badge badge--muted" style={{ fontSize: 9 }}>History + realtime</span>
          </div>
          <div className="panel-body" style={{ position: 'relative' }}>
            <CandleChart
              initialCandles={historicalCandles}
              realtimeCandles={realtimeCandles}
              symbol={symbol}
            />
          </div>
        </div>

        {/* Order Ticket */}
        <div className="panel">
          <div className="panel-header"><span className="panel-title">Order Ticket</span></div>
          <div className="panel-body">
            <OrderTicket symbol={symbol} accountId={accountId} />
          </div>
        </div>
      </div>

      {/* Row 2: Ticker Detail */}
      <div className="panel">
        <div className="panel-header"><span className="panel-title">{symbol} — Ticker</span></div>
        <div className="panel-body">
          <TickerPanel data={tickerData} symbol={symbol} isLoading={tickerLoading} />
        </div>
      </div>

      {/* Row 3: Book + Trades */}
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 'var(--sp-2)' }}>
        <div className="panel" style={{ maxHeight: 400 }}>
          <div className="panel-header"><span className="panel-title">Order Book</span></div>
          <div className="panel-body" style={{ overflowY: 'auto' }}>
            <OrderBook book={book} />
          </div>
        </div>

        <div className="panel" style={{ maxHeight: 400 }}>
          <div className="panel-header"><span className="panel-title">Recent Trades</span></div>
          <div className="panel-body" style={{ overflowY: 'auto' }}>
            <TradesFeed trades={trades} isLoading={tradesLoading} />
          </div>
        </div>
      </div>

      {/* Row 4: Event Tape */}
      <div className="panel" style={{ maxHeight: 250 }}>
        <div className="panel-header">
          <span className="panel-title">Event Tape</span>
          <span className="badge badge--accent">{eventTapeEntries.length} events</span>
        </div>
        <div className="panel-body" style={{ overflowY: 'auto' }}>
          <EventTape entries={eventTapeEntries} />
        </div>
      </div>
    </>
  )
}
