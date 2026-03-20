# gateway-api

API HTTP de entrada para comandos de trading.

## Endpoints

- `GET /health`
- `POST /api/orders`
- `POST /api/orders/{orderId}/cancel`
- `GET /api/orders`
- `GET /api/orders/{orderId}`

## Organização

- `Exchange.Gateway.Api`: camada HTTP
- `Exchange.Trading.Application`: orquestração de comandos
- `Exchange.Trading.Domain`: entidades e value objects
- `Exchange.Trading.Infrastructure`: repositório, mensageria e matching client stub
