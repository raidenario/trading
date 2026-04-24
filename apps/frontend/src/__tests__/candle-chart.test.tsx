import { describe, beforeEach, expect, it, vi } from 'vitest'
import { render } from '@testing-library/react'
import { CandleChart } from '../components/CandleChart'
import type { CandleSnapshot, RealtimeCandleUpdate } from '../types'

const setData = vi.fn()
const update = vi.fn()
const fitContent = vi.fn()
const applyOptions = vi.fn()
const remove = vi.fn()

vi.mock('lightweight-charts', () => ({
  CandlestickSeries: Symbol('CandlestickSeries'),
  createChart: vi.fn(() => ({
    addSeries: vi.fn(() => ({
      setData,
      update,
    })),
    applyOptions,
    remove,
    timeScale: () => ({
      fitContent,
    }),
  })),
}))

describe('CandleChart', () => {
  beforeEach(() => {
    setData.mockReset()
    update.mockReset()
    fitContent.mockReset()
    applyOptions.mockReset()
    remove.mockReset()
  })

  it('loads history once and applies realtime candles incrementally', async () => {
    const history: CandleSnapshot[] = [
      {
        symbol: 'PETR4',
        interval: '1m',
        open: 24.1,
        high: 24.3,
        low: 24,
        close: 24.2,
        volume: 200,
        openedAt: '2026-04-24T13:00:00Z',
        closedAt: '2026-04-24T13:00:59Z',
      },
    ]

    const realtime: RealtimeCandleUpdate[] = [
      {
        symbol: 'PETR4',
        interval: '1m',
        open: 24.2,
        high: 24.5,
        low: 24.1,
        close: 24.45,
        volume: 250,
        open_time: '2026-04-24T13:01:00Z',
        close_time: '2026-04-24T13:01:59Z',
      },
    ]
    const revisedRealtime: RealtimeCandleUpdate[] = [
      {
        ...realtime[0],
        high: 24.6,
        close: 24.55,
      },
    ]

    const { rerender } = render(
      <CandleChart initialCandles={history} realtimeCandles={[]} symbol="PETR4" />,
    )

    expect(setData).toHaveBeenCalledTimes(1)
    expect(update).not.toHaveBeenCalled()

    rerender(
      <CandleChart initialCandles={history} realtimeCandles={realtime} symbol="PETR4" />,
    )

    await new Promise((resolve) => setTimeout(resolve, 0))

    expect(setData).toHaveBeenCalledTimes(1)
    expect(update).toHaveBeenCalledTimes(1)
    expect(fitContent).toHaveBeenCalledTimes(1)

    rerender(
      <CandleChart initialCandles={history} realtimeCandles={revisedRealtime} symbol="PETR4" />,
    )

    await new Promise((resolve) => setTimeout(resolve, 0))

    expect(update).toHaveBeenCalledTimes(2)
  })

  it('moves the active candle from the latest realtime trade', async () => {
    const history: CandleSnapshot[] = [
      {
        symbol: 'PETR4',
        interval: '1m',
        open: 24.1,
        high: 24.3,
        low: 24,
        close: 24.2,
        volume: 200,
        openedAt: '2026-04-24T13:00:00Z',
        closedAt: '2026-04-24T13:00:59Z',
      },
    ]

    render(
      <CandleChart
        initialCandles={history}
        realtimeCandles={[]}
        latestTrade={{
          tradeId: 'trade-1',
          symbol: 'PETR4',
          price: 24.45,
          quantity: 10,
          side: 'Buy',
          executedAt: '2026-04-24T13:00:30Z',
        }}
        symbol="PETR4"
      />,
    )

    await new Promise((resolve) => setTimeout(resolve, 0))

    expect(update).toHaveBeenCalled()
  })

  it('resets the live series when a regenerated candle stream rewinds in time', async () => {
    const history: CandleSnapshot[] = [
      {
        symbol: 'PETR4',
        interval: '1m',
        open: 24.1,
        high: 24.3,
        low: 24,
        close: 24.2,
        volume: 200,
        openedAt: '2026-04-24T13:40:00Z',
        closedAt: '2026-04-24T13:40:59Z',
      },
    ]

    const rewind: RealtimeCandleUpdate[] = [
      {
        symbol: 'PETR4',
        interval: '1m',
        open: 23.9,
        high: 24.1,
        low: 23.8,
        close: 24,
        volume: 100,
        open_time: '2026-04-24T13:00:00Z',
        close_time: '2026-04-24T13:00:59Z',
      },
    ]

    const { rerender } = render(
      <CandleChart initialCandles={history} realtimeCandles={[]} symbol="PETR4" />,
    )

    setData.mockClear()
    rerender(
      <CandleChart initialCandles={history} realtimeCandles={rewind} symbol="PETR4" />,
    )

    await new Promise((resolve) => setTimeout(resolve, 0))

    expect(setData).toHaveBeenCalledWith([
      {
        time: 1_777_035_600,
        open: 23.9,
        high: 24.1,
        low: 23.8,
        close: 24,
      },
    ])
  })
})
