// ══════════════════════════════════════════════════════════
// Component Render Tests
// ══════════════════════════════════════════════════════════

import { describe, it, expect, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { EventTape } from '../components/EventTape'
import { TradesFeed } from '../components/TradesFeed'
import { OrderBook } from '../components/OrderBook'
import { TickerPanel } from '../components/TickerPanel'
import { OrderHistory } from '../components/OrderHistory'
import type { EventTapeEntry, RecentTrade, BookSnapshot, TickerWithCandle, EnrichedOrderView } from '../types'

describe('EventTape', () => {
  it('shows empty state when no entries', () => {
    render(<EventTape entries={[]} />)
    expect(screen.getByText(/no realtime events/i)).toBeInTheDocument()
  })

  it('renders entries with symbol, type and payload', () => {
    const entries: EventTapeEntry[] = [
      {
        id: '1',
        timestamp: '2026-01-15T10:00:00Z',
        symbol: 'PETR4',
        eventType: 'ticker_update',
        payload: { last_price: 32.5, best_bid: 32.4, best_ask: 32.6 },
      },
    ]
    render(<EventTape entries={entries} />)
    expect(screen.getByText('PETR4')).toBeInTheDocument()
    expect(screen.getByText('ticker_update')).toBeInTheDocument()
  })
})

describe('TradesFeed', () => {
  it('shows loading state', () => {
    render(<TradesFeed trades={[]} isLoading={true} />)
    expect(screen.getByText(/loading trades/i)).toBeInTheDocument()
  })

  it('shows empty state when no trades', () => {
    render(<TradesFeed trades={[]} isLoading={false} />)
    expect(screen.getByText(/no recent trades/i)).toBeInTheDocument()
  })

  it('renders trade rows', () => {
    const trades: RecentTrade[] = [
      { tradeId: 't1', symbol: 'PETR4', price: 32.50, quantity: 100, side: 'Buy', executedAt: '2026-01-15T10:00:00Z' },
      { tradeId: 't2', symbol: 'PETR4', price: 32.48, quantity: 50, side: 'Sell', executedAt: '2026-01-15T10:00:01Z' },
    ]
    render(<TradesFeed trades={trades} isLoading={false} />)
    expect(screen.getByText('BUY')).toBeInTheDocument()
    expect(screen.getByText('SELL')).toBeInTheDocument()
  })
})

describe('OrderBook', () => {
  it('shows empty state when no book', () => {
    render(<OrderBook book={null} />)
    expect(screen.getByText(/no order book data/i)).toBeInTheDocument()
  })

  it('renders bids and asks', () => {
    const book: BookSnapshot = {
      symbol: 'PETR4',
      bids: [{ price: 32.40, quantity: 100, order_count: 3 }],
      asks: [{ price: 32.60, quantity: 80, order_count: 2 }],
    }
    render(<OrderBook book={book} />)
    // Verify book renders by checking the container exists and has content
    const bookEl = document.getElementById('order-book')
    expect(bookEl).toBeInTheDocument()
    // Check for buy/sell colored spans
    expect(bookEl!.querySelector('.text-buy')).toBeInTheDocument()
    expect(bookEl!.querySelector('.text-sell')).toBeInTheDocument()
  })

  it('shows spread', () => {
    const book: BookSnapshot = {
      symbol: 'PETR4',
      bids: [{ price: 32.00, quantity: 100, order_count: 1 }],
      asks: [{ price: 32.20, quantity: 100, order_count: 1 }],
    }
    render(<OrderBook book={book} />)
    expect(screen.getByText(/spread/i)).toBeInTheDocument()
  })
})

describe('TickerPanel', () => {
  it('shows loading state', () => {
    render(<TickerPanel data={null} symbol="PETR4" isLoading={true} />)
    expect(screen.getByText(/loading ticker/i)).toBeInTheDocument()
  })

  it('shows empty state when no data', () => {
    render(<TickerPanel data={null} symbol="PETR4" isLoading={false} />)
    expect(screen.getByText(/no ticker data/i)).toBeInTheDocument()
  })

  it('renders ticker values', () => {
    const data: TickerWithCandle = {
      ticker: {
        symbol: 'PETR4',
        lastPrice: 32.50,
        bestBid: 32.40,
        bestAsk: 32.60,
        high24H: 33.00,
        low24H: 31.80,
        volume24H: 15000,
        change24H: 0.70,
        asOf: '2026-01-15T10:00:00Z',
      },
      candle: {
        symbol: 'PETR4',
        interval: '1m',
        open: 32.20,
        high: 32.60,
        low: 32.10,
        close: 32.50,
        volume: 500,
        openTime: '2026-01-15T09:59:00Z',
        closeTime: '2026-01-15T10:00:00Z',
      },
    }
    render(<TickerPanel data={data} symbol="PETR4" isLoading={false} />)
    // Should render ticker detail section (testing by id)
    expect(document.getElementById('ticker-panel')).toBeInTheDocument()
  })
})

describe('OrderHistory', () => {
  it('shows loading state', () => {
    render(<OrderHistory orders={[]} isLoading={true} />)
    expect(screen.getByText(/loading orders/i)).toBeInTheDocument()
  })

  it('shows empty state', () => {
    render(<OrderHistory orders={[]} isLoading={false} />)
    expect(screen.getByText(/no orders yet/i)).toBeInTheDocument()
  })

  it('renders order rows', () => {
    const orders: EnrichedOrderView[] = [
      {
        orderId: '12345678-1234-1234-1234-123456789012',
        accountId: 'acc-1',
        tradingAccountId: 'ta-1',
        instrumentId: 'inst-1',
        symbol: 'PETR4',
        side: 'Buy',
        type: 'Limit',
        status: 'Filled',
        quantity: 100,
        filledQuantity: 100,
        openQuantity: 0,
        price: 32.50,
        sourceSystem: 'Web',
        createdAt: '2026-01-15T10:00:00Z',
        updatedAt: '2026-01-15T10:00:01Z',
      },
    ]
    render(<OrderHistory orders={orders} isLoading={false} />)
    expect(screen.getByText('PETR4')).toBeInTheDocument()
    expect(screen.getByText('Buy')).toBeInTheDocument()
    // 'Filled' appears in both the <th> header and the <span> badge
    expect(screen.getAllByText('Filled')).toHaveLength(2)
  })
})
