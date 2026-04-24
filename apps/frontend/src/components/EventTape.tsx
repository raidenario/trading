import type { EventTapeEntry } from '../types'

interface Props {
  entries: EventTapeEntry[]
}

const EVENT_COLORS: Record<string, string> = {
  ticker_update: 'text-accent',
  trade_update: 'text-buy',
  book_update: 'text-info',
  candle_update: 'text-warn',
}

export function EventTape({ entries }: Props) {
  if (entries.length === 0) {
    return (
      <div className="empty-state">
        <div className="empty-state__icon">📡</div>
        <div className="empty-state__text">No realtime events yet</div>
        <div className="empty-state__sub">Events will appear when the Realtime Gateway is connected</div>
      </div>
    )
  }

  return (
    <div className="event-tape" id="event-tape">
      {entries.map((entry) => (
        <div className="event-tape__row" key={entry.id}>
          <span className="event-tape__time">{fmtTime(entry.timestamp)}</span>
          <span className="event-tape__symbol">{entry.symbol}</span>
          <span className={`event-tape__type ${EVENT_COLORS[entry.eventType] || 'text-muted'}`}>
            {entry.eventType}
          </span>
          <span className="event-tape__payload">{summarize(entry)}</span>
        </div>
      ))}
    </div>
  )
}

function fmtTime(iso: string): string {
  try {
    return new Date(iso).toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit', second: '2-digit', fractionalSecondDigits: 1 } as Intl.DateTimeFormatOptions)
  } catch {
    return iso
  }
}

function summarize(entry: EventTapeEntry): string {
  const p = entry.payload
  switch (entry.eventType) {
    case 'ticker_update':
      return `last=${p.last_price} bid=${p.best_bid} ask=${p.best_ask}`
    case 'trade_update':
      return `price=${p.price} qty=${p.quantity} side=${p.side}`
    case 'book_update': {
      const bids = Array.isArray(p.bids) ? p.bids.length : 0
      const asks = Array.isArray(p.asks) ? p.asks.length : 0
      return `${bids} bids, ${asks} asks`
    }
    case 'candle_update':
      return `O=${p.open} H=${p.high} L=${p.low} C=${p.close}`
    default:
      return JSON.stringify(p).substring(0, 80)
  }
}
