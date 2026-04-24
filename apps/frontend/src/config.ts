// ══════════════════════════════════════════════════════════
// Environment configuration
// ══════════════════════════════════════════════════════════
// Reads from Vite env vars. In dev, the Vite proxy handles
// routing, so we use relative paths. The env vars are only
// needed if the frontend is served from a separate origin.

export const config = {
  /** Base URL for the Gateway API (order submission, accounts) */
  gatewayApiBase: import.meta.env.VITE_GATEWAY_API_BASE_URL as string || '',

  /** Base URL for the Query API (read projections) */
  queryApiBase: import.meta.env.VITE_QUERY_API_BASE_URL as string || '/query-api',

  /** WebSocket URL for Phoenix Channels Realtime Gateway */
  realtimeSocketUrl: import.meta.env.VITE_REALTIME_SOCKET_URL as string || 'ws://localhost:4000/socket',
} as const
