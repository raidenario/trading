defmodule RealtimeGateway.EventIngestor do
  @moduledoc """
  Accepts exchange integration-event envelopes and fans them out to channels.

  Kafka consumption stays outside this Phoenix app in local Windows setups to
  avoid native NIF dependencies. Any service that already consumes Kafka can
  POST the raw envelope here.
  """

  alias RealtimeGateway.KafkaEventRouter
  alias RealtimeGateway.MarketEventRouter

  require Logger

  def ingest(message, broadcaster \\ MarketEventRouter)

  def ingest(message, broadcaster) when is_binary(message) do
    metadata = describe(message)

    case KafkaEventRouter.to_broadcasts(message) do
      {:ok, broadcasts} ->
        Logger.info(
          "Realtime ingest event_type=#{metadata.event_type} symbol=#{metadata.symbol} broadcasts=#{length(broadcasts)} details=#{inspect(metadata)}"
        )

        Enum.each(broadcasts, &broadcaster.broadcast/1)
        {:ok, length(broadcasts)}

      :ignore ->
        Logger.debug("Realtime ignored event details=#{inspect(metadata)}")
        {:ok, 0}

      {:error, reason} ->
        Logger.warning("Realtime rejected malformed event reason=#{inspect(reason)}")
        {:error, reason}
    end
  end

  def ingest(message, broadcaster) when is_map(message) do
    message
    |> Jason.encode()
    |> case do
      {:ok, json} -> ingest(json, broadcaster)
      {:error, reason} -> {:error, reason}
    end
  end

  def describe(message) when is_binary(message) do
    case Jason.decode(message) do
      {:ok, decoded} -> describe(decoded)
      {:error, _reason} -> %{event_type: nil, symbol: nil}
    end
  end

  def describe(message) when is_map(message) do
    payload = get_value(message, "Payload", "payload") || %{}

    %{
      event_type: get_value(message, "EventType", "eventType"),
      symbol: get_value(payload, "Symbol", "symbol"),
      trade_id: get_value(payload, "TradeId", "tradeId"),
      price: get_value(payload, "Price", "price") || get_value(payload, "LastPrice", "lastPrice"),
      quantity: get_value(payload, "Quantity", "quantity")
    }
    |> Enum.reject(fn {_key, value} -> is_nil(value) end)
    |> Map.new()
  end

  defp get_value(map, primary, fallback) when is_map(map) do
    Map.get(map, primary) || Map.get(map, fallback)
  end

  defp get_value(_map, _primary, _fallback), do: nil
end
