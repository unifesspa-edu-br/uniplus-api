# Checkpoint — Spike: Monólito Modular Purista

> Artefato de trabalho da branch `spike/monolito-modular`. Não é ADR. Documenta o
> estado da validação para retomada. Após validado, vira a base do ADR; este
> arquivo é removido no rollout.

## Objetivo e método

Validar **construindo** que o backend pode migrar do "monólito modular distribuído"
atual (6 hosts, 6 bancos, só Kafka) para o **monólito modular purista** (1 processo,
1 banco com schema-por-módulo, leitura cross-módulo in-process, fronteiras
preservadas para extração futura). Sequência: **spike → confirmar → ADR → rollout**.

## Decisões travadas

- **Monólito** = núcleo administrativo interno: **Selecao, Ingresso, Configuracao, OrganizacaoInstitucional**.
- **Geo**: permanece **deploy separado** (host/banco/PostGIS próprios; consumo por composição-no-cliente, ADR-0090). *(Decidido.)*
- **Portal**: recomendado **separado** (BFF público; sem acesso in-process aos schemas internos). *(Confirmar no ADR.)*
- **IUnitOfWork**: **interface por módulo**. *(Decidido e implementado.)*
- **#588 (OfertaCurso)**: **aguarda** esta modelagem; depois lê a Unidade in-process.
- **#730** (POC DB-direct): a fechar pós-ADR (superada por in-process).

## Feito (validado)

- [x] Branch `spike/monolito-modular` criada a partir de `main`.
- [x] Parâmetro `schema` opcional em `UseUniPlusNpgsqlConventions` + `BuildDesignTimeOptions` (commit `f39360b`).
- [x] `IUnitOfWork` por módulo: `I{Modulo}UnitOfWork` nos 4 módulos; DbContext implementa; handlers injetam; testes ajustados (commit `a140128`).
- [x] **Build limpo (0 erro / 0 aviso)** + **unit tests verdes (71 Configuracao + 34 Organizacao)**.
- [x] **Schema-por-módulo PROVADO em Configuracao** (commit `18daddd`): `HasDefaultSchema` + schema na DI/design-time + migrations regeneradas (squash no schema) + SQL cru de teste qualificado. `has-pending-model-changes` limpo; **62 testes de integração (Testcontainers + MigrateAsync) verdes**.

## A fazer

### P2 — schema-por-módulo
- [x] **Configuracao**: `HasDefaultSchema` + DI/design-time + migration regenerada + testes. **Validado** (`18daddd`).
- [ ] **Organizacao**: idem — **atenção:** preservar SQL cru (`immutable_unaccent` + índice trigram `idx_unidade_nome_trgm`) na regeneração; qualificar índice em `organizacao.unidade`. (Produtor da prova P5.)
- [ ] **Selecao**: idem (checar SQL cru nas migrations antes de squash).
- [ ] **Ingresso**: idem (esqueleto — provavelmente simples).
- [ ] Connection por módulo → banco `uniplus`, com role/`search_path` por schema (feito junto do host/P3).
- [ ] `init-db.sql` (variante spike): banco `uniplus` + 4 schemas + role-por-schema; `uniplus_geo` intacto.

> **Padrão validado por módulo:** (1) `const Schema` + `HasDefaultSchema(Schema)` no DbContext; (2) `schema:` na DI e na design-time factory; (3) `dotnet ef migrations add InitialCreate` com `--startup-project` no `.API` (necessário no .NET 10 p/ o tooling resolver runtime); (4) **re-adicionar SQL cru** (funções/índices via `migrationBuilder.Sql`) na nova InitialCreate, qualificado ao schema; (5) qualificar SQL cru dos testes (`INTO/FROM/UPDATE <tabela>` → `<schema>.<tabela>`); (6) validar `has-pending-model-changes` limpo + integração verde.

### P3 — composition root
- [ ] `Add{Modulo}Module(IServiceCollection, IConfiguration)` por módulo (extrair do `Program.cs`).
- [ ] Projeto `src/host/Unifesspa.UniPlus.Host` + `HostAssemblyMarker` + `.slnx`.
- [ ] Centralizar compartilhado no host (Serilog, AddControllers, OIDC, CORS, cache, storage, encryption, cursor pagination, idempotency por DbContext, observabilidade, middleware, endpoints compartilhados).
- [ ] Compor os 4 módulos + `AddDbContextMigrationsOnStartup<T>` por módulo.

### P4 — Wolverine consolidado
- [ ] Refatorar `UseWolverineOutboxCascading` para compor discovery+routing dos 4 módulos numa instância (hook `ConfigureWolverine` por módulo).
- [ ] Outbox no schema `wolverine` único; Kafka só externo (Selecao); eventos internos em fila local durável.
- [ ] Preservar ordem migrations→Wolverine (`MigrationBeforeWolverineRuntimeOrderTests`).

### P5 — prova de leitura in-process (objetivo central)
- [ ] Host registra `IUnidadeReader`; Configuração consome.
- [ ] Teste de integração: Configuração lê uma Unidade viva in-process (o desbloqueio da #588).

### P1 / P6 / P7
- [ ] Prefixar rotas `/api/{modulo}/` em Configuracao e Organizacao; checar colisão no monólito.
- [ ] `CrossModuleReadIsolationTests`: isentar o host do `ModulesRoster`; fato novo "host pode compor todos".
- [ ] OpenAPI por-módulo no host (transformer de filtro); baselines `contracts/openapi.*.json` válidos.
- [ ] `Dockerfile` + serviço compose único + `init-db` variante (ops mínima para bootar/testar).
- [ ] Gate final: **`dotnet build` + `dotnet test` completo verde**; smoke `/api/{modulo}/...`.

## Decisões para o ADR (pós-spike)
- Portal público dentro/fora do monólito.
- Isolamento: role-por-schema + connection por módulo (recomendado) vs role única.
- Migração de dados em prod: confirmar fase de scaffolding (sem dado real) → "recriar".
- Wolverine: outbox no schema `wolverine` único (banco único) — confirmar durabilidade/at-least-once.
- OpenAPI: docs por-módulo (mantém baselines) vs doc único.

## Achados (registrar no ADR)
- **`IUnitOfWork` e registros scoped por módulo assumiam "um módulo por processo"** — co-hosting exigiu interface por módulo. Primeiro custo real revelado pelo spike.
- **`packages.lock.json` com drift latente** — `dotnet restore` adiciona deps transitivas (ex.: `Microsoft.Extensions.Caching.Memory`); revertido no spike, mas confirmar/regenerar no rollout.
- **Squash de migrations descarta SQL cru** — módulos com função/índice/trigger via `migrationBuilder.Sql` (ex.: Organizacao: `immutable_unaccent` + índice trigram) precisam re-adicionar esse SQL na InitialCreate regenerada, qualificado ao schema. Configuracao só tinha extensão vestigial (`btree_gist`), descartada sem impacto.
- **`dotnet ef` no .NET 10 exige `--startup-project`** no projeto `.API` (a lib Infrastructure sozinha não fornece runtimeconfig; sem isso, "Could not load System.Runtime 10.0.0.0").
- **SQL cru em testes referencia tabelas sem schema** — qualificar com `<schema>.` ao adotar schema-por-módulo (3 arquivos em Configuracao).

## Como retomar
Branch `spike/monolito-modular`. Próximo passo sugerido: **P2 (HasDefaultSchema + migrations num módulo, validar, replicar)** ou **P3 (Add{Modulo}Module → host)**. Plano completo em `~/.claude/plans/qual-a-forma-replicated-journal.md`.
