// ══════════════════════════════════════════════════════════
// Environment configuration
// ══════════════════════════════════════════════════════════
// Reads from Vite env vars. In dev, the Vite proxy handles
// routing, so we use relative paths. The env vars are only
// needed if the frontend is served from a separate origin.

function envOrFallback(value: string | undefined, fallback: string): string {
  return value && value.trim().length > 0 ? value : fallback
}

export const config = {
  /** Base URL for the Gateway API (order submission, accounts) */
  gatewayApiBase: envOrFallback(import.meta.env.VITE_GATEWAY_API_BASE_URL as string | undefined, '/api'),

  /** Base URL for the Query API (read projections) */
  queryApiBase: envOrFallback(import.meta.env.VITE_QUERY_API_BASE_URL as string | undefined, '/query-api'),

  /** WebSocket URL for Phoenix Channels Realtime Gateway */
  realtimeSocketUrl: envOrFallback(import.meta.env.VITE_REALTIME_SOCKET_URL as string | undefined, '/socket'),
} as const
