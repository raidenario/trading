import Config

port =
  "PORT"
  |> System.get_env("4000")
  |> String.trim()
  |> String.to_integer()

host =
  "PHX_HOST"
  |> System.get_env("localhost")
  |> String.trim()

config :realtime_gateway, RealtimeGatewayWeb.Endpoint,
  http: [ip: {0, 0, 0, 0}, port: port],
  url: [host: host, port: port]

if System.get_env("PHX_SERVER") do
  config :realtime_gateway, RealtimeGatewayWeb.Endpoint, server: true
end
