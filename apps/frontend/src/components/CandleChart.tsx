import { useEffect, useRef } from 'react'
import { createChart, type IChartApi, type ISeriesApi, CandlestickSeries } from 'lightweight-charts'
import type { CandleSnapshot, RealtimeCandleUpdate } from '../types'
import { buildChartCandles } from '../market/candles'

interface Props {
  initialCandles: CandleSnapshot[]
  realtimeCandles: RealtimeCandleUpdate[]
  symbol: string
}

/**
 * Loads historical candles from the Query API and appends realtime
 * `candle_update` events as they arrive from the Realtime Gateway.
 */
export function CandleChart({ initialCandles, realtimeCandles, symbol }: Props) {
  const containerRef = useRef<HTMLDivElement>(null)
  const chartRef = useRef<IChartApi | null>(null)
  const seriesRef = useRef<ISeriesApi<'Candlestick'> | null>(null)

  // Create chart on mount
  useEffect(() => {
    if (!containerRef.current) return

    const chart = createChart(containerRef.current, {
      layout: {
        background: { color: '#141821' },
        textColor: '#848e9c',
        fontFamily: "'JetBrains Mono', monospace",
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

  // Update data when symbol or candles change
  useEffect(() => {
    if (!seriesRef.current) return

    const candles = buildChartCandles(initialCandles, realtimeCandles, symbol)
    seriesRef.current.setData(candles)

    if (chartRef.current && candles.length > 0) {
      chartRef.current.timeScale().fitContent()
    }
  }, [initialCandles, realtimeCandles, symbol])

  return (
    <div
      ref={containerRef}
      id="candle-chart"
      style={{ width: '100%', height: '100%', minHeight: 250 }}
    />
  )
}
