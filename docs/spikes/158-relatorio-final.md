# Relatório final — Validação do outbox Wolverine (#158)

- **Branch:** `spikes/158-relatorio-final`
- **Data:** 2026-04-25
- **Autor da execução:** Claude (Opus 4.7) — execução autônoma sequencial S0→S9 sob direção do Tech Lead.
- **Status:** **Validação técnica concluída.** Wolverine 5.32.1-pr2586 com `PersistMessagesWithPostgresql` é candidato técnico viável para o requisito do plano. Recomendação: prosseguir para ADR de adoção.

## Recomendação

**Adotar Wolverine 5.32.1-pr2586 como outbox transacional para domain events do UniPlus**, com a configuração validada nos spikes:

```csharp
options
    .PersistMessagesWithPostgresql(connectionString, schemaName: "wolverine")
    .EnableMessageTransport(_ => { });

options.Policies.UseDurableOutboxOnAllSendingEndpoints();

options.PublishMessage<EditalPublicadoEvent>().ToPostgresqlQueue("domain-events");
options.ListenToPostgresqlQueue("domain-events");

options.UseKafka(kafkaBootstrapServers).AutoProvision();
options.PublishMessage<EditalPublicadoEvent>().ToKafkaTopic("edital_events");

options.PublishDomainEventsFromEntityFrameworkCore<EntityBase>(
    entity => entity.DomainEvents);
```

Combinada com a decisão de **desligar `EnableRetryOnFailure`** em DbContexts usados por handlers Wolverine (centralizar retry em políticas do Wolverine).

Plano B (interceptor próprio) **fica em espera** — não foi necessário. Decisão final de adoção depende do ADR.

## Status final por AC do plano

| AC | Requisito | Spike | Status |
|---|---|---|---|
| AC1a | Persistência transacional do envelope com `SaveChanges` | S2/V4, S3/V5 | ✅ **Comprovado** em PG e Kafka |
| AC1b | Entrega ao destino após commit | S2/V4, S3/V5 | ✅ **Comprovado** em PG queue e Kafka topic |
| AC2  | Rollback elimina entidade e mensagem | S4/V4, S4/V5 | ✅ **Comprovado** em PG e Kafka |
| AC3  | Recuperação após restart / indisponibilidade | S5/V6 (parte 1 + parte 2), S6/V7 | ✅ **Comprovado integralmente** — envelope persiste durante indisponibilidade, é despachado automaticamente quando broker volta (KRaft), reassignment funciona após restart de host |
| AC4  | Migration auditável das tabelas Wolverine | S8 | ✅ **Superfície mapeada** (10 tabelas em 2 schemas). Decisão de versionamento (migration EF / DbContext dedicado / SQL) **registrada como recomendação A**, fechamento depende de ADR |
| AC5  | Retry EF/Wolverine sem conflito | S7 (variantes A e B) | ✅ **Comprovado** — variante A levanta `InvalidOperationException`; variante B (EF retry OFF) é a recomendação aplicada |

## Matriz V0–V7 final

| Variante | Configuração | Status | Spike | Observação |
|---|---|---|---|---|
| V0 | Stack base sem routing durável | ❌ **Reprovado por inviabilidade técnica do framework** | S0 | `EfCoreEnvelopeTransaction` exige message persistence; sem ela `InvalidOperationException` antes do scraper rodar |
| V1 | `Policies.UseDurableLocalQueues` sem routing explícito | ❌ Mesmo bloqueio de V0 | (não exercido) | Bloqueado pela mesma exigência |
| V2 | `PublishAllMessages().ToLocalQueue(...)` + local durable | ❌ Mesmo bloqueio | (não exercido) | Bloqueado pela mesma exigência |
| V3 | `PublishMessage<T>().ToLocalQueue(...)` + local durable | ❌ Mesmo bloqueio | (não exercido) | Bloqueado pela mesma exigência |
| V3a | Handler real via `IMessageBus.InvokeAsync` sem retry EF | ✅ Indireto | (subsumido por V4) | Validado dentro do S2 |
| V3a' | `PublishAllMessages().ToLocalQueue(...)` no `5.32.1-pr2586` | ❌ Mesmo bloqueio | (não exercido) | Bloqueado pela mesma exigência |
| V4 | PostgreSQL transport `ToPostgresqlQueue(...)` | ✅ **Aprovado** | S2/V4, S4/V4 | Caminho principal sem broker externo |
| V5 | Kafka transport com durable outbox PG | ✅ **Aprovado** | S3/V5, S4/V5 | Caminho com broker externo |
| V6 | Kafka indisponível no commit | ✅ **Aprovado** completo | S5/V6 (parte 1 e parte 2) | Parte 1 (envelope persiste) e parte 2 (despacho ao retorno via KRaft) — ambas comprovadas |
| V7 | Restart recovery | ✅ **Aprovado** | S6/V7 | Reassignment automático em <60s |

## Decisões fechadas

1. **Pacotes Wolverine**: usar `5.32.1-pr2586` do feed local em
   `vendors/nuget-local/`. Reverter para versão oficial quando upstream
   publicar `5.32.2+` contendo o fix de `JasperFx/wolverine#2586`. Critério
   de saída do feed local cobre 5 pacotes:
   - `WolverineFx`
   - `WolverineFx.EntityFrameworkCore`
   - `WolverineFx.RDBMS`
   - `WolverineFx.Postgresql`
   - `WolverineFx.Kafka`

2. **Retry strategy**: variante B (EF retry OFF + Wolverine retry).
   `AddSelecaoInfrastructure` precisa permitir desligar
   `EnableRetryOnFailure` quando outbox transacional estiver ativo.

3. **Durabilidade outbox**: `Policies.UseDurableOutboxOnAllSendingEndpoints()`
   é **obrigatório** — Wolverine não usa outbox durável por default em
   senders externos (Kafka, etc.).

4. **Convenção de nomenclatura**: usar **snake_case** desde a declaração
   para queues/tópicos (Wolverine normaliza `-` para `_` internamente —
   evita surpresas com clientes admin de Kafka).

5. **Conflito de versão NuGet**: prerelease `5.32.1-pr2586` < stable
   `5.32.1` em semver, então qualquer pacote `WolverineFx.*` oficial
   `5.32.1` introduz NU1605. **Solução**: gerar TODOS os pacotes Wolverine
   usados a partir do mesmo branch do fork.

6. **Bump Confluent.Kafka**: 2.13.2 → 2.14.0 (transitivo de
   `WolverineFx.Kafka`). Patch level, sem breaking changes.

## Decisões pendentes (para ADR / próxima Story)

1. **Versionamento das tabelas Wolverine** (caminho 2 do plano):
   recomendação A (migration EF do `SelecaoDbContext` via
   `MapWolverineEnvelopeStorage(modelBuilder, "wolverine")`) com
   `AutoBuildMessageStorageOnStartup` desligado em produção. Confirmação
   por ADR.

2. **Retenção de dead letters**: definir
   `DurabilitySettings.DeadLetterQueueExpiration` apropriado (default 10
   dias). Considerar LGPD e auditabilidade — possivelmente 30 dias.

3. **Endpoint admin de replay**: avaliar se construir
   `POST /admin/outbox/replay?type=...` usando
   `IDeadLetters.MarkDeadLetterEnvelopesAsReplayableAsync(...)` ou se
   replay manual via SQL/CLI atende.

4. **Política de drift fork × upstream**: definir que a atualização para
   `WolverineFx 5.32.2+` oficial **não é automática**. Vai exigir PR
   dedicado que re-executa **no mínimo** S2, S3, S5b e S7 da matriz
   antes do merge. Documentar critério no ADR.

5. **Política de adoção uniforme** (evitar "dois modelos de
   consistência" — handler A via Wolverine, handler B via EF direto):
   o ADR deve declarar que **todo handler que persiste agregado com
   `EntityBase.DomainEvents` é Wolverine**. Handler que faz query/lookup
   sem `SaveChanges` continua livre. Trava no review.

4. ~~**S5 parte 2** (despacho após retorno de broker externo)~~:
   **Concluído.** Re-executado com `apache/kafka:3.9.0` (KRaft puro) +
   `WithPortBinding` para preservar endereço do producer entre restarts.
   Despacho ao retorno funciona em <8s. Ver `158-s5b-relatorio.md`.

## Trabalhos seguintes recomendados

1. **Story de implementação produtiva** (consumir esta validação):
   - Refatorar `AddSelecaoInfrastructure` e `AddIngressoInfrastructure`
     para aceitar flag de retry desligado.
   - Configurar Wolverine no `Program.cs` de cada API conforme
     configuração validada.
   - Migrations EF para o schema `wolverine`.
   - Trocar `LinkProgramHandlerForFutureScraping` (placeholder) pelo
     scraper real em handlers existentes.
   - Atualizar `CLAUDE.md` removendo a trava sobre domain events.

2. **ADR formal de adoção** (`docs/adrs/ADR-NNN-outbox-wolverine-adotado.md`
   no `uniplus-docs`):
   - Status: aceito.
   - Decisão: adotar Wolverine como outbox transacional.
   - Consequências: lista das 6 decisões fechadas acima.
   - Promove e fecha #158.

3. **Issue para retorno do upstream**: agendar agente em ~2 semanas para
   verificar se `WolverineFx 5.32.2+` foi publicado oficialmente e
   abrir PR removendo o feed local. **Sugiro `/schedule`**.

4. **Padronizar todas as fixtures Kafka em KRaft + porta fixa**: a
   fixture geral (`OutboxCapabilityFixture`) ainda usa `cp-kafka:7.6.1`
   (Zookeeper). Migrar para `apache/kafka:3.9.0+` por consistência com
   produção (Kafka 4.2 KRaft) e para destravar futuros testes de
   resiliência que precisem de restart.

5. **Guardrails de produção** (validações no startup que falhem cedo se a
   configuração estiver inconsistente com os achados desta validação):
   - **Falhar startup** se algum `DbContextOptions<TContext>` registrado
     para uso por handlers Wolverine tem `EnableRetryOnFailure` ligado.
     Verificação via `IInterceptor`/`IDbContextOptionsExtension` — se
     `NpgsqlRetryingExecutionStrategy` aparecer no provider, lança no
     `IHostedService.StartAsync`.
   - **Falhar startup** se `Policies.UseDurableOutboxOnAllSendingEndpoints`
     não estiver aplicada e algum `PublishMessage<T>().ToKafkaTopic(...)`
     estiver configurado. Verificação no `WolverineOptions.Discovery`
     pós-build.
   - **Logar configuração de transport** no boot — `LogInformation`
     com lista de queues/topics + endpoint mode (durable/buffered) +
     persistence schema. Operacionalmente útil ao subir nó novo.

   Hoje essas regras vivem só no relatório/ADR — sem enforcement no código
   é questão de tempo até alguém ligar `EnableRetryOnFailure` por engano.

6. **Observabilidade mínima obrigatória** (não basta ter o runbook —
   precisa enforcement de coleta + alerta):
   - **Métricas Wolverine via OpenTelemetry**: o pacote `WolverineFx`
     emite contadores nativos (mensagens enviadas/recebidas/falhas,
     latência por handler). Adicionar `Wolverine` ao
     `OpenTelemetry.Trace.Builder.AddSource(...)` no `Program.cs`.
   - **Alertas de saúde do outbox** (Grafana/Prometheus):
     - Crescimento sustentado de `wolverine_outgoing_envelopes` (lag
       de despacho).
     - Crescimento de `wolverine_dead_letters` acima de threshold (ex.:
       >10 por hora — indica regressão no handler).
     - `wolverine_node_assignments` com node owner morto há >5 min —
       sinal de zombie.
   - **Dashboards no `repositories/uniplus-docs/grafana/`** (a criar).

   Sem isso, o sistema "está funcionando" mas backlog silencioso pode
   acumular sem ninguém notar até bater o quota do Postgres ou o cliente
   reclamar do atraso.

## Resultados consolidados

```
Aprovado S2/V4 — PG transport entrega EditalPublicadoEvent ao handler local
Aprovado S4/V4 — rollback PG: entidade ausente, envelope ausente
Aprovado S3/V5 — Kafka transport publica EditalPublicadoEvent no tópico
Aprovado S4/V5 — rollback Kafka: tópico não recebe mensagem fantasma
Aprovado S5/V6 (parte 1) — Kafka offline: envelope pendente em storage
Aprovado S5/V6 (parte 2) — Kafka volta: envelope retido é despachado (KRaft)
Aprovado S6/V7 — restart: novo host processa mensagem pendente
Aprovado S7 (variante A) — EF retry ON levanta conflito esperado
Aprovado S7 (variante B) — EF retry OFF é a recomendação aplicada
Aprovado S8 — schema 'wolverine' contém todas as tabelas esperadas
Aprovado S8 — superfície completa observada documentada
Aprovado S9 — handler que sempre falha gera entrada em dead letters
Aprovado S9 — query SQL de dead letters retorna snapshot inspecionável

Total de testes: 13
     Aprovados: 13
Tempo total: ~46s
```

## Histórico de branches

```
spikes/158-s0-handler-inmemory          → 1b159c6  S0/V0 reprovado por inviabilidade técnica
spikes/158-s2-transporte-postgresql     → fee75f5  S2/V4 + S4/V4 aprovados
spikes/158-s3-transporte-kafka          → dc88a42  S3/V5 + S4/V5 aprovados
spikes/158-s5-kafka-indisponivel        → 5c9ccee  S5/V6 parte 1 aprovado
spikes/158-s6-restart-recovery          → 1a38d61  S6/V7 aprovado
spikes/158-s7-retry-strategy            → 13531eb  AC5 formalmente comprovado
spikes/158-s8-migration-surface         → 14eba2e  Schema mapeado
spikes/158-s9-operacao                  → ba010b2  Runbook operacional + dead letters
spikes/158-relatorio-final              → 5fa3ac4  Consolidação inicial
spikes/158-s5b-kafka-kraft              → (este commit) S5/V6 parte 2 destravado com KRaft
```

Cada branch tem seu próprio relatório em `docs/spikes/158-s{N}-relatorio.md`.

## Histórico de pacotes locais gerados

Em `vendors/nuget-local/`, todos compilados a partir de
`/home/jeferson/Projects/workspaces/wolverine-fork` na branch
`fix/domain-event-scraper-materialize-before-publish` (commit `cd6a2ee`):

```
WolverineFx.5.32.1-pr2586.nupkg                          (PR #160 — base)
WolverineFx.EntityFrameworkCore.5.32.1-pr2586.nupkg      (PR #160 — base)
WolverineFx.RDBMS.5.32.1-pr2586.nupkg                    (PR #160 — base)
WolverineFx.Postgresql.5.32.1-pr2586.nupkg               (S2 — esta sessão)
WolverineFx.Kafka.5.32.1-pr2586.nupkg                    (S3 — esta sessão)
```

Comando padrão de geração:

```bash
dotnet pack <csproj-path> \
  -c Release \
  -p:Version=5.32.1-pr2586 \
  -p:PackageVersion=5.32.1-pr2586 \
  -o /tmp/wolverine-pack
```

## Bug de modelagem corrigido durante a validação

Encontrado durante S0, corrigido no commit `868b24e`:

```
fix(kernel): renomear parâmetro do construtor de NomeSocial para EF bindar
```

`NomeSocial(string nomeCivil, string? nomeSocial)` → `NomeSocial(string nomeCivil, string? nome)`. EF Core 10 não conseguia bindar o parâmetro `nomeSocial` na propriedade `Nome` do tipo. Bug de modelagem sem ligação com o outbox, exposto pelo primeiro caminho do projeto a materializar o schema completo do módulo Seleção contra Postgres real.

## Versões e ambiente

- **Pacotes Wolverine:** `5.32.1-pr2586` (todos do feed local).
- **Pacotes adicionais bumpados nesta validação:** `Confluent.Kafka` 2.13.2 → 2.14.0.
- **Pacotes adicionados nesta validação:** `Testcontainers.Kafka` 4.11.0.
- **Postgres:** `postgres:18-alpine`.
- **Kafka (geral):** `confluentinc/cp-kafka:7.6.1` (Zookeeper) — usado em S2/S3/S4/S5/S6/S7/S8/S9. Migração para KRaft puro recomendada (item 4 dos trabalhos seguintes).
- **Kafka (S5b):** `apache/kafka:3.9.0` (KRaft puro, sem Zookeeper) — alinhado com Kafka 4.2 KRaft de produção.
- **Runtime:** .NET 10 / C# 14, Linux 6.19.14-arch1-1, Docker 28.3.3.
- **Conta gh ativa nas sessões:** `marmota-alpina`.

## Referências

- [Plano de validação do outbox Wolverine (#158)](158-plano-validacao-outbox-wolverine.md)
- [Relatório S0](158-s0-relatorio.md) — V0 inviável + bug NomeSocial corrigido
- [Relatório S2](158-s2-relatorio.md) — V4 aprovado (PG transport)
- [Relatório S3](158-s3-relatorio.md) — V5 aprovado (Kafka transport)
- [Relatório S5](158-s5-relatorio.md) — V6 parte 1 aprovado, parte 2 pendente (resolvida em S5b)
- [Relatório S5b](158-s5b-relatorio.md) — V6 parte 2 aprovado (Kafka KRaft)
- [Relatório S6](158-s6-relatorio.md) — V7 aprovado (restart recovery)
- [Relatório S7](158-s7-relatorio.md) — AC5 formalmente comprovado
- [Relatório S8](158-s8-relatorio.md) — Schema mapeado, recomendação A
- [Relatório S9](158-s9-relatorio.md) — Runbook operacional
- [ADR-022 — backbone CQRS Wolverine](https://github.com/unifesspa-edu-br/uniplus-docs/blob/main/docs/adrs/ADR-022-backbone-cqrs-wolverine.md)
- [ADR-024 — outbox Wolverine + EF não adotado em #135](https://github.com/unifesspa-edu-br/uniplus-docs/blob/main/docs/adrs/ADR-024-outbox-wolverine-ef-nao-adotado-em-135.md)
- [PR uniplus-api#160 — feed local com fix do scraper](https://github.com/unifesspa-edu-br/uniplus-api/pull/160)
- [PR uniplus-api#161 — plano de validação](https://github.com/unifesspa-edu-br/uniplus-api/pull/161)
- [JasperFx/wolverine#2585](https://github.com/JasperFx/wolverine/issues/2585) e [#2586](https://github.com/JasperFx/wolverine/pull/2586)
