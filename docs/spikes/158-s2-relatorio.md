# Relatório dos Spikes S2 e S4 (PG) — Outbox Wolverine (#158)

- **Branch:** `spikes/158-s2-transporte-postgresql`
- **Branch base:** `spikes/158-s0-handler-inmemory` (`1b159c6`) → `main` (`54f1a8b`)
- **Data:** 2026-04-25
- **Status:** **AC1a, AC1b e AC2 do plano comprovados** com transporte PostgreSQL — 2 testes aprovados (`S2/V4` e `S4/V4`)
- **Plano de referência:** [`docs/spikes/158-plano-validacao-outbox-wolverine.md`](158-plano-validacao-outbox-wolverine.md)
- **Relatório anterior:** [`docs/spikes/158-s0-relatorio.md`](158-s0-relatorio.md)

## Resumo executivo

Transporte PostgreSQL do Wolverine entrega o invariante exigido pelo projeto:

- **S2/V4 (caminho feliz):** comando via `IMessageBus.InvokeAsync` altera entidade,
  gera `AddDomainEvent`, persiste envelope durável na mesma transação do
  `SaveChanges` e o `EditalPublicadoEvent` é entregue ao handler subscritor
  local pela queue `domain-events` do transport — **sem `Collection was modified`**.
- **S4/V4 (rollback):** exceção depois de `SaveChanges` e antes do retorno do
  handler deixa entidade ausente em `editais`, **sem envelope confirmado** em
  `wolverine.wolverine_outgoing_envelopes` e **sem entrega** ao handler subscritor.

Critérios bloqueantes do plano cobertos nesta fase:

| AC | Requisito | Status nesta fase |
|---|---|---|
| AC1a | Persistência transacional do envelope com `SaveChanges` | **Comprovado** em `S2/V4` |
| AC1b | Entrega ao destino após commit | **Comprovado** em `S2/V4` (queue PostgreSQL) |
| AC2 | Rollback elimina entidade e mensagem | **Comprovado** em `S4/V4` |
| AC3 | Recuperação após restart | Pendente — endereçado em S6 |
| AC4 | Migration auditável das tabelas Wolverine | Pendente — endereçado em S8 |
| AC5 | Retry EF/Wolverine sem conflito | **Decisão antecipada do S7 aplicada:** desligar `EnableRetryOnFailure` em DbContexts usados por handlers Wolverine. Sem isso, a transação Wolverine lança `The configured execution strategy 'NpgsqlRetryingExecutionStrategy' does not support user-initiated transactions` |

## Configuração validada

```csharp
options
    .PersistMessagesWithPostgresql(connectionString, schemaName: "wolverine")
    .EnableMessageTransport(_ => { });

options.PublishMessage<EditalPublicadoEvent>().ToPostgresqlQueue("domain-events");
options.ListenToPostgresqlQueue("domain-events");

options.PublishDomainEventsFromEntityFrameworkCore<EntityBase>(
    entity => entity.DomainEvents);
```

`UsePostgresqlPersistenceAndTransport(...)` foi marcado como obsoleto em
5.32.1-pr2586. A API recomendada é `PersistMessagesWithPostgresql(...).EnableMessageTransport(...)`.

## Achados desta fase

### B1 — Conflito NU1605 com `WolverineFx.Postgresql` oficial

Tentativa inicial: adicionar `WolverineFx.Postgresql 5.32.1` (oficial nuget.org)
ao `Directory.Packages.props`. Resultado:

```
error NU1605: Downgrade de pacote detectado: WolverineFx de 5.32.1 para 5.32.1-pr2586.
Unifesspa.UniPlus.Selecao.IntegrationTests
  -> WolverineFx.EntityFrameworkCore 5.32.1-pr2586
  -> WolverineFx.RDBMS 5.32.1
  -> WolverineFx (>= 5.32.1)
Unifesspa.UniPlus.Selecao.IntegrationTests
  -> WolverineFx (>= 5.32.1-pr2586)
```

Causa: prerelease `5.32.1-pr2586` é menor que stable `5.32.1` em semver. A
constraint `>= 5.32.1` que o pacote oficial transitivamente exige NÃO é
satisfeita pelo prerelease. O plano antecipou isso (linhas 51–55: "Se S2 ou S3
exigirem `WolverineFx.Postgresql`, há risco de conflito... gerar também
`WolverineFx.Postgresql` a partir do mesmo branch do fork e publicar no feed
local antes de interpretar erros NuGet como falha funcional do spike").

**Solução aplicada:** gerar `WolverineFx.Postgresql 5.32.1-pr2586` localmente e
adicionar ao feed `vendors/nuget-local/`:

```bash
dotnet pack /home/jeferson/Projects/workspaces/wolverine-fork/src/Persistence/Wolverine.Postgresql/Wolverine.Postgresql.csproj \
  -c Release \
  -p:Version=5.32.1-pr2586 \
  -p:PackageVersion=5.32.1-pr2586 \
  -o /tmp/wolverine-pack

cp /tmp/wolverine-pack/WolverineFx.Postgresql.5.32.1-pr2586.nupkg \
   /home/jeferson/Projects/workspaces/uniplus/repositories/uniplus-api/vendors/nuget-local/
```

E mapear `WolverineFx.Postgresql` ao package source `wolverine-pr2586` no
`nuget.config`. Critério de saída do feed local (do plano §"Snapshot da versão
em validação") agora inclui o `WolverineFx.Postgresql` por extensão.

### B2 — `EfCoreEnvelopeTransaction` exige message persistence (carry-over de S0)

A descoberta de S0 que **bloqueou V0** (estack base sem routing durável) é
exatamente o que destrava S2: ao adicionar `PersistMessagesWithPostgresql`, o
`EfCoreEnvelopeTransaction` consegue inicializar e o pipeline scraper → outbox
transacional → queue → handler funciona ponta a ponta.

### B3 — `NpgsqlRetryingExecutionStrategy` × transação Wolverine

`AddSelecaoInfrastructure` em produção registra:

```csharp
options.UseNpgsql(connectionString, npgsqlOptions =>
{
    npgsqlOptions.EnableRetryOnFailure(maxRetryCount: 3, ...);
});
```

Quando o handler Wolverine chama `db.SaveChangesAsync` dentro da transação
Wolverine, lança:

```
System.InvalidOperationException : The configured execution strategy
'NpgsqlRetryingExecutionStrategy' does not support user-initiated transactions.
Use the execution strategy returned by 'DbContext.Database.CreateExecutionStrategy()'
to execute all the operations in the transaction as a retriable unit.
```

**Solução aplicada (alinhada com S7 do plano):** no test factory, registrar
`SelecaoDbContext` sem retry strategy. Implicação para produção: quando o
outbox Wolverine for adotado em `Selecao.API`, será preciso desligar
`EnableRetryOnFailure` no `AddSelecaoInfrastructure` ou usar
`IExecutionStrategy.ExecuteAsync` em todo handler. O plano §S7 recomenda a
primeira opção.

#### B3.1 — `RemoveAll<DbContextOptions<T>>` não é suficiente

`services.RemoveAll<DbContextOptions<SelecaoDbContext>>()` sozinho não desfaz a
configuração registrada por `AddDbContext`. Os configures são inseridos via
`IConfigureOptions<DbContextOptions<TContext>>` e
`IDbContextOptionsConfiguration<TContext>`, e ambos sobrevivem ao `RemoveAll`.
Foi preciso varrer essas interfaces antes de re-registrar. Ver helper
`RemoveAllOptionsConfigurations<TOptions>` em
`tests/.../Outbox/Capability/OutboxCapabilityApiFactory.cs`.

### B4 — Disputa de timing entre `EnsureCreatedAsync` e Wolverine bootstrap

Tentativa inicial: chamar `db.Database.EnsureCreatedAsync()` no início do
método de teste, antes de `host.TrackActivity().InvokeMessageAndWaitAsync(...)`.
Resultado:

```
Npgsql.PostgresException : 42P01: relation "editais" does not exist
```

Apesar de o `EnsureCreatedAsync` aparentemente ter rodado, o handler do
Wolverine não enxergava a tabela `editais`. A causa provável é o startup do
`PostgresqlTransport`, que já tinha sido disparado pelo `api.CreateClient()`
antes do `EnsureCreatedAsync`, e o handler usava um pool de conexões com
metadata stale.

**Solução aplicada:** criar o schema do domínio Selecao na própria
`OutboxCapabilityFixture.InitializeAsync`, em DbContext standalone, **antes**
de instanciar a `OutboxCapabilityApiFactory`:

```csharp
public async Task InitializeAsync()
{
    await _postgres.StartAsync();
    OutboxSpikeWolverineExtension.ConnectionString = ConnectionString;

    DbContextOptions<SelecaoDbContext> options = new DbContextOptionsBuilder<SelecaoDbContext>()
        .UseNpgsql(ConnectionString)
        .Options;

    await using SelecaoDbContext db = new(options);
    await db.Database.EnsureCreatedAsync();

    _factory = new OutboxCapabilityApiFactory(ConnectionString);
}
```

Implicação: o ciclo "schema do domínio sempre antes do host Wolverine" é
condição de teste, não cabe a discussão sobre migrations Wolverine (S8).

## Resultados

```bash
dotnet test tests/Unifesspa.UniPlus.Selecao.IntegrationTests/Unifesspa.UniPlus.Selecao.IntegrationTests.csproj \
  --filter "Category=OutboxCapability"
```

```
Aprovado S2/V4 — PG transport entrega EditalPublicadoEvent ao handler local
   após Publicar+SaveChanges, scraper sem 'Collection was modified' [5 s]
Aprovado S4/V4 — rollback PG: exceção pós-SaveChanges deixa entidade ausente,
   envelope sem registro confirmado e handler subscritor sem entrega [164 ms]

Total de testes: 2
     Aprovados: 2
Tempo total: 11,1415 Segundos
```

## Atualização da matriz V0–V7

Linhas a atualizar em [`158-plano-validacao-outbox-wolverine.md`](158-plano-validacao-outbox-wolverine.md):

| Variante | Configuração | Esperado após fix | Observado | Status |
|---|---|---|---|---|
| V0 | Stack base sem routing durável | drenagem sem envelope durável | `EfCoreEnvelopeTransaction` exige persistence; sem ela `InvalidOperationException` antes do scraper rodar | **Reprovado por inviabilidade técnica do framework** (registrado em S0) |
| V4 | PostgreSQL transport `ToPostgresqlQueue(...)` | Outbox durável sem Kafka | Persistence + transport PG inicializam, scraper drena sem `Collection was modified`, queue entrega ao handler subscritor; rollback após `SaveChanges` deixa entidade e envelope ausentes | **Aprovado** (`S2/V4` e `S4/V4`) |

## Configuração final do spike (referência)

| Componente | Valor |
|---|---|
| Persistence schema | `wolverine` |
| Transport schema | default (`wolverine_queues`) — não customizado |
| Queue de domain events | `domain-events` |
| Connection string | dinâmica via Testcontainers `postgres:18-alpine` |
| Retry EF/Npgsql | **desligado** no test factory (decisão de S7 aplicada antecipadamente) |
| Storage bootstrap | `JasperFxOptions.AutoBuildMessageStorageOnStartup = CreateOrUpdate` (default) |

## Próximo passo recomendado

**S3 — Transporte Kafka** ([§S3 do plano](158-plano-validacao-outbox-wolverine.md#s3---transporte-kafka)).

Pré-requisitos para S3:

1. `WolverineFx.Kafka` no `Directory.Packages.props` (versão a decidir — mesmo
   risco de conflito do B1 acima; gerar `5.32.1-pr2586` no fork local se
   necessário).
2. Container Kafka via Testcontainers (`Testcontainers.Kafka` ainda não está em
   `Directory.Packages.props`).
3. Estender `OutboxSpikeWolverineExtension` mantendo PG persistence (envelope
   storage continua em PG, conforme padrão Wolverine) mas roteando
   `EditalPublicadoEvent` para tópico Kafka — testar caminho com broker real do
   projeto.
4. Repetir S4 para Kafka (rollback) — comportamento esperado idêntico ao
   S4/V4 deste relatório, agora exigindo que **Kafka não receba** mensagem
   quando a transação reverte.

A partir de S3, pode ser interessante consolidar o `OutboxCapabilityFixture`
com containers Postgres **e** Kafka. Decisão pode esperar até confirmar a
viabilidade do `WolverineFx.Kafka` 5.32.1-pr2586.

## Versões e ambiente

- **Pacotes Wolverine:** `WolverineFx`, `WolverineFx.EntityFrameworkCore`,
  `WolverineFx.RDBMS`, `WolverineFx.Postgresql` — todos `5.32.1-pr2586` em
  `vendors/nuget-local/`.
- **Pacote oficial introduzido nesta fase:** nenhum.
- **Pacote local gerado nesta fase:** `WolverineFx.Postgresql.5.32.1-pr2586.nupkg`
  a partir de `/home/jeferson/Projects/workspaces/wolverine-fork` na branch
  `fix/domain-event-scraper-materialize-before-publish` (`cd6a2ee`).
- **`Testcontainers.PostgreSql`:** 4.11.0.
- **Postgres do spike:** imagem `postgres:18-alpine` em container efêmero.
- **Runtime:** .NET 10 / C# 14, Linux 6.19.14-arch1-1, Docker 28.3.3.
- **Conta gh ativa:** `marmota-alpina`.

## Referências

- [Plano de validação do outbox Wolverine (#158)](158-plano-validacao-outbox-wolverine.md)
- [Relatório S0 — V0 inviável](158-s0-relatorio.md)
- [ADR-022 — backbone CQRS Wolverine](https://github.com/unifesspa-edu-br/uniplus-docs/blob/main/docs/adrs/ADR-022-backbone-cqrs-wolverine.md)
- [ADR-024 — outbox Wolverine + EF não adotado em #135](https://github.com/unifesspa-edu-br/uniplus-docs/blob/main/docs/adrs/ADR-024-outbox-wolverine-ef-nao-adotado-em-135.md)
- [PR uniplus-api#160 — feed local com fix do scraper](https://github.com/unifesspa-edu-br/uniplus-api/pull/160)
- [PR uniplus-api#161 — plano de validação](https://github.com/unifesspa-edu-br/uniplus-api/pull/161)
- [JasperFx/wolverine#2585](https://github.com/JasperFx/wolverine/issues/2585) e [#2586](https://github.com/JasperFx/wolverine/pull/2586)
