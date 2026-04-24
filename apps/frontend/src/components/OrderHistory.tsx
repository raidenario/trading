import type { EnrichedOrderView } from '../types'

interface Props {
  orders: EnrichedOrderView[]
  isLoading: boolean
}

export function OrderHistory({ orders, isLoading }: Props) {
  if (isLoading) return <div className="loading">Loading orders…</div>

  if (orders.length === 0) {
    return (
      <div className="empty-state">
        <div className="empty-state__icon">📋</div>
        <div className="empty-state__text">No orders yet</div>
        <div className="empty-state__sub">Submit an order from the ticket panel</div>
      </div>
    )
  }

  return (
    <div className="table-wrap" id="order-history">
      <table>
        <thead>
          <tr>
            <th>ID</th>
            <th>Symbol</th>
            <th>Side</th>
            <th>Type</th>
            <th className="right">Price</th>
            <th className="right">Qty</th>
            <th className="right">Filled</th>
            <th>Status</th>
            <th className="right">Time</th>
          </tr>
        </thead>
        <tbody>
          {orders.map((o) => (
            <tr key={o.orderId}>
              <td className="text-muted">{o.orderId.substring(0, 8)}</td>
              <td style={{ fontWeight: 600 }}>{o.symbol}</td>
              <td>
                <span className={`badge ${o.side === 'Buy' ? 'badge--buy' : 'badge--sell'}`}>
                  {o.side}
                </span>
              </td>
              <td>{o.type}</td>
              <td className="right">{o.price != null ? fmtPrice(o.price) : 'MKT'}</td>
              <td className="right">{fmtQty(o.quantity)}</td>
              <td className="right">{fmtQty(o.filledQuantity)}</td>
              <td>
                <span className={`badge ${statusBadge(o.status)}`}>{o.status}</span>
              </td>
              <td className="right text-muted">{fmtTime(o.updatedAt)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

function statusBadge(status: string): string {
  switch (status) {
    case 'Filled': return 'badge--buy'
    case 'PartiallyFilled': return 'badge--warn'
    case 'Rejected': case 'Cancelled': return 'badge--sell'
    case 'Accepted': case 'New': case 'Pending': return 'badge--info'
    default: return 'badge--muted'
  }
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
