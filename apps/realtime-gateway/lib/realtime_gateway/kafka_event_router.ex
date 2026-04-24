defmodule RealtimeGateway.KafkaEventRouter do
  @moduledoc """
  Converts Kafka integration-event envelopes into Phoenix channel broadcasts.

  The exchange services publish envelopes with PascalCase keys. The frontend
  channel contract stays snake_case and symbol-scoped.
  """

  @type broadcast :: %{
          topic: String.t(),
          event: String.t(),
          payload: map()
        }

  @spec to_broadcasts(String.t()) :: {:ok, [broadcast()]} | :ignore | {:error, term()}
  def to_broadcasts(message) when is_binary(message) do
    with {:ok, envelope} <- Jason.decode(message) do
      envelope
      |> normalize_envelope()
      |> broadcasts_for()
    end
  end

  defp normalize_envelope(envelope) do
    %{
      event_type: get_value(envelope, "EventType", "eventType"),
      payload: get_value(envelope, "Payload", "payload") || %{}
    }
  end

  defp broadcasts_for(%{event_type: "TickerUpdated", payload: payload}) do
    symbol = normalize_symbol(get_value(payload, "Symbol", "symbol"))

    {:ok,
     [
       %{
         topic: topic(symbol),
         event: "ticker_update",
         payload: %{
           symbol: symbol,
           last_price: get_value(payload, "LastPrice", "lastPrice"),
           best_bid: get_value(payload, "BestBid", "bestBid"),
           best_ask: get_value(payload, "BestAsk", "bestAsk"),
           volume_24h: get_value(payload, "Volume24H", "volume24H"),
           change_24h: get_value(payload, "Change24H", "change24H"),
           as_of: get_value(payload, "AsOf", "asOf")
         }
       }
     ]}
  end

  defp broadcasts_for(%{event_type: "BookUpdated", payload: payload}) do
    symbol = normalize_symbol(get_value(payload, "Symbol", "symbol"))

    {:ok,
     [
       %{
         topic: topic(symbol),
         event: "book_update",
         payload: %{
           symbol: symbol,
           bids: normalize_levels(get_value(payload, "Bids", "bids")),
           asks: normalize_levels(get_value(payload, "Asks", "asks")),
           as_of: get_value(payload, "AsOf", "asOf")
         }
       }
     ]}
  end

  defp broadcasts_for(%{event_type: "TradeExecuted", payload: payload}) do
    symbol = normalize_symbol(get_value(payload, "Symbol", "symbol"))

    {:ok,
     [
       %{
         topic: topic(symbol),
         event: "trade_update",
         payload: %{
           symbol: symbol,
           trade_id: get_value(payload, "TradeId", "tradeId"),
           price: get_value(payload, "Price", "price"),
           quantity: get_value(payload, "Quantity", "quantity"),
           side: "Buy",
           buy_order_id: get_value(payload, "BuyOrderId", "buyOrderId"),
           sell_order_id: get_value(payload, "SellOrderId", "sellOrderId"),
           buy_account_id: get_value(payload, "BuyAccountId", "buyAccountId"),
           sell_account_id: get_value(payload, "SellAccountId", "sellAccountId"),
           executed_at: get_value(payload, "ExecutedAt", "executedAt")
         }
       }
     ]}
  end

  defp broadcasts_for(%{event_type: "CandleUpdated", payload: payload}) do
    symbol = normalize_symbol(get_value(payload, "Symbol", "symbol"))

    {:ok,
     [
       %{
         topic: topic(symbol),
         event: "candle_update",
         payload: %{
           symbol: symbol,
           interval: get_value(payload, "Interval", "interval"),
           open: get_value(payload, "Open", "open"),
           high: get_value(payload, "High", "high"),
           low: get_value(payload, "Low", "low"),
           close: get_value(payload, "Close", "close"),
           volume: get_value(payload, "Volume", "volume"),
           open_time: get_value(payload, "OpenTime", "openTime"),
           close_time: get_value(payload, "CloseTime", "closeTime")
         }
       }
     ]}
  end

  defp broadcasts_for(_envelope), do: :ignore

  defp normalize_levels(levels) when is_list(levels) do
    Enum.map(levels, fn level ->
      %{
        price: get_value(level, "Price", "price"),
        quantity: get_value(level, "Quantity", "quantity"),
        order_count: get_value(level, "OrderCount", "orderCount")
      }
    end)
  end

  defp normalize_levels(_levels), do: []

  defp get_value(map, primary, fallback) when is_map(map) do
    Map.get(map, primary) || Map.get(map, fallback)
  end

  defp get_value(_map, _primary, _fallback), do: nil

  defp normalize_symbol(symbol) when is_binary(symbol), do: String.upcase(symbol)
  defp normalize_symbol(_symbol), do: "UNKNOWN"

  defp topic(symbol), do: "market:" <> symbol
end
