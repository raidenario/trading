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
  })
})
