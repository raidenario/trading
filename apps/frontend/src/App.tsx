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
  useOrderBook,
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
import { getCandleTimestampKey } from './market/candles'

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
const STREAM_REWIND_THRESHOLD_SECONDS = 90

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
  const { data: persistedBook = null } = useOrderBook(selectedSymbol)
  const { data: trades = [], isLoading: tradesLoading } = useRecentTrades(selectedSymbol)
  const { data: positions = [], isLoading: positionsLoading } = usePositions()
  const { data: balances = [], isLoading: balancesLoading } = useAccountBalances(selectedAccount)
  const { data: enrichedOrders = [], isLoading: ordersLoading } = useEnrichedOrders(selectedAccount)

  // ── Realtime state ───────────────────────────────────────
  const [realtimeTicker, setRealtimeTicker] = useState<RealtimeTickerUpdate | null>(null)
  const [bookSnapshots, setBookSnapshots] = useState<Record<string, BookSnapshot>>({})
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
    setRealtimeTrades((prev) => mergeRealtimeTrades(prev, trade))
    pushEvent(data.symbol, 'trade_update', data as unknown as Record<string, unknown>)
  }, [pushEvent])

  const onBook = useCallback((data: RealtimeBookUpdate) => {
    const snapshot: BookSnapshot = {
      symbol: data.symbol,
      bids: data.bids,
      asks: data.asks,
      asOf: data.as_of,
    }

    setBookSnapshots((prev) => {
      const key = data.symbol.toUpperCase()
      return {
        ...prev,
        [key]: mergeBookSnapshot(prev[key], snapshot),
      }
    })
    pushEvent(data.symbol, 'book_update', data as unknown as Record<string, unknown>)
  }, [pushEvent])

  const onCandle = useCallback((data: RealtimeCandleUpdate) => {
    setRealtimeCandles((prev) => mergeRealtimeCandles(prev, data))
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
    setRealtimeTrades([])
    setRealtimeCandles([])
    setRealtimeConnected(false)
  }, [])

  const handleOrderBookUpdate = useCallback((snapshot: BookSnapshot) => {
    setBookSnapshots((prev) => {
      const key = snapshot.symbol.toUpperCase()
      return {
        ...prev,
        [key]: mergeBookSnapshot(prev[key], snapshot),
      }
    })
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

  const realtimeBook = bookSnapshots[selectedSymbol.toUpperCase()] ?? null
  const selectedBook = useMemo(
    () => selectLatestBook(realtimeBook, persistedBook),
    [realtimeBook, persistedBook],
  )

  // ── Render ───────────────────────────────────────────────
  return (
    <div className="app-layout">
      {/* Header */}
      <header className="app-header">
        <div className="header-brand">
          <svg viewBox="0 0 24 24" fill="currentColor"><path d="M13 10V3L4 14h7v7l9-11h-7z"/></svg>
          <span>EXCHANGE</span>
          <span className="header-brand__pro">PRO</span>
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
              book={selectedBook}
              realtimeCandles={realtimeCandles}
              latestTrade={realtimeTrades[0] ?? null}
              onOrderBookUpdate={handleOrderBookUpdate}
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
  latestTrade: RecentTrade | null
  onOrderBookUpdate: (snapshot: BookSnapshot) => void
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
  latestTrade,
  onOrderBookUpdate,
  eventTapeEntries,
}: TradingViewProps) {
  return (
    <>
      {/* Row 1: Ticker + Chart */}
      <div className="trading-top-grid">
        <div className="panel chart-panel">
          <div className="chart-panel__header">
            <div className="chart-title">
              <span>{symbol}</span>
              <button className="icon-button icon-button--star" type="button" aria-label="Favorite symbol">☆</button>
            </div>
            <div className="chart-actions">
              <button className="chart-action" type="button">Indicators</button>
              <span className="chart-realtime">Realtime <span className="status-dot status-dot--connected" /></span>
              <button className="icon-button" type="button" aria-label="Fullscreen">⛶</button>
            </div>
          </div>
          <div className="chart-toolbar" aria-label="Chart tools">
            {['1m', '5m', '15m', '1H', '4H', '1D'].map((period) => (
              <button key={period} className={`chart-tool${period === '1m' ? ' active' : ''}`} type="button">
                {period}
              </button>
            ))}
            <span className="chart-toolbar__separator" />
            <button className="icon-button" type="button" aria-label="Candles">⌗</button>
            <button className="icon-button" type="button" aria-label="Indicators">ƒx</button>
            <button className="icon-button" type="button" aria-label="Drawing tools">✎</button>
            <button className="icon-button" type="button" aria-label="More tools">•••</button>
          </div>
          <div className="panel-body chart-panel__body">
            <CandleChart
              initialCandles={historicalCandles}
              realtimeCandles={realtimeCandles}
              latestTrade={latestTrade}
              symbol={symbol}
            />
          </div>
        </div>

        <div className="panel">
          <div className="panel-header">
            <span className="panel-title">Order Ticket</span>
            <button className="icon-button" type="button" aria-label="Order ticket settings">⚙</button>
          </div>
          <div className="panel-body">
            <OrderTicket symbol={symbol} accountId={accountId} onBookUpdate={onOrderBookUpdate} />
          </div>
        </div>
      </div>

      <div className="panel ticker-strip-panel">
        <div className="panel-body">
          <TickerPanel data={tickerData} symbol={symbol} isLoading={tickerLoading} />
        </div>
      </div>

      <div className="market-grid">
        <div className="panel market-panel">
          <div className="panel-header">
            <span className="panel-title">Order Book</span>
            <button className="icon-button" type="button" aria-label="Order book display">⌁</button>
          </div>
          <div className="panel-body market-panel__body">
            <OrderBook book={book} />
          </div>
        </div>

        <div className="panel market-panel">
          <div className="panel-header"><span className="panel-title">Recent Trades</span></div>
          <div className="panel-body market-panel__body">
            <TradesFeed trades={trades} isLoading={tradesLoading} />
          </div>
        </div>
      </div>

      <div className="panel event-panel">
        <div className="panel-header">
          <span className="panel-title">Event Tape</span>
          <span className="badge badge--accent">{eventTapeEntries.length} events</span>
        </div>
        <div className="panel-body event-panel__body">
          <EventTape entries={eventTapeEntries} />
        </div>
      </div>
    </>
  )
}

function mergeRealtimeCandles(
  previous: RealtimeCandleUpdate[],
  next: RealtimeCandleUpdate,
): RealtimeCandleUpdate[] {
  const nextKey = getCandleTimestampKey(next)
  if (nextKey === null) {
    return [...previous, next].slice(-200)
  }

  const nextSymbol = next.symbol.toUpperCase()
  const latestKey = previous
    .filter((candidate) => candidate.symbol.toUpperCase() === nextSymbol)
    .map(getCandleTimestampKey)
    .filter((key): key is number => key !== null)
    .reduce<number | null>((latest, key) => latest === null ? key : Math.max(latest, key), null)

  if (latestKey !== null && nextKey < latestKey - STREAM_REWIND_THRESHOLD_SECONDS) {
    return [next]
  }

  const existingIndex = previous.findIndex((candidate) => {
    return candidate.symbol.toUpperCase() === nextSymbol && getCandleTimestampKey(candidate) === nextKey
  })

  if (existingIndex === -1) {
    return [...previous, next].slice(-200)
  }

  const updated = previous.slice()
  updated[existingIndex] = next
  return updated
}

function mergeRealtimeTrades(previous: RecentTrade[], next: RecentTrade): RecentTrade[] {
  const nextTime = Date.parse(next.executedAt)
  if (!Number.isFinite(nextTime)) {
    return [next, ...previous].slice(0, 100)
  }

  const nextSymbol = next.symbol.toUpperCase()
  const latestTime = previous
    .filter((trade) => trade.symbol.toUpperCase() === nextSymbol)
    .map((trade) => Date.parse(trade.executedAt))
    .filter(Number.isFinite)
    .reduce<number | null>((latest, time) => latest === null ? time : Math.max(latest, time), null)

  if (latestTime !== null && nextTime < latestTime - STREAM_REWIND_THRESHOLD_SECONDS * 1000) {
    return [next]
  }

  return [next, ...previous.filter((trade) => trade.tradeId !== next.tradeId)].slice(0, 100)
}

function mergeBookSnapshot(_previous: BookSnapshot | undefined, next: BookSnapshot): BookSnapshot {
  return next
}

function selectLatestBook(
  realtimeBook: BookSnapshot | null,
  persistedBook: BookSnapshot | null,
): BookSnapshot | null {
  if (!realtimeBook) return persistedBook
  if (!persistedBook) return realtimeBook

  return getBookTimestamp(persistedBook) > getBookTimestamp(realtimeBook)
    ? persistedBook
    : realtimeBook
}

function getBookTimestamp(book: BookSnapshot): number {
  if (!book.asOf) return 0
  const parsed = Date.parse(book.asOf)
  return Number.isFinite(parsed) ? parsed : 0
}
