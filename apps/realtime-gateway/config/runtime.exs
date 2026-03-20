import Config

if System.get_env("PHX_SERVER") do
  config :realtime_gateway, RealtimeGatewayWeb.Endpoint, server: true
end
