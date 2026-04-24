# quant-lab

Espaço reservado para o laboratório quantitativo do projeto.

## Estado Atual

Hoje este app ainda não contém implementação executável. A estrutura existente é apenas:

```text
src/
  quant_lab/
    pricing/
    risk/
```

Ou seja: o repositório já separou a responsabilidade do laboratório quant, mas o serviço ainda não foi ligado à stack principal.

## Papel Planejado

O `quant-lab` deve receber, nas próximas iterações:

- métricas de estratégia;
- séries de retorno;
- PnL realizado e não realizado;
- drawdown;
- turnover;
- exposição por ativo e por classe;
- risco e cenários;
- integração com replay histórico do `real-market-simulator`.

## Integração Esperada

A direção técnica mais coerente para este app é consumir dados do ecossistema já existente, sem furar a arquitetura:

```text
tooling/query-api/ledger -> quant-lab -> analytics/backtests/reports
```

Fontes naturais de entrada:

- candles históricas do `query-api`;
- trades e fills projetados;
- posições e balances do `ledger-service`;
- datasets offline preparados pelo `tooling`.

## Observação

Este README existe para deixar claro que o `quant-lab` faz parte do desenho do projeto, mas ainda não deve ser tratado como componente pronto.
