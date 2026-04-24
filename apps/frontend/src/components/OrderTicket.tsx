import { useState } from 'react'
import { createOrder } from '../api/gatewayApi'
import type { BookSnapshot, CreateOrderPayload } from '../types'

interface Props {
  symbol: string
  accountId: string
  onBookUpdate?: (book: BookSnapshot) => void
}

type SubmitState = 'idle' | 'sending' | 'success' | 'error'

export function OrderTicket({ symbol, accountId, onBookUpdate }: Props) {
  const [side, setSide] = useState<'Buy' | 'Sell'>('Buy')
  const [price, setPrice] = useState('')
  const [quantity, setQuantity] = useState('')
  const [submitState, setSubmitState] = useState<SubmitState>('idle')
  const [message, setMessage] = useState('')

  const errors = validate(price, quantity)
  const hasErrors = Object.values(errors).some(Boolean)

  const handleSubmit = async () => {
    if (hasErrors || !accountId) return

    setSubmitState('sending')
    setMessage('')

    const payload: CreateOrderPayload = {
      orderId: crypto.randomUUID(),
      accountId,
      symbol,
      side,
      type: 'Limit',
      quantity: parseFloat(quantity),
      price: parseFloat(price),
      timeInForce: 'Gtc',
      clientOrderId: `web-${Date.now()}`,
      submittedAt: new Date().toISOString(),
      schemaVersion: 1,
    }

    try {
      const result = await createOrder(payload)
      if (result.book) {
        onBookUpdate?.(result.book)
      }
      setSubmitState('success')
      setMessage(`Order ${result.status} — ${result.orderId.substring(0, 8)}…`)
      setPrice('')
      setQuantity('')
      setTimeout(() => setSubmitState('idle'), 4000)
    } catch (err: unknown) {
      setSubmitState('error')
      setMessage(err instanceof Error ? err.message : 'Unknown error')
    }
  }

  return (
    <div className="order-ticket" id="order-ticket">
      <div className="side-toggle">
        <button
          className={`side-toggle__btn side-toggle__btn--buy${side === 'Buy' ? ' active' : ''}`}
          onClick={() => setSide('Buy')}
          id="order-side-buy"
        >
          BUY
        </button>
        <button
          className={`side-toggle__btn side-toggle__btn--sell${side === 'Sell' ? ' active' : ''}`}
          onClick={() => setSide('Sell')}
          id="order-side-sell"
        >
          SELL
        </button>
      </div>

      <div className="form-group">
        <label className="form-label">Symbol</label>
        <div className="symbol-select-shell">
          <span className="asset-avatar asset-avatar--sm">{symbol.slice(0, 2).toUpperCase()}</span>
          <input className="form-input form-input--ghost" value={symbol} readOnly id="order-symbol" />
          <span className="select-chevron">⌄</span>
        </div>
      </div>

      <div className="form-group">
        <label className="form-label">Price</label>
        <div className="input-with-unit">
          <input
            className="form-input"
            type="number"
            step="any"
            placeholder="0,00"
            value={price}
            onChange={(e) => setPrice(e.target.value)}
            id="order-price"
          />
          <span>USD</span>
        </div>
        {errors.price && <span className="form-error">{errors.price}</span>}
      </div>

      <div className="form-group">
        <label className="form-label">Quantity</label>
        <div className="input-with-unit">
          <input
            className="form-input"
            type="number"
            step="any"
            placeholder="0"
            value={quantity}
            onChange={(e) => setQuantity(e.target.value)}
            id="order-quantity"
          />
          <span>{symbol.split('-')[0] || symbol}</span>
        </div>
        {errors.quantity && <span className="form-error">{errors.quantity}</span>}
      </div>

      <button
        className={`btn btn--full ${side === 'Buy' ? 'btn--buy' : 'btn--sell'}`}
        onClick={handleSubmit}
        disabled={hasErrors || submitState === 'sending' || !accountId}
        id="order-submit"
      >
        {submitState === 'sending' ? 'Sending…' : `${side} ${symbol.split('-')[0] || symbol}`}
      </button>

      {message && (
        <div
          className={submitState === 'error' ? 'error-banner' : 'text-buy'}
          style={{ fontSize: 11, textAlign: 'center' }}
          id="order-feedback"
        >
          {message}
        </div>
      )}

      {!accountId && (
        <div className="error-banner" style={{ fontSize: 10 }}>
          No account selected — cannot submit orders
        </div>
      )}
    </div>
  )
}

function validate(price: string, quantity: string): { price?: string; quantity?: string } {
  const errors: { price?: string; quantity?: string } = {}
  if (price && (isNaN(Number(price)) || Number(price) <= 0)) {
    errors.price = 'Must be a positive number'
  }
  if (quantity && (isNaN(Number(quantity)) || Number(quantity) <= 0)) {
    errors.quantity = 'Must be a positive number'
  }
  if (!price) errors.price = 'Required for Limit orders'
  if (!quantity) errors.quantity = 'Required'
  return errors
}
