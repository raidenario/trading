@echo off
echo =====================================================
echo Iniciando a Exchange Platform via Docker Compose
echo =====================================================
echo.
echo Isso ira construir as imagens do C#, Rust e Elixir e subi-las juntas.
echo.

docker compose -f infra/compose/docker-compose.yml up --build

echo.
pause
