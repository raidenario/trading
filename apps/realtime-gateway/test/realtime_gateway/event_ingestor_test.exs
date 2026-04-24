defmodule RealtimeGateway.EventIngestorTest do
  use ExUnit.Case, async: true

  alias RealtimeGateway.EventIngestor

  defmodule TestBroadcaster do
    def broadcast(broadcast) do
      send(self(), {:broadcast, broadcast})
      :ok
    end
  end

  test "ingest broadcasts every route produced from an exchange envelope" do
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

    assert {:ok, 1} = EventIngestor.ingest(message, TestBroadcaster)

    assert_receive {:broadcast,
                    %{
                      topic: "market:PETR4",
                      event: "ticker_update",
                      payload: %{symbol: "PETR4", last_price: 25.63}
                    }}
  end

  test "ingest ignores unsupported event types" do
    message = Jason.encode!(%{"EventType" => "OrderAccepted", "Payload" => %{"Symbol" => "PETR4"}})

    assert {:ok, 0} = EventIngestor.ingest(message, TestBroadcaster)
    refute_receive {:broadcast, _}
  end

  test "ingest accepts decoded maps from Phoenix controllers" do
    message = %{
      "EventType" => "BookUpdated",
      "Payload" => %{
        "Symbol" => "BTC-USD",
        "Bids" => [%{"Price" => 50_000.0, "Quantity" => 0.5, "OrderCount" => 2}],
        "Asks" => [%{"Price" => 50_010.0, "Quantity" => 0.25, "OrderCount" => 1}],
        "AsOf" => "2026-04-22T13:00:01Z"
      }
    }

    assert {:ok, 1} = EventIngestor.ingest(message, TestBroadcaster)

    assert_receive {:broadcast,
                    %{
                      topic: "market:BTC-USD",
                      event: "book_update",
                      payload: %{symbol: "BTC-USD"}
                    }}
  end

  test "ingest returns error for malformed json" do
    assert {:error, _reason} = EventIngestor.ingest("{", TestBroadcaster)
    refute_receive {:broadcast, _}
  end

  test "describe returns event metadata for logs" do
    message = %{
      "EventType" => "TradeExecuted",
      "Payload" => %{
        "Symbol" => "PETR4",
        "TradeId" => "trade-1",
        "Price" => 25.63,
        "Quantity" => 100
      }
    }

    assert EventIngestor.describe(message) == %{
             event_type: "TradeExecuted",
             symbol: "PETR4",
             trade_id: "trade-1",
             price: 25.63,
             quantity: 100
           }
  end
end
