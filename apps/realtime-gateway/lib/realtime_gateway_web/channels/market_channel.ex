defmodule RealtimeGatewayWeb.MarketChannel do
  use RealtimeGatewayWeb, :channel

  alias RealtimeGateway.MarketEventRouter

  @impl true
  def join("market:" <> symbol, _payload, socket) do
    response = %{
      subscribed: true,
      symbol: String.upcase(symbol),
      channels: ["ticker_update", "trade_update"]
    }

    {:ok, response, assign(socket, :symbol, String.upcase(symbol))}
  end

  @impl true
  def handle_in("demo:ticker", payload, socket) do
    event = %{
      symbol: socket.assigns.symbol,
      last_price: Map.get(payload, "last_price", 50_010.0),
      best_bid: Map.get(payload, "best_bid", 50_000.0),
      best_ask: Map.get(payload, "best_ask", 50_020.0),
      volume_24h: Map.get(payload, "volume_24h", 235.42),
      as_of: DateTime.utc_now()
    }

    :ok = MarketEventRouter.broadcast_ticker(socket.assigns.symbol, event)
    {:reply, :ok, event, socket}
  end

  @impl true
  def handle_in("demo:trade", payload, socket) do
    event = %{
      symbol: socket.assigns.symbol,
      trade_id: Map.get(payload, "trade_id", "sim-trade-1"),
      price: Map.get(payload, "price", 50_010.0),
      quantity: Map.get(payload, "quantity", 0.25),
      side: Map.get(payload, "side", "buy"),
      executed_at: DateTime.utc_now()
    }

    :ok = MarketEventRouter.broadcast_trade(socket.assigns.symbol, event)
    {:reply, :ok, event, socket}
  end
end
