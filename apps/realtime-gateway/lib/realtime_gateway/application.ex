defmodule RealtimeGateway.Application do
  use Application

  @impl true
  def start(_type, _args) do
    children = [
      {Phoenix.PubSub, name: RealtimeGateway.PubSub},
      RealtimeGatewayWeb.Endpoint
    ]

    opts = [strategy: :one_for_one, name: RealtimeGateway.Supervisor]
    Supervisor.start_link(children, opts)
  end

  @impl true
  def config_change(changed, _new, removed) do
    RealtimeGatewayWeb.Endpoint.config_change(changed, removed)
    :ok
  end
end
