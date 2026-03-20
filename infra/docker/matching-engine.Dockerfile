FROM rust:1.77 AS build
WORKDIR /src

COPY apps/matching-engine/Cargo.toml apps/matching-engine/Cargo.toml
COPY apps/matching-engine/src/ apps/matching-engine/src/

WORKDIR /src/apps/matching-engine
RUN cargo build --release

FROM debian:bookworm-slim AS runtime
WORKDIR /app
COPY --from=build /src/apps/matching-engine/target/release/matching-engine-service /app/matching-engine-service
EXPOSE 7000
ENTRYPOINT ["./matching-engine-service"]
