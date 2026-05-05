---
status: "accepted"
date: "2026-05-05"
decision-makers:
  - "Tech Lead (CTIC)"
---

# ADR-0047: `Confluent.Kafka 2.14.0` + `Npgsql 9.0.4` como pisos transitivos do Wolverine 5.32.1

## Contexto e enunciado do problema

Após o de-vendor do Wolverine (PR #168) e a adoção do `WolverineFx.Postgresql` + `WolverineFx.Kafka` 5.32.1 oficiais (PR #172), o `Directory.Packages.props` passou a depender transitivamente de:

- `Confluent.Kafka 2.14.0` (provém de `WolverineFx.Kafka`)
- `Npgsql 9.0.4` (provém de `WolverineFx.Postgresql` + `Microsoft.EntityFrameworkCore`)

Esses pacotes não foram escolhidos independentemente — vieram em pacote arquitetural com a decisão de usar Wolverine. Sem ADR documentando isso, futuros bumps de Confluent.Kafka ou Npgsql parecem opção independente, quando na verdade são dependência arquitetural da escolha Wolverine.

## Drivers da decisão

- **Auditoria**: bumps de pacotes de infra precisam ser rastreáveis a uma decisão arquitetural, não à conveniência local.
- **Compat**: bump unilateral de `Confluent.Kafka` ou `Npgsql` pode quebrar compatibilidade com a versão Wolverine — só Wolverine atesta.
- **Documentar restrição**: a próxima vez que alguém ver `<PackageVersion Include="Confluent.Kafka" Version="2.14.0" />` no `Directory.Packages.props`, ADR aponta porque essa versão.

## Opções consideradas

- **A. Documentar pisos transitivos como ADR.**
- **B. Comentário inline em `Directory.Packages.props`.**
- **C. Não documentar — rastrear caso a caso.**

## Resultado da decisão

**Escolhida:** "A — ADR documentando pisos transitivos como dependência arquitetural".

Esta ADR registra que:

1. `Confluent.Kafka 2.14.0` e `Npgsql 9.0.4` são **pisos transitivos** do Wolverine 5.32.1.
2. Bumps unilaterais de qualquer um desses pacotes **devem** vir acompanhados de bump do Wolverine ou de validação explícita de compat.
3. O matrix de teste S0-S10 da spike #158 valida o conjunto na versão atual; bump no `WolverineFx.Postgresql` ou `WolverineFx.Kafka` deve regerar o matrix.

A opção B (comentário inline em `Directory.Packages.props`) é compatível com A — pode ser feita em PR adicional pequeno, citando esta ADR.

## Consequências

### Positivas

- Bumps futuros de `Confluent.Kafka` ou `Npgsql` ficam visíveis como decisão arquitetural; review consulta este ADR.
- Aderência ao matrix S0-S10 — não há "pegou e funcionou", há "validado na config X".

### Negativas

- ADR fica em risco de obsolescência — quando Wolverine bumpar, esta ADR precisa de atualização (ou de uma sucessora). Mitigação: nota no rodapé do ADR registra a versão Wolverine atestada e a data.

### Neutras

- A decisão pode ser ampliada quando novos pisos transitivos surgirem (ex.: `MongoDB.Driver` se Wolverine adicionar transport Mongo). Hoje cobre apenas Confluent.Kafka + Npgsql.

## Confirmação

- `Directory.Packages.props` em `repositories/uniplus-api/` declara as versões.
- `package.lock.json` em cada projeto fixa a versão resolvida.
- Spike S10 (`docs/spikes/158-relatorio-final.md`) registrou os matrix tests para a config atual.

## Prós e contras das opções

### A — ADR (escolhida)

- Bom: rastreabilidade arquitetural; review consulta documento canônico.
- Ruim: ADR pode ficar obsoleto se Wolverine bumpar e ninguém atualizar.

### B — Comentário inline em `Directory.Packages.props`

- Bom: mais próximo do código.
- Ruim: comentário em props file é raramente lido em review de bumps.

### C — Não documentar

- Bom: zero esforço.
- Ruim: bump unilateral é provável; debug de incompat é caro.

## Mais informações

- [JasperFx Wolverine — Postgresql backend](https://wolverinefx.io/guide/durability/postgresql.html)
- [JasperFx Wolverine — Kafka transport](https://wolverinefx.io/guide/messaging/transports/kafka.html)
- ADR-0026 — Outbox transacional via Wolverine
- ADR-0040 — Helper `UseWolverineOutboxCascading`
- Origem: PR [#168](https://github.com/unifesspa-edu-br/uniplus-api/pull/168) (de-vendor); PR [#172](https://github.com/unifesspa-edu-br/uniplus-api/pull/172) (5.32.1 oficial); issue [#189](https://github.com/unifesspa-edu-br/uniplus-api/issues/189)
- **Versão Wolverine atestada nesta ADR:** `WolverineFx.Postgresql 5.32.1` + `WolverineFx.Kafka 5.32.1` (2026-05-05).
