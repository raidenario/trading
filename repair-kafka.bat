@echo off
setlocal EnableExtensions EnableDelayedExpansion

set "COMPOSE_FILE=infra/compose/docker-compose.yml"
set "BOOTSTRAP_SERVER=kafka:9092"
set "KAFKA_SERVICE=kafka"
set "ZOOKEEPER_SERVICE=zookeeper"
set "INIT_SERVICE=kafka-init"
set "TOPICS=order-commands:3 matching-events:3 ledger-events:3 marketdata-events:3 account-events:2"
set /a MAX_RETRIES=30

echo =====================================================
echo Reparando Kafka e recriando topicos da Exchange Platform
echo =====================================================
echo.

echo [1/5] Parando e removendo Kafka, Zookeeper e bootstrap antigo...
docker compose -f "%COMPOSE_FILE%" stop %KAFKA_SERVICE% %INIT_SERVICE% %ZOOKEEPER_SERVICE% >nul 2>&1
docker compose -f "%COMPOSE_FILE%" rm -f -s %KAFKA_SERVICE% %INIT_SERVICE% %ZOOKEEPER_SERVICE% >nul 2>&1

echo [2/5] Removendo volumes do Kafka/Zookeeper...
docker volume rm projtrading_kafka-data projtrading_zookeeper-data projtrading_zookeeper-log >nul 2>&1

echo [3/5] Subindo Zookeeper e Kafka limpos...
docker compose -f "%COMPOSE_FILE%" up -d %ZOOKEEPER_SERVICE% %KAFKA_SERVICE%
if errorlevel 1 (
  echo Falha ao subir Kafka/Zookeeper.
  exit /b 1
)

echo [4/5] Aguardando Kafka responder no bootstrap...
call :wait_for_kafka
if errorlevel 1 (
  exit /b 1
)

echo [5/5] Criando topicos obrigatorios...
for %%T in (%TOPICS%) do (
  for /F "tokens=1,2 delims=:" %%A in ("%%T") do (
    echo   criando %%A com %%B particoes...
    docker compose -f "%COMPOSE_FILE%" exec -T %KAFKA_SERVICE% bash -lc "kafka-topics --create --if-not-exists --bootstrap-server %BOOTSTRAP_SERVER% --topic %%A --partitions %%B --replication-factor 1"
    if errorlevel 1 (
      echo Falha ao criar topico %%A.
      exit /b 1
    )
  )
)

echo.
echo Topicos finais no broker:
docker compose -f "%COMPOSE_FILE%" exec -T %KAFKA_SERVICE% bash -lc "kafka-topics --list --bootstrap-server %BOOTSTRAP_SERVER%"
if errorlevel 1 (
  echo Falha ao listar topicos no broker.
  exit /b 1
)

echo.
echo =====================================================
echo Kafka reparado e topicos garantidos com sucesso.
echo =====================================================
exit /b 0

:wait_for_kafka
for /L %%I in (1,1,%MAX_RETRIES%) do (
  docker compose -f "%COMPOSE_FILE%" exec -T %KAFKA_SERVICE% bash -lc "kafka-broker-api-versions --bootstrap-server %BOOTSTRAP_SERVER% > /dev/null 2>&1" >nul 2>&1
  if !errorlevel! equ 0 (
    exit /b 0
  )

  echo   tentativa %%I/%MAX_RETRIES% ...
  timeout /t 2 /nobreak >nul
)

echo Kafka nao ficou pronto a tempo.
echo Verifique: docker compose -f "%COMPOSE_FILE%" logs kafka
exit /b 1
