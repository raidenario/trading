@echo off
setlocal

cd /d "%~dp0"

echo =====================================================
echo Starting Exchange Platform via Docker Compose
echo =====================================================
echo.
echo Frontend:         http://localhost:3000
echo Gateway API:      http://localhost:5103
echo Query API:        http://localhost:5267
echo Ledger Service:   http://localhost:5075
echo Realtime Gateway: http://localhost:4000
echo Matching Engine:  http://localhost:7000
echo.

docker compose -f infra\compose\docker-compose.yml up --build %*
