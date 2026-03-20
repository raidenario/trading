defmodule RealtimeGatewayWeb.Endpoint do
  use Phoenix.Endpoint, otp_app: :realtime_gateway

  socket "/socket", RealtimeGatewayWeb.UserSocket,
    websocket: true,
    longpoll: false

  plug Plug.RequestId
  plug Plug.Telemetry, event_prefix: [:phoenix, :endpoint]

  plug Plug.Parsers,
    parsers: [:json],
    pass: ["*/*"],
    json_decoder: Phoenix.json_library()

  plug RealtimeGatewayWeb.Router
end
