@echo off
echo =====================================================
echo Subindo apenas a INFRAESTRUTURA (Docker)
echo =====================================================
echo.

docker compose -f infra/compose/docker-compose.yml up postgres redis zookeeper kafka kafka-init -d

echo.
echo =====================================================
echo Infraestrutura pronta! 
echo Agora voce pode rodar o .\start-local.bat em outra janela.
echo =====================================================
pause
