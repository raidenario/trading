@echo off
echo =====================================================
echo Iniciando servicos da Exchange Platform (Windows Terminal)
echo =====================================================
echo.

echo [C#] Restaurando pacotes NuGet da Solucao...
dotnet restore ExchangePlatform.slnx

echo.
echo Abrindo Windows Terminal com 6 abas...

wt -d "%CD%" --title "Gateway API" cmd /k "dotnet run --project apps\gateway-api\src\Exchange.Gateway.Api" ; ^
new-tab -d "%CD%" --title "Query API" cmd /k "dotnet run --project apps\query-api\src\Exchange.Query.Api" ; ^
new-tab -d "%CD%" --title "Ledger Service" cmd /k "dotnet run --project apps\ledger-service\src\Exchange.Ledger.Api" ; ^
new-tab -d "%CD%\apps\matching-engine" --title "Matching Engine" cmd /k "cargo run --bin matching-engine-service" ; ^
new-tab -d "%CD%\apps\realtime-gateway" --title "Realtime Gateway" cmd /k "mix deps.get && mix phx.server" ; ^
new-tab -d "%CD%\apps\frontend" --title "Frontend" cmd /k "npm install && npm run dev"

echo.
echo =====================================================
echo Tudo pronto! O Frontend abrira em http://localhost:3000
echo =====================================================
pause
