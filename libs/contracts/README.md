# contracts

Contratos compartilhados entre serviços e linguagens.

## Conteúdo

- `dotnet/Exchange.Platform.Contracts`: records e enums usados pelos serviços .NET
- `schemas/v1`: schemas JSON versionados para integração futura entre C#, Rust, Elixir e Python
- taxonomias B3-inspired para instrumentos, participantes, contas de negociação, allocations e placeholders de clearing/settlement

## Estratégia

- nomes estáveis
- payloads pequenos e explícitos
- `SchemaVersion` no nível da mensagem
- versionamento por pasta para evitar breaking changes silenciosas
- campos futuros entram opcionais e documentados para manter compatibilidade com payloads legados
