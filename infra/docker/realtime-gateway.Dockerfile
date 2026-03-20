FROM elixir:1.19-alpine AS build
WORKDIR /app

RUN apk add --no-cache build-base git

RUN mix local.hex --force && mix local.rebar --force

COPY apps/realtime-gateway/mix.exs apps/realtime-gateway/mix.lock ./
RUN mix deps.get --only dev

COPY apps/realtime-gateway/config config
COPY apps/realtime-gateway/lib lib

RUN mix compile

EXPOSE 4000
CMD ["mix", "phx.server"]
