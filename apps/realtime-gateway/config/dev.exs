import Config

config :realtime_gateway, RealtimeGatewayWeb.Endpoint,
  http: [
    ip: {0, 0, 0, 0},
    port: System.get_env("PORT", "4000") |> String.trim() |> String.to_integer()
  ],
  code_reloader: false,
  debug_errors: true,
  check_origin: false,
  watchers: []
