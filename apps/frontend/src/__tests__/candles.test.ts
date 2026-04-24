import { describe, expect, it } from 'vitest'
import { buildChartCandles } from '../market/candles'
import type { CandleSnapshot, RealtimeCandleUpdate } from '../types'

describe('buildChartCandles', () => {
  it('accepts query-api candles that use openedAt/closedAt', () => {
    const initial: CandleSnapshot = {
      symbol: 'PETR4',
      interval: '1m',
      open: 24.79,
      high: 24.79,
      low: 24.79,
      close: 24.79,
      volume: 0,
      openedAt: '2026-04-24T06:25:52.776917+00:00',
      closedAt: '2026-04-24T06:24:59.3087835+00:00',
    }

    const result = buildChartCandles([initial], [], 'PETR4')

    expect(result).toHaveLength(1)
    expect(result[0].time).toBe(1_777_011_952)
  })

  it('returns multiple sorted candles and keeps the latest update for the same bucket', () => {
    const realtime: RealtimeCandleUpdate[] = [
      {
        symbol: 'PETR4',
        interval: '1m',
        open: 24.79,
        high: 24.95,
        low: 24.70,
        close: 24.90,
        volume: 100,
        open_time: '2026-04-24T06:26:00Z',
        close_time: '2026-04-24T06:26:59Z',
      },
      {
        symbol: 'PETR4',
        interval: '1m',
        open: 24.90,
        high: 25.10,
        low: 24.88,
        close: 25.05,
        volume: 125,
        open_time: '2026-04-24T06:27:00Z',
        close_time: '2026-04-24T06:27:59Z',
      },
      {
        symbol: 'PETR4',
        interval: '1m',
        open: 24.79,
        high: 24.98,
        low: 24.69,
        close: 24.91,
        volume: 101,
        open_time: '2026-04-24T06:26:00Z',
        close_time: '2026-04-24T06:26:59Z',
      },
    ]

    const result = buildChartCandles([], realtime, 'PETR4')

    expect(result).toHaveLength(2)
    expect(result.map((c) => c.time)).toEqual([1_777_011_960, 1_777_012_020])
    expect(result[0].close).toBe(24.91)
    expect(result[1].close).toBe(25.05)
  })

  it('filters out invalid timestamps instead of emitting broken chart points', () => {
    const initial: CandleSnapshot = {
      symbol: 'PETR4',
      interval: '1m',
      open: 24.79,
      high: 24.79,
      low: 24.79,
      close: 24.79,
      volume: 0,
      openTime: 'not-a-date',
      closeTime: 'also-not-a-date',
    }

    const realtime: RealtimeCandleUpdate[] = [
      {
        symbol: 'PETR4',
        interval: '1m',
        open: 24.90,
        high: 25.10,
        low: 24.88,
        close: 25.05,
        volume: 125,
        open_time: '2026-04-24T06:27:00Z',
        close_time: '2026-04-24T06:27:59Z',
      },
      {
        symbol: 'PETR4',
        interval: '1m',
        open: 25.05,
        high: 25.20,
        low: 25.00,
        close: 25.12,
        volume: 110,
        open_time: 'invalid',
        close_time: '2026-04-24T06:28:59Z',
      },
    ]

    const result = buildChartCandles([initial], realtime, 'PETR4')

    expect(result).toHaveLength(1)
    expect(result[0].time).toBe(1_777_012_020)
  })

  it('keeps multiple historical candles before applying realtime updates', () => {
    const history: CandleSnapshot[] = [
      {
        symbol: 'PETR4',
        interval: '1m',
        open: 24.10,
        high: 24.30,
        low: 24.00,
        close: 24.20,
        volume: 300,
        openedAt: '2026-04-24T13:00:00Z',
        closedAt: '2026-04-24T13:00:59Z',
      },
      {
        symbol: 'PETR4',
        interval: '1m',
        open: 24.20,
        high: 24.40,
        low: 24.15,
        close: 24.35,
        volume: 280,
        openedAt: '2026-04-24T13:01:00Z',
        closedAt: '2026-04-24T13:01:59Z',
      },
    ]

    const result = buildChartCandles(history, [], 'PETR4')

    expect(result).toHaveLength(2)
    expect(result.map((c) => c.time)).toEqual([1_777_035_600, 1_777_035_660])
  })
})
