FROM rust:1.85-bookworm AS build
WORKDIR /src

# Instala librdkafka dependencies inside the container
RUN apt-get update && apt-get install -y cmake build-essential libssl-dev pkg-config

COPY apps/matching-engine/Cargo.toml apps/matching-engine/Cargo.toml
COPY apps/matching-engine/src/ apps/matching-engine/src/

WORKDIR /src/apps/matching-engine
RUN cargo build --release

FROM debian:bookworm-slim AS runtime
WORKDIR /app
# Instala runtime dependencies (openssl é necessário para o rdkafka)
RUN apt-get update && apt-get install -y openssl ca-certificates && rm -rf /var/lib/apt/lists/*
COPY --from=build /src/apps/matching-engine/target/release/matching-engine-service /app/matching-engine-service
EXPOSE 7000
ENTRYPOINT ["./matching-engine-service"]
