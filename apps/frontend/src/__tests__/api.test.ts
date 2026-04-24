// ══════════════════════════════════════════════════════════
// API Client Tests
// ══════════════════════════════════════════════════════════

import { describe, it, expect, vi, beforeEach } from 'vitest'
import { fetchJson, HttpError, postJson } from '../api/client'

// Mock global fetch
const mockFetch = vi.fn()
beforeEach(() => {
  vi.stubGlobal('fetch', mockFetch)
  mockFetch.mockReset()
})

describe('fetchJson', () => {
  it('returns parsed JSON on success', async () => {
    mockFetch.mockResolvedValue({
      ok: true,
      json: () => Promise.resolve({ foo: 'bar' }),
    })

    const result = await fetchJson<{ foo: string }>('/test')
    expect(result).toEqual({ foo: 'bar' })
    expect(mockFetch).toHaveBeenCalledWith('/test', undefined)
  })

  it('throws HttpError on non-2xx status', async () => {
    mockFetch.mockResolvedValue({
      ok: false,
      status: 404,
      statusText: 'Not Found',
      text: () => Promise.resolve('not found'),
    })

    await expect(fetchJson('/test')).rejects.toThrow(HttpError)
    await expect(fetchJson('/test')).rejects.toThrow('HTTP 404')
  })

  it('includes status in HttpError', async () => {
    mockFetch.mockResolvedValue({
      ok: false,
      status: 500,
      statusText: 'Internal Server Error',
      text: () => Promise.resolve('server error'),
    })

    try {
      await fetchJson('/test')
      expect.fail('Should have thrown')
    } catch (err) {
      expect(err).toBeInstanceOf(HttpError)
      expect((err as HttpError).status).toBe(500)
    }
  })
})

describe('postJson', () => {
  it('sends POST with JSON body', async () => {
    mockFetch.mockResolvedValue({
      ok: true,
      json: () => Promise.resolve({ id: '123' }),
    })

    const result = await postJson<{ id: string }>('/api/orders', { symbol: 'PETR4' })
    expect(result).toEqual({ id: '123' })

    const [url, options] = mockFetch.mock.calls[0]
    expect(url).toBe('/api/orders')
    expect(options.method).toBe('POST')
    expect(options.headers['Content-Type']).toBe('application/json')
    expect(JSON.parse(options.body)).toEqual({ symbol: 'PETR4' })
  })
})

// ── URL construction tests ─────────────────────────────────
describe('Query API URL construction', () => {
  it('builds instrument URL correctly', async () => {
    mockFetch.mockResolvedValue({ ok: true, json: () => Promise.resolve([]) })

    const { getInstruments } = await import('../api/queryApi')
    await getInstruments()

    expect(mockFetch.mock.calls[0][0]).toBe('/query-api/api/instruments')
  })

  it('builds ticker URL with symbol encoding', async () => {
    mockFetch.mockResolvedValue({ ok: true, json: () => Promise.resolve({ ticker: {}, candle: {} }) })

    const { getTicker } = await import('../api/queryApi')
    await getTicker('BTC-USD')

    expect(mockFetch.mock.calls[0][0]).toBe('/query-api/api/markets/BTC-USD/ticker')
  })

  it('builds recent trades URL with params', async () => {
    mockFetch.mockResolvedValue({ ok: true, json: () => Promise.resolve([]) })

    const { getRecentTrades } = await import('../api/queryApi')
    await getRecentTrades('PETR4', 30)

    expect(mockFetch.mock.calls[0][0]).toBe('/query-api/api/trades/recent?symbol=PETR4&limit=30')
  })

  it('builds candles URL with interval and limit', async () => {
    mockFetch.mockResolvedValue({ ok: true, json: () => Promise.resolve([]) })

    const { getCandles } = await import('../api/queryApi')
    await getCandles('PETR4', '1m', 300)

    expect(mockFetch.mock.calls[0][0]).toBe('/query-api/api/markets/PETR4/candles?interval=1m&limit=300')
  })

  it('builds positions URL with optional accountId', async () => {
    mockFetch.mockResolvedValue({ ok: true, json: () => Promise.resolve([]) })

    const { getPositions } = await import('../api/queryApi')
    await getPositions()
    expect(mockFetch.mock.calls[0][0]).toBe('/query-api/api/positions')

    mockFetch.mockClear()
    mockFetch.mockResolvedValue({ ok: true, json: () => Promise.resolve([]) })
    await getPositions('abc-123')
    expect(mockFetch.mock.calls[0][0]).toBe('/query-api/api/positions?tradingAccountId=abc-123')
  })
})
