import type { CandlestickData, Time, UTCTimestamp } from 'lightweight-charts'
import type { CandleSnapshot, RealtimeCandleUpdate } from '../types'

type CandleTimestampAliases = {
  open_time?: string
  opened_at?: string
  openTime?: string
  openedAt?: string
  close_time?: string
  closed_at?: string
  closeTime?: string
  closedAt?: string
}

type CandleLike = (CandleSnapshot | RealtimeCandleUpdate) & CandleTimestampAliases

export function buildChartCandles(
  initialCandles: CandleSnapshot[],
  realtimeCandles: RealtimeCandleUpdate[],
  symbol: string,
): CandlestickData<Time>[] {
  const normalizedSymbol = symbol.toUpperCase()
  const deduped = new Map<number, CandlestickData<Time>>()

  for (const candle of [...initialCandles, ...realtimeCandles]) {
    if (!candle) continue
    if ((candle.symbol ?? '').toUpperCase() !== normalizedSymbol) continue

    const normalized = normalizeChartCandle(candle)
    if (!normalized) continue

    deduped.set(normalized.time as number, normalized)
  }

  return [...deduped.values()].sort((a, b) => (a.time as number) - (b.time as number))
}

export function buildHistoricalChartCandles(
  candles: CandleSnapshot[],
  symbol: string,
): CandlestickData<Time>[] {
  return buildChartCandles(candles, [], symbol)
}

export function buildRealtimeChartCandles(
  candles: RealtimeCandleUpdate[],
  symbol: string,
): CandlestickData<Time>[] {
  return buildChartCandles([], candles, symbol)
}

export function getCandleTimestampKey(candle: CandleLike): number | null {
  const timestamp = resolveCandleTimestamp(candle)
  return timestamp === null ? null : (timestamp as number)
}

function normalizeChartCandle(candle: CandleLike): CandlestickData<Time> | null {
  const timestamp = resolveCandleTimestamp(candle)
  if (timestamp === null) return null

  if (![candle.open, candle.high, candle.low, candle.close].every(Number.isFinite)) {
    return null
  }

  return {
    time: timestamp,
    open: candle.open,
    high: candle.high,
    low: candle.low,
    close: candle.close,
  }
}

function resolveCandleTimestamp(candle: CandleLike): UTCTimestamp | null {
  const openField = firstDefined([
    candle.open_time,
    candle.opened_at,
    candle.openTime,
    candle.openedAt,
  ])

  if (openField !== undefined) {
    return parseIsoTimestamp(openField)
  }

  const closeField = firstDefined([
    candle.close_time,
    candle.closed_at,
    candle.closeTime,
    candle.closedAt,
  ])

  if (closeField !== undefined) {
    return parseIsoTimestamp(closeField)
  }

  return null
}

function parseIsoTimestamp(value: string): UTCTimestamp | null {
  const parsed = Date.parse(value)
  if (!Number.isFinite(parsed)) return null
  return Math.floor(parsed / 1000) as UTCTimestamp
}

function firstDefined(values: Array<string | undefined>): string | undefined {
  return values.find((value) => value !== undefined)
}
