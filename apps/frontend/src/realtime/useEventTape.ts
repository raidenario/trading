// ══════════════════════════════════════════════════════════
// useEventTape — global realtime event log
// ══════════════════════════════════════════════════════════

import { useState, useCallback, useRef } from 'react'
import type { EventTapeEntry, RealtimeEventType } from '../types'

const MAX_ENTRIES = 200

export function useEventTape() {
  const [entries, setEntries] = useState<EventTapeEntry[]>([])
  const idCounter = useRef(0)

  const pushEvent = useCallback(
    (symbol: string, eventType: RealtimeEventType, payload: Record<string, unknown>) => {
      const entry: EventTapeEntry = {
        id: `evt-${++idCounter.current}`,
        timestamp: new Date().toISOString(),
        symbol,
        eventType,
        payload,
      }
      setEntries((prev) => [entry, ...prev].slice(0, MAX_ENTRIES))
    },
    [],
  )

  const clearEvents = useCallback(() => {
    setEntries([])
  }, [])

  return { entries, pushEvent, clearEvents }
}
