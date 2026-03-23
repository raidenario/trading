@echo off
echo =====================================================
echo Iniciando serviços da Exchange Platform localmente
echo =====================================================
echo.
echo Isso ira abrir uma nova janela preta (CMD) para cada serviço separadamente.
echo.

echo [C#] Restaurando pacotes NuGet da Solucao...
dotnet restore ExchangePlatform.slnx

echo.
echo Iniciando Gateway API (C#)...
start "Gateway API (C#)" cmd /k "dotnet run --project apps\gateway-api\src\Exchange.Gateway.Api"

echo Iniciando Query API (C#)...
start "Query API (C#)" cmd /k "dotnet run --project apps\query-api\src\Exchange.Query.Api"

echo Iniciando Ledger Service (C#)...
start "Ledger Service (C#)" cmd /k "dotnet run --project apps\ledger-service\src\Exchange.Ledger.Api"

echo Iniciando Matching Engine (Rust)...
start "Matching Engine (Rust)" cmd /k "cd apps\matching-engine && cargo run --bin matching-engine-service"

echo Iniciando Realtime Gateway (Elixir)...
start "Realtime Gateway (Elixir)" cmd /k "cd apps\realtime-gateway && mix deps.get && mix phx.server"

echo.
echo =====================================================
echo Todos os serviços estao iniciando em novas janelas!1
echo Feche as janelas individuais para parar cada servico.
echo =====================================================
pause
