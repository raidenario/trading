defmodule RealtimeGatewayWeb.EventController do
  use RealtimeGatewayWeb, :controller

  alias RealtimeGateway.EventIngestor

  require Logger

  def create(conn, params) do
    Logger.info("Realtime HTTP ingest received #{inspect(EventIngestor.describe(params))}")

    case EventIngestor.ingest(params) do
      {:ok, count} ->
        json(conn, %{accepted: true, broadcasts: count})

      {:error, reason} ->
        conn
        |> put_status(:bad_request)
        |> json(%{accepted: false, reason: inspect(reason)})
    end
  end
end
