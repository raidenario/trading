import { useState, useMemo } from 'react'
import type { InstrumentSnapshot, MarketOverviewItem } from '../types'

interface Props {
  instruments: InstrumentSnapshot[]
  overview: MarketOverviewItem[]
  selectedSymbol: string
  onSelect: (symbol: string) => void
}

export function Sidebar({ instruments, overview, selectedSymbol, onSelect }: Props) {
  const [filter, setFilter] = useState('')

  const overviewMap = useMemo(() => {
    const map = new Map<string, MarketOverviewItem>()
    for (const item of overview) {
      map.set(item.symbol, item)
    }
    return map
  }, [overview])

  const filtered = useMemo(() => {
    if (!filter) return instruments
    const lower = filter.toLowerCase()
    return instruments.filter(
      (i) =>
        i.symbol.toLowerCase().includes(lower) ||
        i.baseAsset.toLowerCase().includes(lower) ||
        i.assetClass.toLowerCase().includes(lower),
    )
  }, [instruments, filter])

  return (
    <aside className="app-sidebar" aria-label="Instrument watchlist">
      <div className="sidebar-header">Watchlist</div>
      <div className="sidebar-search">
        <input
          type="text"
          placeholder="Search symbol…"
          value={filter}
          onChange={(e) => setFilter(e.target.value)}
          aria-label="Filter instruments"
          id="sidebar-search-input"
        />
      </div>
      <div className="sidebar-list" role="listbox" aria-label="Instruments">
        {filtered.map((inst) => {
          const market = overviewMap.get(inst.symbol)
          const isActive = inst.symbol === selectedSymbol
          return (
            <div
              key={inst.symbol}
              className={`sidebar-item${isActive ? ' active' : ''}`}
              onClick={() => onSelect(inst.symbol)}
              role="option"
              aria-selected={isActive}
              tabIndex={0}
              onKeyDown={(e) => e.key === 'Enter' && onSelect(inst.symbol)}
              id={`sidebar-item-${inst.symbol}`}
            >
              <div>
                <div className="sidebar-item__symbol">{inst.symbol}</div>
                <div className="sidebar-item__meta">
                  {inst.baseAsset}/{inst.quoteAsset}
                </div>
              </div>
              <div>
                <div className="sidebar-item__price">
                  {market ? fmtPrice(market.lastPrice) : '—'}
                </div>
                {market && (
                  <div
                    className={`sidebar-item__change ${market.changePercent24h >= 0 ? 'text-buy' : 'text-sell'}`}
                  >
                    {market.changePercent24h >= 0 ? '+' : ''}
                    {market.changePercent24h.toFixed(2)}%
                  </div>
                )}
              </div>
            </div>
          )
        })}
        {filtered.length === 0 && (
          <div className="empty-state">
            <div className="empty-state__text">No instruments found</div>
          </div>
        )}
      </div>
    </aside>
  )
}

function fmtPrice(n: number): string {
  return n >= 1
    ? n.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })
    : n.toLocaleString(undefined, { minimumFractionDigits: 4, maximumFractionDigits: 6 })
}
