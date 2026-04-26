# Relatório do Spike S8 — Outbox Wolverine (#158)

- **Branch:** `spikes/158-s8-migration-surface`
- **Branch base:** `spikes/158-s7-retry-strategy` (`13531eb`)
- **Data:** 2026-04-25
- **Status:** **Superfície de schema mapeada**. AC4 do plano requer ainda **decisão de versionamento** (migration EF, DbContext dedicado ou SQL versionado). 10 testes verdes.
- **Plano de referência:** [`docs/spikes/158-plano-validacao-outbox-wolverine.md`](158-plano-validacao-outbox-wolverine.md)

## Resumo executivo

`AutoBuildMessageStorageOnStartup = CreateOrUpdate` (default) cria **10 tabelas
em 2 schemas** quando o spike completa um ciclo de publish/consume:

```
wolverine.wolverine_agent_restrictions
wolverine.wolverine_control_queue
wolverine.wolverine_dead_letters
wolverine.wolverine_incoming_envelopes
wolverine.wolverine_node_assignments
wolverine.wolverine_node_records
wolverine.wolverine_nodes
wolverine.wolverine_outgoing_envelopes
wolverine_queues.wolverine_queue_domain_events
wolverine_queues.wolverine_queue_domain_events_scheduled
```

Comparação com a lista do plano §S8:

| Tabela esperada | Observada | Schema |
|---|---|---|
| `wolverine_outgoing_envelopes` | ✅ | `wolverine` |
| `wolverine_incoming_envelopes` | ✅ | `wolverine` |
| `wolverine_dead_letters` | ✅ | `wolverine` |
| `wolverine_nodes` | ✅ | `wolverine` |
| `wolverine_node_assignments` | ✅ | `wolverine` |
| `wolverine_node_records` | ✅ | `wolverine` |
| `wolverine_control_queue` | ✅ | `wolverine` |
| `wolverine_agent_restrictions` | ✅ | `wolverine` |
| Tabelas adicionais de PG transport | `wolverine_queue_domain_events`, `wolverine_queue_domain_events_scheduled` | `wolverine_queues` |

Plano §S8 estava **completo** — a única lacuna eram os nomes exatos das tabelas
do PG transport, agora confirmadas. Cada queue PG nomeada (`domain-events` no
spike, vira `domain_events` em snake_case) gera **duas tabelas**: a queue
principal e a versão "scheduled" (mensagens agendadas).

## Achados desta fase

### G1 — `AutoBuildMessageStorageOnStartup` é `CreateOrUpdate` por default

Comportamento padrão idempotente: idempotência confirmada em runs sequenciais
do test (segundo run não tenta `CREATE TABLE` em tabela existente). É
**aceitável para dev/test**, conforme [§Política dev/test vs produção do plano](158-plano-validacao-outbox-wolverine.md#política-devtest-vs-produção).

**Em produção** o plano explicita: o schema deve ser auditável e versionado.
A decisão fica entre as três opções abaixo.

### G2 — Opções de versionamento

| Opção | Vantagens | Desvantagens | Esforço |
|---|---|---|---|
| **A — Migration EF do `SelecaoDbContext` via `MapWolverineEnvelopeStorage(modelBuilder, "wolverine")`** | Único pipeline de migration por módulo; dependências do EF já estão configuradas; `dotnet ef migrations add ...` gera o script | Mistura tabelas Wolverine no DbContext de domínio; refatorações futuras podem afetar; `MapWolverineEnvelopeStorage` cobre só o schema persistence — o transport PG (`wolverine_queues`) **não** entra no model do EF | Médio |
| **B — DbContext dedicado (`WolverineSchemaDbContext`)** | Isolamento; migrations Wolverine separadas das de domínio; possível compartilhar entre módulos (Selecao + Ingresso) | Dois pipelines de migration por módulo; coordenação de aplicação na ordem certa | Médio-alto |
| **C — SQL versionado fora do EF** (Flyway, Liquibase, ou scripts numerados) | Auditável; idêntico ao que o `AutoBuild` gera; pode ser dumpado direto do schema observado | Não usa EF; dois fluxos de migration coexistindo | Alto (introduz nova tooling) |

### G3 — Recomendação técnica

**Opção A (migration EF), com ressalva de schema separado**:

1. `SelecaoDbContext` mapeia `MapWolverineEnvelopeStorage(modelBuilder, "wolverine")`
   no `OnModelCreating`. Tabelas Wolverine ficam no schema `wolverine`, sem
   colidir com tabelas de domínio (que ficam em `public`).
2. Para o PG transport (`wolverine_queues.*`), aceitar que o `AutoProvision`
   do Wolverine cuida dele em runtime — alternativa: SQL versionado
   complementar para esse schema secundário (caso governança exija auditoria).
3. **Desligar** `AutoBuildMessageStorageOnStartup` em produção
   (`opts.AutoBuildMessageStorageOnStartup = AutoCreate.None`) — schema só é
   alterado por migration explícita.

Confirmação dessa decisão depende de uma sessão dedicada com revisão de
arquitetura — fica como entrada para o ADR final do outbox (#158).

### G4 — `wolverine_queue_<nome>` e `wolverine_queue_<nome>_scheduled`

Convenção de nomenclatura observada do PG transport:

- `wolverine_queue_domain_events` — fila principal.
- `wolverine_queue_domain_events_scheduled` — mensagens agendadas para envio futuro.

Para múltiplas queues (cada `ToPostgresqlQueue(name)`), Wolverine cria 2
tabelas por nome. Considerar isso ao dimensionar — cada par de tabelas tem
índice próprio.

## Resultados

```bash
dotnet test tests/Unifesspa.UniPlus.Selecao.IntegrationTests --filter "Category=OutboxCapability"
```

```
Aprovado S2/V4 — PG transport entrega EditalPublicadoEvent ao handler local
Aprovado S4/V4 — rollback PG: entidade ausente, envelope ausente
Aprovado S3/V5 — Kafka transport publica EditalPublicadoEvent no tópico
Aprovado S4/V5 — rollback Kafka: tópico não recebe mensagem fantasma
Aprovado S5/V6 (parte 1) — Kafka offline: envelope pendente em storage
Aprovado S6/V7 — restart: novo host processa mensagem pendente
Aprovado S7 (variante A) — EF retry ON levanta conflito esperado
Aprovado S7 (variante B) — EF retry OFF é a recomendação aplicada
Aprovado S8 — schema 'wolverine' contém todas as tabelas esperadas
Aprovado S8 — superfície completa observada documentada no test output

Total de testes: 10
     Aprovados: 10
```

## Decisões pendentes da #158 (atualização)

| Caminho | Decisão |
|---|---|
| Caminho 1 — upgrade/fix Wolverine | **Resolvido** (5.32.1-pr2586) |
| Caminho 2 — migration das tabelas Wolverine | **Recomendação A** (migration EF do schema `wolverine` no `SelecaoDbContext`) — requer ADR para fechar |
| Caminho 3 — retry EF vs retry Wolverine | **Resolvido** (variante B) |
| Transporte principal | **PostgreSQL como envelope storage + Kafka como transport** — combinação atualmente em uso e validada nos spikes |

## Próximo passo recomendado

**S9 — Operação e observabilidade**: como inspecionar mensagens pendentes,
dead letters, ownership, replay e limpeza. Saída esperada: pequeno runbook
operacional para o guia Wolverine.

## Versões e ambiente

- `WolverineFx.*` 5.32.1-pr2586 do feed local.
- `Testcontainers.PostgreSql` 4.11.0; `Testcontainers.Kafka` 4.11.0.
- Postgres image: `postgres:18-alpine`.
- Runtime: .NET 10 / C# 14.
- Conta gh ativa: `marmota-alpina`.

## Referências

- [Plano de validação do outbox Wolverine (#158)](158-plano-validacao-outbox-wolverine.md)
- [Relatório S0](158-s0-relatorio.md), [S2](158-s2-relatorio.md), [S3](158-s3-relatorio.md), [S5](158-s5-relatorio.md), [S6](158-s6-relatorio.md), [S7](158-s7-relatorio.md)
