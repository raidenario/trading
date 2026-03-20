# matching-engine

Crate Rust para o núcleo de matching e manutenção do order book em memória.

## O que existe nesta base

- `src/domain`: tipos principais de domínio para ordens e trades
- `src/price_level.rs`: fila FIFO por nível de preço
- `src/order_book.rs`: book com bids/asks e regra de preço-tempo
- `src/matching_engine.rs`: coordenação multi-símbolo com testes unitários
- `src/main.rs`: binário mínimo com endpoint `GET /health` em `:7000`

## Premissas iniciais

- preço e quantidade usam inteiros (`u64`) para evitar ponto flutuante
- `price` representa ticks
- `quantity` representa lots/unidades mínimas
- apenas o book e o matching ficam aqui; risco, ledger e realtime permanecem fora do hot path

## Rodando

```bash
cargo test
cargo run --bin matching-engine-service
```

## Evolução natural

- expor API gRPC ou NATS/Kafka consumer para comandos
- suportar cancelamento por lookup indexado
- adicionar market data derivada (best bid/ask, last trade, depth)
- persistir snapshots assíncronos sem contaminar o caminho crítico
