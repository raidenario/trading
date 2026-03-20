import { useState, useEffect, useCallback } from 'react'
import * as api from './api'

type Page = 'dashboard' | 'trading' | 'orders'

const SYMBOLS = ['BTC-USD', 'ETH-USD', 'SOL-USD']

export default function App() {
  const [page, setPage] = useState<Page>('dashboard')
  const [accounts, setAccounts] = useState<api.AccountSummary[]>([])
  const [selectedAccount, setSelectedAccount] = useState('')
  const [balances, setBalances] = useState<api.AccountBalance[]>([])
  const [overview, setOverview] = useState<api.MarketOverview[]>([])

  useEffect(() => {
    api.getAccounts().then(data => {
      setAccounts(data)
      if (data.length > 0) setSelectedAccount(data[0].accountId)
    }).catch(() => {})
    api.getMarketOverview().then(setOverview).catch(() => {})
  }, [])

  useEffect(() => {
    if (!selectedAccount) return
    api.getBalances(selectedAccount).then(setBalances).catch(() => {})
  }, [selectedAccount])

  return (
    <div className="app">
      <header className="header">
        <span className="header-logo">⚡ Exchange Platform</span>
        <nav className="header-nav">
          <button className={page === 'dashboard' ? 'active' : ''} onClick={() => setPage('dashboard')}>Dashboard</button>
          <button className={page === 'trading' ? 'active' : ''} onClick={() => setPage('trading')}>Trading</button>
          <button className={page === 'orders' ? 'active' : ''} onClick={() => setPage('orders')}>Orders</button>
        </nav>
        <div className="header-account">
          <div className="status-dot" title="Connected" />
          <select value={selectedAccount} onChange={e => setSelectedAccount(e.target.value)}>
            {accounts.map(a => <option key={a.accountId} value={a.accountId}>{a.displayName}</option>)}
          </select>
        </div>
      </header>

      <TickerBar overview={overview} />

      <main className="main">
        {page === 'dashboard' && <DashboardPage balances={balances} overview={overview} selectedAccount={selectedAccount} />}
        {page === 'trading' && <TradingPage selectedAccount={selectedAccount} />}
        {page === 'orders' && <OrdersPage selectedAccount={selectedAccount} />}
      </main>
    </div>
  )
}

function TickerBar({ overview }: { overview: api.MarketOverview[] }) {
  return (
    <div className="ticker-bar">
      {overview.map(m => (
        <div key={m.symbol} className="ticker-item">
          <span className="ticker-symbol">{m.symbol}</span>
          <span className="ticker-price">${m.lastPrice.toLocaleString(undefined, { minimumFractionDigits: 2 })}</span>
          <span className={`ticker-change ${m.changePercent24h >= 0 ? 'positive' : 'negative'}`}>
            {m.changePercent24h >= 0 ? '+' : ''}{m.changePercent24h.toFixed(2)}%
          </span>
        </div>
      ))}
      {overview.length === 0 && <span style={{color: 'var(--text-muted)'}}>Loading markets...</span>}
    </div>
  )
}

function DashboardPage({ balances, overview, selectedAccount }: { balances: api.AccountBalance[]; overview: api.MarketOverview[]; selectedAccount: string }) {
  const totalUsd = balances.reduce((sum, b) => {
    if (b.asset === 'USD') return sum + b.available + b.reserved
    const market = overview.find(m => m.symbol === `${b.asset}-USD`)
    return sum + (b.available + b.reserved) * (market?.lastPrice ?? 0)
  }, 0)

  return (
    <>
      <div className="grid-4">
        <div className="card">
          <div className="card-title">Portfolio Value</div>
          <div className="card-value">${totalUsd.toLocaleString(undefined, { maximumFractionDigits: 2 })}</div>
          <div className="card-sub">Across all assets</div>
        </div>
        {balances.slice(0, 3).map(b => (
          <div className="card" key={b.asset}>
            <div className="card-title">{b.asset} Balance</div>
            <div className="card-value" style={{ fontSize: 22 }}>
              {b.available.toLocaleString(undefined, { maximumFractionDigits: 8 })}
            </div>
            <div className="card-sub">Reserved: {b.reserved}</div>
          </div>
        ))}
      </div>

      <div className="grid-2">
        <div className="card">
          <div className="card-title">Market Overview</div>
          <div className="table-wrap">
            <table>
              <thead>
                <tr><th>Symbol</th><th>Price</th><th>24h Change</th><th>Volume</th><th>High</th><th>Low</th></tr>
              </thead>
              <tbody>
                {overview.map(m => (
                  <tr key={m.symbol}>
                    <td style={{ fontWeight: 600 }}>{m.symbol}</td>
                    <td>${m.lastPrice.toLocaleString()}</td>
                    <td className={m.changePercent24h >= 0 ? 'positive' : 'negative'}>
                      {m.changePercent24h >= 0 ? '+' : ''}{m.changePercent24h.toFixed(2)}%
                    </td>
                    <td>{m.volume24h.toLocaleString()}</td>
                    <td>${m.high24h.toLocaleString()}</td>
                    <td>${m.low24h.toLocaleString()}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>

        <div className="card">
          <div className="card-title">All Balances</div>
          <div className="table-wrap">
            <table>
              <thead>
                <tr><th>Asset</th><th>Available</th><th>Reserved</th><th>Total</th></tr>
              </thead>
              <tbody>
                {balances.map(b => (
                  <tr key={b.asset}>
                    <td style={{ fontWeight: 600 }}>{b.asset}</td>
                    <td>{b.available.toLocaleString(undefined, { maximumFractionDigits: 8 })}</td>
                    <td>{b.reserved}</td>
                    <td>{b.total.toLocaleString(undefined, { maximumFractionDigits: 8 })}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      </div>
    </>
  )
}

function TradingPage({ selectedAccount }: { selectedAccount: string }) {
  const [symbol, setSymbol] = useState('BTC-USD')
  const [side, setSide] = useState<'Buy' | 'Sell'>('Buy')
  const [price, setPrice] = useState('50000')
  const [quantity, setQuantity] = useState('0.10')
  const [status, setStatus] = useState('')
  const [ticker, setTicker] = useState<api.TickerData | null>(null)
  const [trades, setTrades] = useState<api.RecentTrade[]>([])

  const loadData = useCallback(() => {
    api.getTicker(symbol).then(setTicker).catch(() => {})
    api.getRecentTrades(symbol).then(data => setTrades(Array.isArray(data) ? data : [])).catch(() => setTrades([]))
  }, [symbol])

  useEffect(() => { loadData(); const i = setInterval(loadData, 5000); return () => clearInterval(i) }, [loadData])

  const submit = async () => {
    setStatus('Sending...')
    try {
      await api.createOrder({
        orderId: crypto.randomUUID(),
        accountId: selectedAccount,
        symbol,
        side,
        type: 'Limit',
        quantity: parseFloat(quantity),
        price: parseFloat(price),
        timeInForce: 'Gtc',
        clientOrderId: `web-${Date.now()}`,
        submittedAt: new Date().toISOString(),
        schemaVersion: 1,
      })
      setStatus('Order sent!')
      setTimeout(() => setStatus(''), 3000)
    } catch (e: any) {
      setStatus(`Error: ${e.message}`)
    }
  }

  return (
    <div className="grid-3" style={{ gridTemplateColumns: '300px 1fr 1fr' }}>
      <div className="card">
        <div className="card-title">New Order</div>

        <div className="form-group" style={{ marginBottom: 12 }}>
          <label>Symbol</label>
          <select value={symbol} onChange={e => setSymbol(e.target.value)}>
            {SYMBOLS.map(s => <option key={s} value={s}>{s}</option>)}
          </select>
        </div>

        <div className="side-tabs">
          <button className={`side-tab ${side === 'Buy' ? 'buy-active' : ''}`} onClick={() => setSide('Buy')}>Buy</button>
          <button className={`side-tab ${side === 'Sell' ? 'sell-active' : ''}`} onClick={() => setSide('Sell')}>Sell</button>
        </div>

        <div className="form-group" style={{ marginBottom: 12 }}>
          <label>Price (USD)</label>
          <input type="number" value={price} onChange={e => setPrice(e.target.value)} step="0.01" />
        </div>

        <div className="form-group" style={{ marginBottom: 16 }}>
          <label>Quantity</label>
          <input type="number" value={quantity} onChange={e => setQuantity(e.target.value)} step="0.0001" />
        </div>

        <button className={`btn ${side === 'Buy' ? 'btn-buy' : 'btn-sell'}`} style={{ width: '100%' }} onClick={submit}>
          {side} {symbol.split('-')[0]}
        </button>
        {status && <div className="card-sub" style={{ marginTop: 8, textAlign: 'center' }}>{status}</div>}
      </div>

      <div className="card">
        <div className="card-title">Ticker — {symbol}</div>
        {ticker && ticker.ticker ? (
          <div style={{ display: 'grid', gap: 12 }}>
            <div>
              <div className="card-value">${(ticker.ticker.lastPrice || 0).toLocaleString()}</div>
              <div className={`card-sub ${(ticker.ticker.changePercent24h || 0) >= 0 ? 'positive' : 'negative'}`}>
                {(ticker.ticker.changePercent24h || 0) >= 0 ? '+' : ''}{(ticker.ticker.changePercent24h || 0).toFixed(2)}%
              </div>
            </div>
            <div className="grid-2">
              <div><span style={{ color: 'var(--text-muted)', fontSize: 11 }}>BID</span><br/><span className="positive" style={{fontFamily:'var(--font-mono)'}}>${(ticker.ticker.bestBid || 0).toLocaleString()}</span></div>
              <div><span style={{ color: 'var(--text-muted)', fontSize: 11 }}>ASK</span><br/><span className="negative" style={{fontFamily:'var(--font-mono)'}}>${(ticker.ticker.bestAsk || 0).toLocaleString()}</span></div>
              <div><span style={{ color: 'var(--text-muted)', fontSize: 11 }}>24H HIGH</span><br/><span style={{fontFamily:'var(--font-mono)'}}>${(ticker.ticker.high24h || 0).toLocaleString()}</span></div>
              <div><span style={{ color: 'var(--text-muted)', fontSize: 11 }}>24H LOW</span><br/><span style={{fontFamily:'var(--font-mono)'}}>${(ticker.ticker.low24h || 0).toLocaleString()}</span></div>
            </div>
            <div>
              <span style={{ color: 'var(--text-muted)', fontSize: 11 }}>VOLUME 24H</span><br/>
              <span style={{fontFamily:'var(--font-mono)'}}>{(ticker.ticker.volume24h || 0).toLocaleString()}</span>
            </div>
          </div>
        ) : <div className="loading">Loading...</div>}
      </div>

      <div className="card">
        <div className="card-title">Recent Trades</div>
        <div className="table-wrap" style={{ maxHeight: 400, overflowY: 'auto' }}>
          <table>
            <thead><tr><th>Price</th><th>Qty</th><th>Side</th><th>Time</th></tr></thead>
            <tbody>
              {Array.isArray(trades) && trades.length > 0 ? trades.map(t => (
                <tr key={t.tradeId}>
                  <td className={t.side === 'Buy' ? 'positive' : 'negative'}>${(t.price || 0).toLocaleString()}</td>
                  <td>{(t.quantity || 0).toFixed(4)}</td>
                  <td><span className={`badge ${t.side === 'Buy' ? 'badge-green' : 'badge-red'}`}>{t.side}</span></td>
                  <td style={{ color: 'var(--text-muted)' }}>{new Date(t.executedAt).toLocaleTimeString()}</td>
                </tr>
              )) : (
                <tr><td colSpan={4} style={{ textAlign: 'center', color: 'var(--text-muted)', padding: 20 }}>No trades found.</td></tr>
              )}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  )
}

function OrdersPage({ selectedAccount }: { selectedAccount: string }) {
  const [orders, setOrders] = useState<api.OrderView[]>([])

  useEffect(() => {
    if (!selectedAccount) return
    api.getOrders(selectedAccount).then(setOrders).catch(() => {})
  }, [selectedAccount])

  const statusBadge = (s: string) => {
    const map: Record<string, string> = { Filled: 'badge-green', Accepted: 'badge-blue', PartiallyFilled: 'badge-yellow', Rejected: 'badge-red', Cancelled: 'badge-red', Pending: 'badge-blue' }
    return <span className={`badge ${map[s] || 'badge-blue'}`}>{s}</span>
  }

  return (
    <div className="card">
      <div className="card-title">Order History</div>
      <div className="table-wrap">
        <table>
          <thead>
            <tr><th>Order ID</th><th>Symbol</th><th>Side</th><th>Type</th><th>Price</th><th>Qty</th><th>Filled</th><th>Status</th><th>Created</th></tr>
          </thead>
          <tbody>
            {orders.length === 0 ? (
              <tr><td colSpan={9} style={{ textAlign: 'center', color: 'var(--text-muted)', padding: 32 }}>No orders yet. Go to Trading to place your first order!</td></tr>
            ) : orders.map(o => (
              <tr key={o.orderId}>
                <td style={{ color: 'var(--text-muted)' }}>{o.orderId.substring(0, 8)}...</td>
                <td style={{ fontWeight: 600 }}>{o.symbol}</td>
                <td><span className={`badge ${o.side === 'Buy' ? 'badge-green' : 'badge-red'}`}>{o.side}</span></td>
                <td>{o.type}</td>
                <td>{o.price ? `$${o.price.toLocaleString()}` : 'Market'}</td>
                <td>{o.quantity}</td>
                <td>{o.filledQuantity}</td>
                <td>{statusBadge(o.status)}</td>
                <td style={{ color: 'var(--text-muted)' }}>{new Date(o.createdAt).toLocaleTimeString()}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  )
}
