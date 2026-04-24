defmodule RealtimeGatewayWeb.Router do
  use RealtimeGatewayWeb, :router

  pipeline :api do
    plug :accepts, ["json"]
  end

  scope "/", RealtimeGatewayWeb do
    pipe_through :api

    get "/health", HealthController, :show
    post "/internal/events", EventController, :create
  end
end
