import Config

config :realtime_gateway,
  ecto_repos: []

config :realtime_gateway, RealtimeGatewayWeb.Endpoint,
  url: [host: "localhost"],
  render_errors: [formats: [json: RealtimeGatewayWeb.ErrorJSON], layout: false],
  pubsub_server: RealtimeGateway.PubSub,
  secret_key_base: "realtime-gateway-dev-secret-key-base-please-change",
  server: true

config :phoenix, :json_library, Jason
