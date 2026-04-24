import type { TickerWithCandle } from '../types'

interface Props {
  data: TickerWithCandle | null
  symbol: string
  isLoading: boolean
}

export function TickerPanel({ data, symbol, isLoading }: Props) {
  if (isLoading) return <div className="loading">Loading ticker…</div>
  if (!data) return <div className="empty-state"><div className="empty-state__text">No ticker data</div></div>

  const { ticker } = data
  const isPositive = ticker.change24H >= 0

  return (
    <div className="ticker-detail" id="ticker-panel">
      <div className="ticker-detail__main">
        <div className="ticker-detail__identity">
          <span className="asset-avatar asset-avatar--lg">{symbol.slice(0, 2).toUpperCase()}</span>
          <div>
            <strong>{symbol}</strong>
            <span>{symbol.includes('-') ? symbol.replace('-', ' / ') : symbol}</span>
          </div>
        </div>
        <div className="ticker-detail__quote">
          <span className="ticker-detail__price">{fmtPrice(ticker.lastPrice)}</span>
          <span className={`ticker-detail__change ${isPositive ? 'text-buy' : 'text-sell'}`}>
            {isPositive ? '▲' : '▼'} {Math.abs(ticker.change24H).toFixed(2)} ({isPositive ? '+' : ''}{pctChange(ticker).toFixed(2)}%)
          </span>
        </div>
        <div className="ticker-detail__time">
          {symbol} · as of {new Date(ticker.asOf).toLocaleTimeString()}
        </div>
      </div>

      <div>
        <div className="ticker-detail__label">Bid</div>
        <div className="ticker-detail__value text-buy">{fmtPrice(ticker.bestBid)}</div>
      </div>
      <div>
        <div className="ticker-detail__label">Ask</div>
        <div className="ticker-detail__value text-sell">{fmtPrice(ticker.bestAsk)}</div>
      </div>
      <div>
        <div className="ticker-detail__label">24H High</div>
        <div className="ticker-detail__value">{fmtPrice(ticker.high24H)}</div>
      </div>
      <div>
        <div className="ticker-detail__label">24H Low</div>
        <div className="ticker-detail__value">{fmtPrice(ticker.low24H)}</div>
      </div>
      <div>
        <div className="ticker-detail__label">24H Volume</div>
        <div className="ticker-detail__value">{ticker.volume24H.toLocaleString()}</div>
      </div>
      <div>
        <div className="ticker-detail__label">Spread</div>
        <div className="ticker-detail__value">{fmtPrice(ticker.bestAsk - ticker.bestBid)}</div>
      </div>
    </div>
  )
}

function fmtPrice(n: number): string {
  if (n === 0) return '—'
  return n >= 1
    ? n.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })
    : n.toLocaleString(undefined, { minimumFractionDigits: 4, maximumFractionDigits: 8 })
}

function pctChange(t: { change24H: number; lastPrice: number }): number {
  if (t.lastPrice === 0) return 0
  return (t.change24H / t.lastPrice) * 100
}
