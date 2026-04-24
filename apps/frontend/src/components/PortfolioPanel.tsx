import type { PositionSnapshot, AccountBalance } from '../types'

interface Props {
  positions: PositionSnapshot[]
  balances: AccountBalance[]
  isLoading: boolean
}

export function PortfolioPanel({ positions, balances, isLoading }: Props) {
  if (isLoading) return <div className="loading">Loading portfolio…</div>

  const totalAvailable = balances.reduce((sum, b) => sum + b.available, 0)
  const totalReserved = balances.reduce((sum, b) => sum + b.reserved, 0)

  return (
    <div id="portfolio-panel">
      {/* Metric cards */}
      <div className="metric-grid">
        <div className="metric-card">
          <div className="metric-card__label">Total Available</div>
          <div className="metric-card__value">{totalAvailable.toLocaleString(undefined, { maximumFractionDigits: 2 })}</div>
        </div>
        <div className="metric-card">
          <div className="metric-card__label">Reserved</div>
          <div className="metric-card__value">{totalReserved.toLocaleString(undefined, { maximumFractionDigits: 2 })}</div>
        </div>
        <div className="metric-card">
          <div className="metric-card__label">Positions</div>
          <div className="metric-card__value">{positions.length}</div>
        </div>
        <div className="metric-card">
          {/* LIMITATION: PnL cannot be accurately calculated without a mark-to-market price feed.
              The Query API provides averageOpenPrice but no current market price per position. */}
          <div className="metric-card__label">PnL</div>
          <div className="metric-card__value text-muted" style={{ fontSize: 12 }}>Unavailable</div>
          <div className="metric-card__sub">No mark price feed</div>
        </div>
      </div>

      {/* Balances */}
      {balances.length > 0 && (
        <div style={{ padding: '0 var(--sp-3) var(--sp-2)' }}>
          <div style={{ fontSize: 10, fontWeight: 700, textTransform: 'uppercase', color: 'var(--text-muted)', letterSpacing: 0.6, marginBottom: 'var(--sp-2)' }}>
            Balances
          </div>
          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Asset</th>
                  <th className="right">Available</th>
                  <th className="right">Reserved</th>
                  <th className="right">Total</th>
                </tr>
              </thead>
              <tbody>
                {balances.map((b) => (
                  <tr key={`${b.accountId}-${b.asset}`}>
                    <td style={{ fontWeight: 600 }}>{b.asset}</td>
                    <td className="right">{fmt(b.available)}</td>
                    <td className="right text-muted">{fmt(b.reserved)}</td>
                    <td className="right">{fmt(b.total)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* Positions */}
      {positions.length > 0 && (
        <div style={{ padding: '0 var(--sp-3) var(--sp-3)' }}>
          <div style={{ fontSize: 10, fontWeight: 700, textTransform: 'uppercase', color: 'var(--text-muted)', letterSpacing: 0.6, marginBottom: 'var(--sp-2)' }}>
            Positions
          </div>
          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Symbol</th>
                  <th className="right">Net Qty</th>
                  <th className="right">Avg Price</th>
                  <th className="right">Long</th>
                  <th className="right">Short</th>
                </tr>
              </thead>
              <tbody>
                {positions.map((p) => (
                  <tr key={p.positionId}>
                    <td style={{ fontWeight: 600 }}>{p.symbol}</td>
                    <td className={`right ${p.netQuantity >= 0 ? 'text-buy' : 'text-sell'}`}>
                      {fmt(p.netQuantity)}
                    </td>
                    <td className="right">{p.averageOpenPrice != null ? fmt(p.averageOpenPrice) : '—'}</td>
                    <td className="right">{fmt(p.longQuantity)}</td>
                    <td className="right">{fmt(p.shortQuantity)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {positions.length === 0 && balances.length === 0 && (
        <div className="empty-state">
          <div className="empty-state__icon">💼</div>
          <div className="empty-state__text">No portfolio data</div>
        </div>
      )}
    </div>
  )
}

function fmt(n: number): string {
  return n.toLocaleString(undefined, { maximumFractionDigits: 8 })
}
