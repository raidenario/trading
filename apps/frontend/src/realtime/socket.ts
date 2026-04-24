// ══════════════════════════════════════════════════════════
// Phoenix Socket Singleton
// ══════════════════════════════════════════════════════════

import { Socket } from 'phoenix'
import { config } from '../config'

let socket: Socket | null = null

export function getSocket(): Socket {
  if (!socket) {
    socket = new Socket(config.realtimeSocketUrl, {
      params: {},
      heartbeatIntervalMs: 30_000,
    })
    socket.connect()
  }
  return socket
}

export function disconnectSocket(): void {
  if (socket) {
    socket.disconnect()
    socket = null
  }
}
