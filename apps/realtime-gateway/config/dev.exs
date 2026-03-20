import Config

config :realtime_gateway, RealtimeGatewayWeb.Endpoint,
  http: [ip: {0, 0, 0, 0}, port: String.to_integer(System.get_env("PORT", "4000"))],
  code_reloader: false,
  debug_errors: true,
  check_origin: false,
  watchers: []
