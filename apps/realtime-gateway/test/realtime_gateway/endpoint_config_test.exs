defmodule RealtimeGateway.EndpointConfigTest do
  use ExUnit.Case, async: true

  test "endpoint is configured with an explicit HTTP listener" do
    config = Application.fetch_env!(:realtime_gateway, RealtimeGatewayWeb.Endpoint)
    http = Keyword.fetch!(config, :http)

    assert Keyword.fetch!(http, :ip) == {0, 0, 0, 0}
    assert is_integer(Keyword.fetch!(http, :port))
    assert Keyword.fetch!(http, :port) > 0
  end

  test "endpoint URL includes the configured port" do
    config = Application.fetch_env!(:realtime_gateway, RealtimeGatewayWeb.Endpoint)
    url = Keyword.fetch!(config, :url)

    assert Keyword.fetch!(url, :host) in ["localhost", "127.0.0.1"]
    assert is_integer(Keyword.fetch!(url, :port))
    assert Keyword.fetch!(url, :port) > 0
  end
end
