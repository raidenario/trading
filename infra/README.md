# infra

Infraestrutura local mínima para desenvolvimento do monorepo.

## Conteúdo

- `compose/docker-compose.yml`: stack local
- `docker/*.Dockerfile`: imagens de cada serviço principal

## Observações

- `postgres` e `redis` estão presentes como placeholders opcionais
- o matching engine sobe como serviço HTTP mínimo em `:7000` apenas para health nesta fase
