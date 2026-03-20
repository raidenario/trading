# contracts

Contratos compartilhados entre serviços e linguagens.

## Conteúdo

- `dotnet/Exchange.Platform.Contracts`: records e enums usados pelos serviços .NET
- `schemas/v1`: schemas JSON versionados para integração futura entre C#, Rust, Elixir e Python

## Estratégia

- nomes estáveis
- payloads pequenos e explícitos
- `SchemaVersion` no nível da mensagem
- versionamento por pasta para evitar breaking changes silenciosas
