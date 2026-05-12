---
status: "accepted"
date: "2026-05-05"
decision-makers:
  - "Tech Lead (CTIC)"
---

# ADR-0039: Provisioning do schema Wolverine como responsabilidade do deploy, não auto-create em runtime

## Contexto e enunciado do problema

Wolverine 5.32.1 com `PersistMessagesWithPostgresql` cria as tabelas `wolverine.outbox`, `wolverine.envelopes`, `wolverine.dead_letters` etc. Por default, `WolverineOptions.AutoBuildMessageStorageOnStartup` está ligado — o framework cria o schema na primeira inicialização do host.

Em produção, runtime auto-create traz problemas:

- Risco de race quando dois pods iniciam simultaneamente.
- DDL sem revisão prévia em PR.
- Ausência de auditoria de migrations da messageria.
- Permissions: usuário runtime precisaria de `CREATE TABLE`, ampliando blast radius LGPD.

A pergunta: como provisionar o schema Wolverine?

## Drivers da decisão

- **Imutabilidade de runtime**: o pod produtivo deveria ter permissões mínimas (CRUD em tabelas existentes), não DDL.
- **Auditabilidade**: changes no schema da messageria devem passar pelo mesmo pipeline de PR + review que o schema do domínio.
- **Determinismo**: ambientes (dev, staging, prod) devem ter o mesmo schema validado, não cada um criando seu próprio na primeira inicialização.

## Opções consideradas

- **A. `AutoBuildMessageStorageOnStartup = true` (default Wolverine).**
- **B. Desligar auto-build em produção; provisionar via `dotnet wolverine db-apply` no pipeline ou step do deploy.**
- **C. Migration assembly EF Core dedicado para o schema Wolverine.**

## Resultado da decisão

**Escolhida:** "B — auto-build desligado em produção, provisioning como step explícito do deploy".

`WolverineOutboxConfiguration.UseWolverineOutboxCascading` configura `PersistMessagesWithPostgresql(connectionString, schema: "wolverine")` mas **não** chama `AutoBuildMessageStorageOnStartup()`. Em produção, o operador roda `dotnet wolverine db-apply` (ferramenta CLI do JasperFx) como step do pipeline antes de subir o pod, similar a `dotnet ef database update`.

Em testes integrados, a `CascadingFixture` usa `EnsureCreatedAsync` no Postgres efêmero — isso é OK porque (a) o banco existe só para o test run, (b) o usuário do testcontainer tem DDL, (c) a alternativa (rodar `dotnet wolverine db-apply` na fixture) seria custo desproporcional para test ergonomics.

## Consequências

### Positivas

- Pod runtime tem permissões CRUD-only no schema `wolverine.*` — superfície LGPD reduzida.
- Schema changes passam por PR + review.
- Determinismo cross-environment.
- Sem risco de race em deploys multi-pod.

### Negativas

- Step adicional no pipeline de deploy. Mitigação: documentar no playbook operacional.
- Dev local precisa rodar o comando uma vez antes de iniciar a API. Mitigação: adicionar ao `make dev` / docker-compose entrypoint.

### Neutras

- Decisão pode ser revisitada quando Wolverine introduzir migrations versionadas (parecidas com EF Core). Hoje o `db-apply` é idempotente mas não rastreia versões.

## Confirmação

- `WolverineOutboxConfiguration.UseWolverineOutboxCascading` em `src/shared/Unifesspa.UniPlus.Infrastructure.Core/Messaging/` **NÃO** chama `AutoBuildMessageStorageOnStartup`.
- Comentário inline na linha do `PersistMessagesWithPostgresql` documenta a decisão e aponta para o ADR.
- Em testes integrados, `CascadingFixture.InitializeAsync` provisiona via `EnsureCreatedAsync` (escopo: Postgres efêmero do testcontainer).

## Prós e contras das opções

### A — Auto-build em runtime

- Bom: zero step no deploy; primeira inicialização auto-resolve.
- Ruim: pod precisa de DDL; race em multi-pod; sem auditoria.

### B — `dotnet wolverine db-apply` no pipeline (escolhida)

- Bom: pod com permissões mínimas; auditável; determinístico.
- Ruim: step manual a documentar.

### C — Migration EF Core dedicado

- Bom: integra com o pipeline EF Core do domínio.
- Ruim: dupla manutenção (schema do Wolverine evolui independente das entidades do domínio); não é o caminho recomendado pelo JasperFx.

## Mais informações

- [JasperFx Wolverine — Database operations CLI](https://wolverinefx.io/guide/durability/postgresql.html)
- ADR-0040 — Helper `UseWolverineOutboxCascading`
- ADR-0026 — Outbox transacional via Wolverine
- Origem: spike S10 cascading; issue [#180](https://github.com/unifesspa-edu-br/uniplus-api/issues/180); PR [#172](https://github.com/unifesspa-edu-br/uniplus-api/pull/172)

## Atualizações posteriores

### 2026-05-11 — coabitação com `MigrationHostedService` (issue #416)

Duas evoluções relevantes desde a decisão original:

1. **Schema Wolverine: `AutoBuildMessageStorageOnStartup = CreateOrUpdate` reativado** (issue #344). Em ambientes Uni+ não há orquestração de schema externa ao host (sem step `dotnet wolverine db-apply` no Helm), então delegar a criação ao próprio host destrava bring-up de pods com banco vazio (standalone/lab). A opção B continua válida para produção PROD/HML quando o pipeline ganhar o step explícito; CreateOrUpdate é idempotente (delta-only) e seguro como default para os ambientes atuais. Ver `WolverineOutboxConfiguration.cs:177`.

2. **Schema EF Core do domínio: `MigrationHostedService<TContext>` no startup do host** (issue #344 + #416). `AddDbContextMigrationsOnStartup<>` registra um `IHostedService` que invoca `MigrateAsync`. Coordenação cross-replica usa lock do Npgsql sobre `__EFMigrationsHistory` (ver `MigrationServiceCollectionExtensions.cs:84`). Para que a primeira aplicação contra bancos com schema pré-existente (caso do standalone hoje) não falhe com 42P07, a migration `Up()` precisa ser escrita com guards explícitos (`CREATE TABLE IF NOT EXISTS`) — EF Core não emite isso por padrão.

3. **`CascadingFixture` mudou de `EnsureCreatedAsync` → `MigrateAsync`** (issue #416). Justificativa: a fixture precisa provisionar o schema do domínio ANTES do host startar para evitar a race com Wolverine outbox (timing histórico documentado em #344/#180). Com migrations EF Core agora registradas (PR #416), `EnsureCreatedAsync` colide com `MigrationHostedService` (42P07 relation already exists). `MigrateAsync` na fixture popula `__EFMigrationsHistory` e o `MigrationHostedService` do host vê banco já migrado e vira no-op. Resultado: dois caminhos chamam `MigrateAsync` (fixture + host), funcionalmente idempotente mas conceitualmente duplicado.

A coabitação fixture↔MigrationHostedService é tática. Follow-up planejado em [issue #419](https://github.com/unifesspa-edu-br/uniplus-api/issues/419): reordenar `Program.cs` para registrar `AddDbContextMigrationsOnStartup` antes de `UseWolverineOutboxCascading` nos 3 módulos (Selecao/Ingresso/Portal) + fitness test garantindo a ordem + remover `MigrateAsync` da fixture, restaurando 1 fonte de verdade para o schema do domínio.
