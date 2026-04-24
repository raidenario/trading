defmodule RealtimeGateway.MarketEventRouter do
  @moduledoc false

  alias RealtimeGatewayWeb.Endpoint

  require Logger

  def broadcast_ticker(symbol, payload) do
    Endpoint.broadcast(topic(symbol), "ticker_update", payload)
  end

  def broadcast_trade(symbol, payload) do
    Endpoint.broadcast(topic(symbol), "trade_update", payload)
  end

  def broadcast_book(symbol, payload) do
    Endpoint.broadcast(topic(symbol), "book_update", payload)
  end

  def broadcast_candle(symbol, payload) do
    Endpoint.broadcast(topic(symbol), "candle_update", payload)
  end

  def broadcast(%{topic: topic, event: event, payload: payload}) do
    Logger.info("Realtime broadcast topic=#{topic} event=#{event} payload=#{inspect(payload)}")
    Endpoint.broadcast(topic, event, payload)
  end

  def topic(symbol), do: "market:" <> String.upcase(symbol)
end
