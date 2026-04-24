import type { RecentTrade } from '../types'

interface Props {
  trades: RecentTrade[]
  isLoading: boolean
}

export function TradesFeed({ trades, isLoading }: Props) {
  if (isLoading) return <div className="loading">Loading trades…</div>

  if (trades.length === 0) {
    return (
      <div className="empty-state">
        <div className="empty-state__icon">📊</div>
        <div className="empty-state__text">No recent trades</div>
      </div>
    )
  }

  return (
    <div className="table-wrap" id="trades-feed">
      <table>
        <thead>
          <tr>
            <th>Price</th>
            <th className="right">Qty</th>
            <th>Side</th>
            <th className="right">Time</th>
          </tr>
        </thead>
        <tbody>
          {trades.map((trade, idx) => (
            <tr
              key={trade.tradeId || idx}
              className={`trade-row trade-row--${determineSide(trade.side)}`}
              style={{ animationDelay: `${Math.min(idx * 28, 220)}ms` }}
            >
              <td className={determineSide(trade.side) === 'buy' ? 'text-buy' : 'text-sell'}>
                {fmtPrice(trade.price)}
              </td>
              <td className="right">{fmtQty(trade.quantity)}</td>
              <td>
                <span className={`badge ${determineSide(trade.side) === 'buy' ? 'badge--buy' : 'badge--sell'}`}>
                  {determineSide(trade.side) === 'buy' ? 'BUY' : 'SELL'}
                </span>
              </td>
              <td className="right text-muted">{fmtTime(trade.executedAt)}</td>
            </tr>
          ))}
        </tbody>
      </table>
      <button className="trades-link" type="button">View All Trades</button>
    </div>
  )
}

function determineSide(side: string): 'buy' | 'sell' {
  const lower = side.toLowerCase()
  if (lower.includes('buy') || lower.includes('->')) return 'buy'
  return 'sell'
}

function fmtPrice(n: number): string {
  return n >= 1
    ? n.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })
    : n.toLocaleString(undefined, { minimumFractionDigits: 4, maximumFractionDigits: 8 })
}

function fmtQty(n: number): string {
  return n.toLocaleString(undefined, { maximumFractionDigits: 6 })
}

function fmtTime(iso: string): string {
  try {
    return new Date(iso).toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit', second: '2-digit' })
  } catch {
    return iso
  }
}
