import type { BookSnapshot } from '../types'

interface Props {
  book: BookSnapshot | null
}

export function OrderBook({ book }: Props) {
  if (!book || (book.bids.length === 0 && book.asks.length === 0)) {
    return (
      <div className="empty-state">
        <div className="empty-state__icon">📒</div>
        <div className="empty-state__text">No order book data</div>
        <div className="empty-state__sub">Waiting for book_update events</div>
      </div>
    )
  }

  const maxQty = Math.max(
    ...book.bids.map((l) => l.quantity),
    ...book.asks.map((l) => l.quantity),
    1,
  )

  const asksSorted = [...book.asks].sort((a, b) => b.price - a.price).slice(0, 12)
  const bidsSorted = [...book.bids].sort((a, b) => b.price - a.price).slice(0, 12)

  const spread = asksSorted.length > 0 && bidsSorted.length > 0
    ? asksSorted[asksSorted.length - 1].price - bidsSorted[0].price
    : null

  return (
    <div id="order-book">
      {/* Header */}
      <div className="book-row" style={{ padding: '4px 12px', borderBottom: '1px solid var(--border)' }}>
        <span className="text-muted" style={{ fontSize: 10, fontWeight: 700, textTransform: 'uppercase', letterSpacing: 0.6 }}>Price</span>
        <span className="text-muted" style={{ fontSize: 10, fontWeight: 700, textTransform: 'uppercase', textAlign: 'right', letterSpacing: 0.6 }}>Qty</span>
        <span className="text-muted" style={{ fontSize: 10, fontWeight: 700, textTransform: 'uppercase', textAlign: 'right', letterSpacing: 0.6 }}>Orders</span>
      </div>

      {/* Asks (sells) — displayed top to bottom, highest first */}
      {asksSorted.map((level, i) => (
        <div className="book-row" key={`ask-${i}`}>
          <div className="book-row__bar book-row__bar--ask" style={{ width: `${(level.quantity / maxQty) * 100}%` }} />
          <span className="text-sell">{fmtPrice(level.price)}</span>
          <span style={{ textAlign: 'right' }}>{fmtQty(level.quantity)}</span>
          <span style={{ textAlign: 'right', color: 'var(--text-muted)' }}>{level.order_count ?? level.orderCount ?? '—'}</span>
        </div>
      ))}

      {/* Spread */}
      {spread !== null && (
        <div className="book-spread">
          Spread: {fmtPrice(spread)} ({spread > 0 && bidsSorted[0].price > 0 ? ((spread / bidsSorted[0].price) * 100).toFixed(3) : '0.000'}%)
        </div>
      )}

      {/* Bids (buys) — highest first */}
      {bidsSorted.map((level, i) => (
        <div className="book-row" key={`bid-${i}`}>
          <div className="book-row__bar book-row__bar--bid" style={{ width: `${(level.quantity / maxQty) * 100}%` }} />
          <span className="text-buy">{fmtPrice(level.price)}</span>
          <span style={{ textAlign: 'right' }}>{fmtQty(level.quantity)}</span>
          <span style={{ textAlign: 'right', color: 'var(--text-muted)' }}>{level.order_count ?? level.orderCount ?? '—'}</span>
        </div>
      ))}
    </div>
  )
}

function fmtPrice(n: number): string {
  return n >= 1
    ? n.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })
    : n.toLocaleString(undefined, { minimumFractionDigits: 4, maximumFractionDigits: 8 })
}

function fmtQty(n: number): string {
  return n.toLocaleString(undefined, { maximumFractionDigits: 6 })
}
