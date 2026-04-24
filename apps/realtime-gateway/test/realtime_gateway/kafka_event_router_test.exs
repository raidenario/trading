defmodule RealtimeGateway.KafkaEventRouterTest do
  use ExUnit.Case, async: true

  alias RealtimeGateway.KafkaEventRouter

  test "routes TickerUpdated envelope to market ticker_update broadcast" do
    message =
      Jason.encode!(%{
        "EventType" => "TickerUpdated",
        "Payload" => %{
          "Symbol" => "PETR4",
          "LastPrice" => 25.63,
          "BestBid" => 25.62,
          "BestAsk" => 25.64,
          "Volume24H" => 1200,
          "Change24H" => 0.42,
          "AsOf" => "2026-04-22T13:00:00Z"
        }
      })

    assert {:ok, [%{topic: "market:PETR4", event: "ticker_update", payload: payload}]} =
             KafkaEventRouter.to_broadcasts(message)

    assert payload.symbol == "PETR4"
    assert payload.last_price == 25.63
    assert payload.best_bid == 25.62
    assert payload.best_ask == 25.64
    assert payload.volume_24h == 1200
    assert payload.change_24h == 0.42
    assert payload.as_of == "2026-04-22T13:00:00Z"
  end

  test "routes BookUpdated envelope to market book_update broadcast" do
    message =
      Jason.encode!(%{
        "EventType" => "BookUpdated",
        "Payload" => %{
          "Symbol" => "BTC-USD",
          "Bids" => [
            %{"Price" => 50_000.0, "Quantity" => 0.5, "OrderCount" => 2}
          ],
          "Asks" => [
            %{"Price" => 50_010.0, "Quantity" => 0.25, "OrderCount" => 1}
          ],
          "AsOf" => "2026-04-22T13:00:01Z"
        }
      })

    assert {:ok, [%{topic: "market:BTC-USD", event: "book_update", payload: payload}]} =
             KafkaEventRouter.to_broadcasts(message)

    assert payload.symbol == "BTC-USD"
    assert payload.bids == [%{price: 50_000.0, quantity: 0.5, order_count: 2}]
    assert payload.asks == [%{price: 50_010.0, quantity: 0.25, order_count: 1}]
    assert payload.as_of == "2026-04-22T13:00:01Z"
  end

  test "routes TradeExecuted envelope to market trade_update broadcast" do
    message =
      Jason.encode!(%{
        "EventType" => "TradeExecuted",
        "Payload" => %{
          "TradeId" => "trade-1",
          "Symbol" => "VALE3",
          "Price" => 88.68,
          "Quantity" => 100,
          "BuyOrderId" => "buy-order",
          "SellOrderId" => "sell-order",
          "BuyAccountId" => "11111111-1111-1111-1111-111111111111",
          "SellAccountId" => "22222222-2222-2222-2222-222222222222",
          "ExecutedAt" => "2026-04-22T13:00:02Z"
        }
      })

    assert {:ok, [%{topic: "market:VALE3", event: "trade_update", payload: payload}]} =
             KafkaEventRouter.to_broadcasts(message)

    assert payload.symbol == "VALE3"
    assert payload.trade_id == "trade-1"
    assert payload.price == 88.68
    assert payload.quantity == 100
    assert payload.side == "Buy"
    assert payload.executed_at == "2026-04-22T13:00:02Z"
  end

  test "routes CandleUpdated envelope to market candle_update broadcast" do
    message =
      Jason.encode!(%{
        "EventType" => "CandleUpdated",
        "Payload" => %{
          "Symbol" => "PETR4",
          "Interval" => "1m",
          "Open" => 25.10,
          "High" => 25.80,
          "Low" => 24.90,
          "Close" => 25.50,
          "Volume" => 400,
          "OpenTime" => "2026-04-22T13:00:00Z",
          "CloseTime" => "2026-04-22T13:00:59Z"
        }
      })

    assert {:ok, [%{topic: "market:PETR4", event: "candle_update", payload: payload}]} =
             KafkaEventRouter.to_broadcasts(message)

    assert payload.symbol == "PETR4"
    assert payload.interval == "1m"
    assert payload.open == 25.10
    assert payload.high == 25.80
    assert payload.low == 24.90
    assert payload.close == 25.50
    assert payload.volume == 400
    assert payload.open_time == "2026-04-22T13:00:00Z"
    assert payload.close_time == "2026-04-22T13:00:59Z"
  end

  test "ignores unsupported event types" do
    message = Jason.encode!(%{"EventType" => "OrderAccepted", "Payload" => %{"Symbol" => "PETR4"}})

    assert :ignore = KafkaEventRouter.to_broadcasts(message)
  end

  test "returns error for invalid json" do
    assert {:error, _reason} = KafkaEventRouter.to_broadcasts("{")
  end
end
