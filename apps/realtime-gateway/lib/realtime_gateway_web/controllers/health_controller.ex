defmodule RealtimeGatewayWeb.HealthController do
  use RealtimeGatewayWeb, :controller

  def show(conn, _params) do
    json(conn, %{
      service: "realtime-gateway",
      status: "ok",
      transport: "phoenix-channels"
    })
  end
end
