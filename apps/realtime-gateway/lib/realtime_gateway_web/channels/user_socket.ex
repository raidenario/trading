defmodule RealtimeGatewayWeb.UserSocket do
  use Phoenix.Socket

  channel "market:*", RealtimeGatewayWeb.MarketChannel

  @impl true
  def connect(_params, socket, _connect_info), do: {:ok, socket}

  @impl true
  def id(_socket), do: nil
end
