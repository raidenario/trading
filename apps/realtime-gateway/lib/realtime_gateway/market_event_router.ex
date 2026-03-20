defmodule RealtimeGateway.MarketEventRouter do
  @moduledoc false

  alias RealtimeGatewayWeb.Endpoint

  def broadcast_ticker(symbol, payload) do
    Endpoint.broadcast(topic(symbol), "ticker_update", payload)
  end

  def broadcast_trade(symbol, payload) do
    Endpoint.broadcast(topic(symbol), "trade_update", payload)
  end

  def topic(symbol), do: "market:" <> String.upcase(symbol)
end
