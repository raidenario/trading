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
          <Sparkline symbol={m.symbol} price={m.lastPrice} change={m.changePercent24h} />
        </div>
      ))}
    </div>
  )
}

function Sparkline({ symbol, price, change }: { symbol: string; price: number; change: number }) {
  const points = buildSparklinePoints(symbol, price, change)
  const path = points.map((point, index) => `${index === 0 ? 'M' : 'L'}${point.x},${point.y}`).join(' ')

  return (
    <svg className="ticker-sparkline" viewBox="0 0 72 24" role="img" aria-label={`${symbol} intraday sparkline`}>
      <path className="ticker-sparkline__area" d={`${path} L72,24 L0,24 Z`} />
      <path className="ticker-sparkline__line" d={path} />
    </svg>
  )
}

function buildSparklinePoints(symbol: string, price: number, change: number): Array<{ x: number; y: number }> {
  const seed = [...symbol].reduce((sum, char) => sum + char.charCodeAt(0), Math.round(price * 10))
  const direction = change >= 0 ? -1 : 1

  return Array.from({ length: 18 }, (_, index) => {
    const x = (index / 17) * 72
    const wave = Math.sin((index + seed) * 0.76) * 3.6
    const drift = ((index - 8.5) / 17) * Math.min(Math.abs(change), 6) * direction
    const noise = (((seed + index * 13) % 7) - 3) * 0.7
    const y = Math.max(4, Math.min(20, 12 + wave + drift + noise))
    return { x: Number(x.toFixed(2)), y: Number(y.toFixed(2)) }
  })
}

function fmtPrice(n: number): string {
  return n >= 1
    ? n.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })
    : n.toLocaleString(undefined, { minimumFractionDigits: 4, maximumFractionDigits: 6 })
}
