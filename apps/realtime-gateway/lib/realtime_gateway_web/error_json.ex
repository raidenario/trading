defmodule RealtimeGatewayWeb.ErrorJSON do
  def render(template, _assigns) do
    %{error: template}
  end
end
