// ══════════════════════════════════════════════════════════
// Realtime Hook Tests
// ══════════════════════════════════════════════════════════

import { describe, it, expect, vi } from 'vitest'
import { renderHook, act } from '@testing-library/react'
import { useEventTape } from '../realtime/useEventTape'

describe('useEventTape', () => {
  it('starts with empty entries', () => {
    const { result } = renderHook(() => useEventTape())
    expect(result.current.entries).toEqual([])
  })

  it('pushEvent adds entry at the beginning', () => {
    const { result } = renderHook(() => useEventTape())

    act(() => {
      result.current.pushEvent('PETR4', 'ticker_update', { last_price: 32.5 })
    })

    expect(result.current.entries).toHaveLength(1)
    expect(result.current.entries[0].symbol).toBe('PETR4')
    expect(result.current.entries[0].eventType).toBe('ticker_update')
    expect(result.current.entries[0].payload).toEqual({ last_price: 32.5 })
  })

  it('pushEvent prepends new entries (newest first)', () => {
    const { result } = renderHook(() => useEventTape())

    act(() => {
      result.current.pushEvent('PETR4', 'ticker_update', { last_price: 32 })
    })
    act(() => {
      result.current.pushEvent('VALE3', 'trade_update', { price: 68 })
    })

    expect(result.current.entries).toHaveLength(2)
    expect(result.current.entries[0].symbol).toBe('VALE3')
    expect(result.current.entries[1].symbol).toBe('PETR4')
  })

  it('caps entries at 200', () => {
    const { result } = renderHook(() => useEventTape())

    act(() => {
      for (let i = 0; i < 210; i++) {
        result.current.pushEvent('SYM', 'ticker_update', { i })
      }
    })

    expect(result.current.entries.length).toBeLessThanOrEqual(200)
  })

  it('clearEvents empties the list', () => {
    const { result } = renderHook(() => useEventTape())

    act(() => {
      result.current.pushEvent('PETR4', 'ticker_update', {})
      result.current.pushEvent('VALE3', 'trade_update', {})
    })

    expect(result.current.entries).toHaveLength(2)

    act(() => {
      result.current.clearEvents()
    })

    expect(result.current.entries).toHaveLength(0)
  })

  it('assigns unique IDs to each entry', () => {
    const { result } = renderHook(() => useEventTape())

    act(() => {
      result.current.pushEvent('A', 'ticker_update', {})
      result.current.pushEvent('B', 'trade_update', {})
      result.current.pushEvent('C', 'book_update', {})
    })

    const ids = result.current.entries.map((e) => e.id)
    expect(new Set(ids).size).toBe(3)
  })

  it('each entry has a timestamp', () => {
    vi.useFakeTimers()
    vi.setSystemTime(new Date('2026-01-15T10:00:00Z'))

    const { result } = renderHook(() => useEventTape())

    act(() => {
      result.current.pushEvent('PETR4', 'candle_update', {})
    })

    expect(result.current.entries[0].timestamp).toBe('2026-01-15T10:00:00.000Z')
    vi.useRealTimers()
  })
})
