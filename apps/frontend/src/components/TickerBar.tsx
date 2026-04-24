import type { MarketOverviewItem } from '../types'

interface Props {
  overview: MarketOverviewItem[]
  onSelectSymbol: (symbol: string) => void
}

export function TickerBar({ overview, onSelectSymbol }: Props) {
  if (overview.length === 0) return null

  return (
    <div className="ticker-bar" role="marquee" aria-label="Market tickers">
      {overview.map((m) => (
        <div
          key={m.symbol}
          className="ticker-item"
          onClick={() => onSelectSymbol(m.symbol)}
          role="button"
          tabIndex={0}
          onKeyDown={(e) => e.key === 'Enter' && onSelectSymbol(m.symbol)}
        >
          <span className="ticker-item__symbol">{m.symbol}</span>
          <span className="ticker-item__price">{fmtPrice(m.lastPrice)}</span>
          <span className={`ticker-item__change ${m.changePercent24h >= 0 ? 'text-buy' : 'text-sell'}`}>
            {m.changePercent24h >= 0 ? '+' : ''}{m.changePercent24h.toFixed(2)}%
          </span>
        </div>
      ))}
    </div>
  )
}

function fmtPrice(n: number): string {
  return n >= 1
    ? n.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })
    : n.toLocaleString(undefined, { minimumFractionDigits: 4, maximumFractionDigits: 6 })
}
