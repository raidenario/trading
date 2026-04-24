// ══════════════════════════════════════════════════════════
// useMarketChannel — subscribe to market:{SYMBOL}
// ══════════════════════════════════════════════════════════
// Joins the Phoenix channel for the selected symbol.
// On symbol change, leaves the old channel and joins the new one.
// Invokes callbacks for each event type.

import { useEffect, useRef } from 'react'
import type { Channel } from 'phoenix'
import { getSocket } from './socket'
import type {
  RealtimeTickerUpdate,
  RealtimeTradeUpdate,
  RealtimeBookUpdate,
  RealtimeCandleUpdate,
} from '../types'

export interface MarketChannelCallbacks {
  onTicker?: (data: RealtimeTickerUpdate) => void
  onTrade?: (data: RealtimeTradeUpdate) => void
  onBook?: (data: RealtimeBookUpdate) => void
  onCandle?: (data: RealtimeCandleUpdate) => void
  onJoin?: (symbol: string) => void
  onError?: (error: unknown) => void
}

export function useMarketChannel(symbol: string, callbacks: MarketChannelCallbacks): void {
  const channelRef = useRef<Channel | null>(null)
  const callbacksRef = useRef(callbacks)
  callbacksRef.current = callbacks

  useEffect(() => {
    if (!symbol) return

    const socket = getSocket()
    const topic = `market:${symbol.toUpperCase()}`
    const channel = socket.channel(topic, {})

    channel.on('ticker_update', (payload: RealtimeTickerUpdate) => {
      callbacksRef.current.onTicker?.(payload)
    })

    channel.on('trade_update', (payload: RealtimeTradeUpdate) => {
      callbacksRef.current.onTrade?.(payload)
    })

    channel.on('book_update', (payload: RealtimeBookUpdate) => {
      callbacksRef.current.onBook?.(payload)
    })

    channel.on('candle_update', (payload: RealtimeCandleUpdate) => {
      callbacksRef.current.onCandle?.(payload)
    })

    channel
      .join()
      .receive('ok', () => {
        callbacksRef.current.onJoin?.(symbol)
      })
      .receive('error', (reason: unknown) => {
        callbacksRef.current.onError?.(reason)
      })

    channelRef.current = channel

    return () => {
      channel.leave()
      channelRef.current = null
    }
  }, [symbol])
}
