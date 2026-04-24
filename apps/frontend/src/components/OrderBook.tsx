import type { CSSProperties } from 'react'
import type { BookSnapshot } from '../types'

interface Props {
  book: BookSnapshot | null
}

export function OrderBook({ book }: Props) {
  if (!book || (book.bids.length === 0 && book.asks.length === 0)) {
    return (
      <div className="empty-state">
        <div className="empty-state__text">
          {book ? 'Order book is empty' : 'No order book data'}
        </div>
        <div className="empty-state__sub">
          {book ? 'No resting bids or asks for this symbol' : 'Waiting for book_update events'}
        </div>
      </div>
    )
  }

  const maxQty = Math.max(
    ...book.bids.map((l) => l.quantity),
    ...book.asks.map((l) => l.quantity),
    1,
  )

  const asksSorted = [...book.asks].sort((a, b) => a.price - b.price).slice(0, 7)
  const bidsSorted = [...book.bids].sort((a, b) => b.price - a.price).slice(0, 7)
  const asksWithTotal = withRunningTotal(asksSorted)
  const bidsWithTotal = withRunningTotal(bidsSorted)
  const maxTotal = Math.max(
    ...asksWithTotal.map((level) => level.total),
    ...bidsWithTotal.map((level) => level.total),
    1,
  )

  const spread = asksSorted.length > 0 && bidsSorted.length > 0
    ? asksSorted[0].price - bidsSorted[0].price
    : null

  return (
    <div id="order-book" className="order-book-pro">
      <div className="book-columns" aria-hidden="true">
        <div>Price (USD)</div>
        <div>Size (BTC)</div>
        <div>Total (BTC)</div>
        <div>Price (USD)</div>
        <div>Size (BTC)</div>
        <div>Total (BTC)</div>
      </div>

      <div className="book-depth-grid">
        <div className="book-side book-side--bid">
          {bidsWithTotal.map((level) => (
            <BookLevelRow
              key={`bid-${level.price}`}
              side="bid"
              level={level}
              maxQty={maxQty}
              maxTotal={maxTotal}
            />
          ))}
        </div>

        <div className="book-depth-center" aria-hidden="true">
          {bidsWithTotal.map((level) => (
            <div key={`bid-depth-${level.price}`} className="depth-column depth-column--bid" style={{ height: `${Math.max(10, (level.quantity / maxQty) * 100)}%` }} />
          ))}
          {asksWithTotal.map((level) => (
            <div key={`ask-depth-${level.price}`} className="depth-column depth-column--ask" style={{ height: `${Math.max(10, (level.quantity / maxQty) * 100)}%` }} />
          ))}
        </div>

        <div className="book-side book-side--ask">
          {asksWithTotal.map((level) => (
            <BookLevelRow
              key={`ask-${level.price}`}
              side="ask"
              level={level}
              maxQty={maxQty}
              maxTotal={maxTotal}
            />
          ))}
        </div>
      </div>

      {spread !== null && (
        <div className="book-spread">
          <span>Spread</span>
          <strong>{fmtPrice(spread)}</strong>
          <span>({spread > 0 && bidsSorted[0].price > 0 ? ((spread / bidsSorted[0].price) * 100).toFixed(2) : '0.00'}%)</span>
        </div>
      )}
    </div>
  )
}

type BookLevel = BookSnapshot['bids'][number] & { total: number }

function BookLevelRow({
  side,
  level,
  maxQty,
  maxTotal,
}: {
  side: 'bid' | 'ask'
  level: BookLevel
  maxQty: number
  maxTotal: number
}) {
  const barWidth = Math.max(6, (level.total / maxTotal) * 100)
  const pulseDelay = `${Math.min(320, (level.quantity / maxQty) * 180)}ms`

  return (
    <div className={`book-row book-row--${side}`} style={{ '--bar-width': `${barWidth}%`, '--pulse-delay': pulseDelay } as CSSProperties}>
      <div className={`book-row__bar book-row__bar--${side}`} />
      <span className={side === 'bid' ? 'text-buy' : 'text-sell'}>{fmtPrice(level.price)}</span>
      <span className="right">{fmtQty(level.quantity)}</span>
      <span className="right">{fmtQty(level.total)}</span>
    </div>
  )
}

function withRunningTotal(levels: BookSnapshot['bids']): BookLevel[] {
  let total = 0
  return levels.map((level) => {
    total += level.quantity
    return { ...level, total }
  })
}

function fmtPrice(n: number): string {
  return n >= 1
    ? n.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })
    : n.toLocaleString(undefined, { minimumFractionDigits: 4, maximumFractionDigits: 8 })
}

function fmtQty(n: number): string {
  return n.toLocaleString(undefined, { maximumFractionDigits: 6 })
}
