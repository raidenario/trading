import { useEffect, useMemo, useRef } from 'react'
import {
  createChart,
  type CandlestickData,
  type IChartApi,
  type ISeriesApi,
  CandlestickSeries,
  type Time,
} from 'lightweight-charts'
import type { CandleSnapshot, RecentTrade, RealtimeCandleUpdate } from '../types'
import {
  buildHistoricalChartCandles,
  buildLiveChartCandleFromTrade,
  buildRealtimeChartCandles,
} from '../market/candles'

interface Props {
  initialCandles: CandleSnapshot[]
  realtimeCandles: RealtimeCandleUpdate[]
  latestTrade?: RecentTrade | null
  symbol: string
}

const LIVE_CANDLE_ANIMATION_MS = 160
const REWIND_RESET_THRESHOLD_SECONDS = 90

/**
 * Loads historical candles from the Query API and appends realtime
 * `candle_update` events as they arrive from the Realtime Gateway.
 */
export function CandleChart({ initialCandles, realtimeCandles, latestTrade, symbol }: Props) {
  const containerRef = useRef<HTMLDivElement>(null)
  const chartRef = useRef<IChartApi | null>(null)
  const seriesRef = useRef<ISeriesApi<'Candlestick'> | null>(null)
  const historySignatureRef = useRef<string>('')
  const appliedRealtimeSignaturesRef = useRef<Map<number, string>>(new Map())
  const canonicalCandlesRef = useRef<Map<number, CandlestickData<Time>>>(new Map())
  const visibleCandlesRef = useRef<Map<number, CandlestickData<Time>>>(new Map())
  const liveAnimationFrameRef = useRef<number | null>(null)
  const latestTradeKeyRef = useRef<string>('')

  const historicalData = useMemo(
    () => buildHistoricalChartCandles(initialCandles, symbol),
    [initialCandles, symbol],
  )

  // Create chart on mount
  useEffect(() => {
    if (!containerRef.current) return

    const chart = createChart(containerRef.current, {
      layout: {
        background: { color: '#141821' },
        textColor: '#848e9c',
        fontFamily: "'IBM Plex Sans', sans-serif",
        fontSize: 10,
      },
      grid: {
        vertLines: { color: 'rgba(30, 37, 48, 0.5)' },
        horzLines: { color: 'rgba(30, 37, 48, 0.5)' },
      },
      crosshair: {
        mode: 0,
        vertLine: { color: 'rgba(240, 185, 11, 0.3)' },
        horzLine: { color: 'rgba(240, 185, 11, 0.3)' },
      },
      rightPriceScale: {
        borderColor: '#1e2530',
      },
      timeScale: {
        borderColor: '#1e2530',
        timeVisible: true,
      },
    })

    const series = chart.addSeries(CandlestickSeries, {
      upColor: '#0ecb81',
      downColor: '#f6465d',
      borderUpColor: '#0ecb81',
      borderDownColor: '#f6465d',
      wickUpColor: '#0ecb81',
      wickDownColor: '#f6465d',
    })

    chartRef.current = chart
    seriesRef.current = series

    const handleResize = () => {
      if (containerRef.current) {
        chart.applyOptions({ width: containerRef.current.clientWidth })
      }
    }
    const resizeObserver = new ResizeObserver(handleResize)
    resizeObserver.observe(containerRef.current)

    return () => {
      resizeObserver.disconnect()
      chart.remove()
      chartRef.current = null
      seriesRef.current = null
    }
  }, [])

  // Reset the full dataset only when the historical window changes.
  useEffect(() => {
    if (!seriesRef.current) return

    const lastHistoricalCandle = historicalData[historicalData.length - 1]
    const signature = `${symbol}:${historicalData.length}:${historicalData[0]?.time ?? 'none'}:${lastHistoricalCandle?.time ?? 'none'}:${lastHistoricalCandle?.close ?? 'none'}`
    if (historySignatureRef.current === signature) return

    seriesRef.current.setData(historicalData)
    canonicalCandlesRef.current = indexCandlesByTime(historicalData)
    visibleCandlesRef.current = indexCandlesByTime(historicalData)
    appliedRealtimeSignaturesRef.current = new Map()
    historySignatureRef.current = signature

    if (chartRef.current && historicalData.length > 0) {
      chartRef.current.timeScale().fitContent()
    }
  }, [historicalData, symbol])

  // Apply realtime candles incrementally to avoid resetting the chart every tick.
  useEffect(() => {
    if (!seriesRef.current) return

    const normalized = buildRealtimeChartCandles(realtimeCandles, symbol)
    const changedCandles = normalized.filter((candle) => {
      const time = candle.time as number
      return appliedRealtimeSignaturesRef.current.get(time) !== candleSignature(candle)
    })
    if (changedCandles.length === 0) return

    const frame = requestAnimationFrame(() => {
      for (const candle of changedCandles) {
        commitCandleUpdate(candle, false, false)
        appliedRealtimeSignaturesRef.current.set(candle.time as number, candleSignature(candle))
      }
    })

    return () => cancelAnimationFrame(frame)
  }, [realtimeCandles, symbol])

  // Use trade prints to move the in-progress candle between official candle updates.
  useEffect(() => {
    if (!latestTrade) return

    const tradeKey = `${latestTrade.tradeId}:${latestTrade.executedAt}:${latestTrade.price}:${latestTrade.quantity}`
    if (latestTradeKeyRef.current === tradeKey) return
    latestTradeKeyRef.current = tradeKey

    const executedAt = Date.parse(latestTrade.executedAt)
    if (!Number.isFinite(executedAt)) return

    const bucketTime = Math.floor(executedAt / 60_000) * 60
    const baseCandle = canonicalCandlesRef.current.get(bucketTime)
    const liveCandle = buildLiveChartCandleFromTrade(latestTrade, symbol, baseCandle)
    if (!liveCandle) return

    commitCandleUpdate(liveCandle, true, true)
  }, [latestTrade, symbol])

  useEffect(() => {
    return () => {
      if (liveAnimationFrameRef.current !== null) {
        cancelAnimationFrame(liveAnimationFrameRef.current)
      }
    }
  }, [])

  function commitCandleUpdate(
    target: CandlestickData<Time>,
    animated: boolean,
    preserveExistingExtremes: boolean,
  ) {
    const series = seriesRef.current
    if (!series) return

    const time = target.time as number
    const latestVisibleTime = getLatestTime(visibleCandlesRef.current)
    const previousCanonical = canonicalCandlesRef.current.get(time)
    const nextCandle = preserveExistingExtremes
      ? mergeCandleExtremes(previousCanonical, target)
      : target

    if (latestVisibleTime !== null && time < latestVisibleTime - REWIND_RESET_THRESHOLD_SECONDS) {
      canonicalCandlesRef.current = new Map([[time, nextCandle]])
      visibleCandlesRef.current = new Map([[time, nextCandle]])
      appliedRealtimeSignaturesRef.current = new Map()
      series.setData([nextCandle])
      return
    }

    canonicalCandlesRef.current.set(time, nextCandle)

    const currentVisible = visibleCandlesRef.current.get(time)
    if (latestVisibleTime !== null && time < latestVisibleTime) {
      visibleCandlesRef.current.set(time, nextCandle)
      series.setData(sortCandlesByTime([...visibleCandlesRef.current.values()]))
      return
    }

    if (!animated || !currentVisible || shouldReduceMotion()) {
      series.update(nextCandle)
      visibleCandlesRef.current.set(time, nextCandle)
      return
    }

    animateCandleUpdate(currentVisible, nextCandle)
  }

  function animateCandleUpdate(from: CandlestickData<Time>, to: CandlestickData<Time>) {
    const series = seriesRef.current
    if (!series) return

    if (liveAnimationFrameRef.current !== null) {
      cancelAnimationFrame(liveAnimationFrameRef.current)
    }

    const startedAt = performance.now()
    const time = to.time as number

    const tick = (now: number) => {
      const progress = Math.min((now - startedAt) / LIVE_CANDLE_ANIMATION_MS, 1)
      const eased = easeOutQuart(progress)
      const nextVisible: CandlestickData<Time> = {
        time: to.time,
        open: lerp(from.open, to.open, eased),
        high: lerp(from.high, to.high, eased),
        low: lerp(from.low, to.low, eased),
        close: lerp(from.close, to.close, eased),
      }

      series.update(nextVisible)
      visibleCandlesRef.current.set(time, nextVisible)

      if (progress < 1) {
        liveAnimationFrameRef.current = requestAnimationFrame(tick)
      } else {
        liveAnimationFrameRef.current = null
        visibleCandlesRef.current.set(time, to)
      }
    }

    liveAnimationFrameRef.current = requestAnimationFrame(tick)
  }

  return (
    <div
      ref={containerRef}
      id="candle-chart"
      style={{ width: '100%', height: '100%', minHeight: 250 }}
    />
  )
}

function indexCandlesByTime(candles: CandlestickData<Time>[]): Map<number, CandlestickData<Time>> {
  return new Map(candles.map((candle) => [candle.time as number, candle]))
}

function sortCandlesByTime(candles: CandlestickData<Time>[]): CandlestickData<Time>[] {
  return candles.sort((a, b) => (a.time as number) - (b.time as number))
}

function getLatestTime(candles: Map<number, CandlestickData<Time>>): number | null {
  if (candles.size === 0) return null
  return Math.max(...candles.keys())
}

function mergeCandleExtremes(
  previous: CandlestickData<Time> | undefined,
  next: CandlestickData<Time>,
): CandlestickData<Time> {
  if (!previous) return next

  return {
    ...next,
    open: previous.open,
    high: Math.max(previous.high, next.high),
    low: Math.min(previous.low, next.low),
  }
}

function shouldReduceMotion(): boolean {
  return window.matchMedia?.('(prefers-reduced-motion: reduce)').matches ?? false
}

function easeOutQuart(value: number): number {
  return 1 - Math.pow(1 - value, 4)
}

function lerp(from: number, to: number, progress: number): number {
  return from + (to - from) * progress
}

function candleSignature(candle: CandlestickData<Time>): string {
  return `${candle.time}:${candle.open}:${candle.high}:${candle.low}:${candle.close}`
}
