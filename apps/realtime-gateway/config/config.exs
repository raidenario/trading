import Config

port =
  "PORT"
  |> System.get_env("4000")
  |> String.trim()
  |> String.to_integer()

config :realtime_gateway,
  ecto_repos: []

config :realtime_gateway, RealtimeGatewayWeb.Endpoint,
  http: [ip: {0, 0, 0, 0}, port: port],
  url: [host: "localhost", port: port],
  render_errors: [formats: [json: RealtimeGatewayWeb.ErrorJSON], layout: false],
  pubsub_server: RealtimeGateway.PubSub,
  secret_key_base: "realtime-gateway-dev-secret-key-base-please-change",
  server: true

config :phoenix, :json_library, Jason

env_config = "#{config_env()}.exs"

if File.exists?(Path.expand(env_config, __DIR__)) do
  import_config env_config
end
