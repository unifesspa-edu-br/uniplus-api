---
status: "accepted"
date: "2026-05-13"
decision-makers:
  - "Tech Lead (CTIC)"
consulted: []
informed:
  - "Equipe Uni+"
---

# ADR-0054: Naming convention global e estratégia de migrations EF Core

## Contexto e enunciado do problema

Desde abril/2026 a issue [#155](https://github.com/unifesspa-edu-br/uniplus-api/issues/155) está aberta como ponto único de decisão sobre **como gerar e executar migrations EF Core para as entidades de domínio**. Antes desta ADR, o projeto apresentava:

- **3 DbContexts** (Selecao, Ingresso, Portal) em **3 bancos físicos isolados** (`uniplus_selecao`, `uniplus_ingresso`, `uniplus_portal`), cada um com usuário PostgreSQL dedicado.
- **Naming inconsistente**: tabelas e colunas de domínio em `snake_case` aplicado manualmente via `ToTable("editais")` / `HasColumnName("numero_edital")`, audit fields (`CreatedAt`, `UpdatedAt`, `IsDeleted`, `DeletedAt`, `DeletedBy`) em `PascalCase` (sem `HasColumnName`), gerando colunas `"CreatedAt"` quoted no PostgreSQL.
- **Somente Selecao tem migration** (`20260512010258_InitialCreate`, gerada de forma idempotente em #416 para destravar `idempotency_cache`). Ingresso e Portal **não têm migration** — fixtures criavam schema via `EnsureCreatedAsync` (substituído por `MigrateAsync` em #416 + #419).
- **Sem ADR formal nem guia operacional** sobre persistência → cada Story que mexia em entity discutia naming/casing do zero.

Entregas táticas anteriores (#344, #390, #416, #419) consolidaram partes da infraestrutura (MigrationHostedService, AuditableInterceptor, ordem `Migration→WolverineRuntime`) sem fechar a decisão central de **convention** e **estratégia**.

Esta ADR fecha #155 com decisão única e habilita um guia operacional (`docs/guia-banco-de-dados.md`) que serve como referência canônica para o time.

## Drivers da decisão

- **Consistência de schema**: cada PR que toca entity não pode reabrir o debate de naming.
- **Reduzir manutenção manual**: `HasColumnName` em cada propriedade é frágil e divergente do que devs ad-hoc geram.
- **Compatibilidade com PostgreSQL idiomático**: `snake_case` é convenção idiomática de PG; identifiers quoted (`"CreatedAt"`) são case-sensitive e geram fricção em scripts manuais.
- **Forward-only**: o projeto está pré-produção; reverts complexos com `Down()` não justificam o custo cognitivo (forward-only é o padrão da indústria para migrations em produção).
- **Não introduzir patterns paralelos**: 1 só forma de mapear cada conceito (entidade, VO, audit).

## Opções consideradas

### Naming convention

- **A**: `snake_case` manual via `HasColumnName` (status quo).
- **B**: `EFCore.NamingConventions` (NuGet OSS, Yiisang) com `UseSnakeCaseNamingConvention()`.
- **C**: Custom `IConventionSetPlugin` in-house.

### Value Objects

- **D**: `OwnsOne` (status quo).
- **E**: `ComplexProperty` (EF 10) universal — substitui OwnsOne.
- **F**: `HasConversion` (scalar via `ValueObjectConventions`) universal.
- **G**: Híbrido (1-campo via `HasConversion`, composto via `ComplexProperty`).

### Migrations

- **H**: Migration por Story que mexe schema (1 por mudança incremental).
- **I**: Squash periódico (consolidar histórico ao final de cada milestone).

### Revert

- **J**: Forward-only (revert = nova migration `Reverte_xxx`).
- **K**: `Down()` ativo em todas migrations (reversível bidirecionalmente).

## Resultado da decisão

### Naming convention: **B — `EFCore.NamingConventions` 10.0.1**

Aplicado globalmente via `UseSnakeCaseNamingConvention()` no helper `UniPlusDbContextOptionsExtensions.UseUniPlusNpgsqlConventions<TContext>`. Cobre **tabelas, colunas, índices, PKs, FKs** sem `HasColumnName` manual. NuGet maduro (12M+ downloads, mantenedor ativo, alinhado com EF 10). Custom plugin (Opção C) traria custo de manutenção sem ganho — biblioteca cobre o caso.

### Value Objects: **D — preservar `OwnsOne` (decisão diferida em #397)**

A intenção original era migrar para **E (ComplexProperty universal)** porque elimina shadow keys e table splitting. Bloqueio técnico encontrado durante a implementação desta ADR: **EF Core 10 ainda não suporta `HasIndex` em subpropriedade de `ComplexProperty`** ([doc oficial](https://learn.microsoft.com/en-us/ef/core/modeling/complex-types) confirma; issue [dotnet/efcore#31250](https://github.com/dotnet/efcore/issues/31250) acompanha). Como o domínio Uni+ exige unique constraints sobre VOs 1-campo (Cpf — RN01; ProtocoloConvocacao), uma migração universal para ComplexProperty hoje obriga `CREATE UNIQUE INDEX` via SQL raw em migration, vazando schema para fora do model EF.

Status quo `OwnsOne` cobre o cenário (com `HasIndex(v => v.Valor).IsUnique()` funcional). A decisão **fica diferida para a issue [#397](https://github.com/unifesspa-edu-br/uniplus-api/issues/397)** quando o suporte EF for resolvido — `ValueObjectConventions` e os 4 `ValueConverter` ficam disponíveis para uso pontual ou para o cutover futuro.

### Estratégia de migration: **H + J — migration por Story, forward-only**

- **H**: Cada Story que altera schema gera sua própria migration nomeada semanticamente (`AdicionaCampoBonusRegional`, `RenomeiaTabelaXyz`). Sem squash inicial — histórico curto, baixa entropia.
- **J**: Revert = nova migration `Reverte_xxx`. Migrations `Down()` ficam vazias ou lançam `InvalidOperationException` em produção. Justificativa: `Down()` em produção é fonte de erros (perda de dados sem audit), e a alternativa "nova migration que reverte" deixa rastro em `__EFMigrationsHistory` + audit trail.

### Aplicação no startup: já resolvido em ADRs anteriores

`MigrationHostedService<TContext>` aplica migrations no `StartAsync` do host (#344), registrado **antes** de `UseWolverineOutboxCascading` em cada Program.cs (#419). Fitness test `MigrationBeforeWolverineRuntimeOrderTests` em `tests/Unifesspa.UniPlus.ArchTests/Hosting/` trava a ordem. Aplicação no deploy do standalone é descrita em `RUNBOOKS.md`.

## Consequências

### Positivas

- Naming convention única e automática — tabelas, colunas, índices e FKs em `snake_case` sem `HasColumnName` manual.
- Helper `UseUniPlusNpgsqlConventions` centraliza wire-up: 1 ponto de mudança para 3 módulos.
- Forward-only reduz risco de perda de dados em produção.
- Guia operacional (`docs/guia-banco-de-dados.md`) elimina debate por PR.

### Negativas

- **Schema atual desalinhado da convention**: `20260512010258_InitialCreate` em Selecao cria audit fields em PascalCase quoted (`"CreatedAt"`); convention global expecta `created_at`. Em runtime, isso bate por insensibilidade de case do PG quando identificadores são emitidos sem aspas pelo EF; permanece como débito documentado (ver "Confirmação" abaixo).
- **EF tool 10.0.x quebrado em .NET 10 SDK 10.0.104** (`FileNotFoundException: System.Runtime, Version=10.0.0.0`): impede `dotnet ef migrations add` localmente. Workaround: `IDesignTimeDbContextFactory` criado nos 3 Infrastructure aguardando fix do EF tool ou rodada via container .NET oficial.
- **ComplexProperty universal adiado**: pattern `OwnsOne` continua nas Configurations existentes. Resolve quando #397 for retomada.

### Neutras

- Decisão pode ser revisitada quando EF Core suportar `HasIndex` em ComplexProperty (rastreado em #397) ou quando `Vogen`/`StronglyTypedId` virem prioridade para IDs tipados (ADR separada).

## Confirmação

- **Runtime**: helper `UseUniPlusNpgsqlConventions` aplica `UseSnakeCaseNamingConvention()`. Validável via `dotnet test` (suite completa verde) e via `EXPLAIN` de queries reais em ambiente local (todas referências às colunas em `snake_case`).
- **Schema drift**: o `InitialCreate` atual em Selecao cria audit em PascalCase. Quando o EF tool for resolvido (ou container `.NET 10 SDK` mais recente estiver disponível), uma nova Story regenera `InitialCreate` ou cria migration `NormalizaAuditColumnsParaSnakeCase` que faz `ALTER TABLE … RENAME COLUMN`. Documentado em `RUNBOOKS.md` §19.
- **Ingresso InitialCreate**: ausente; será gerado pela primeira Story que tocar entity Ingresso ou por refactor dedicado quando EF tool estiver utilizável.

## Prós e contras das opções

### A — snake_case manual

- Bom, porque é o que já existe (zero esforço de adoção).
- Ruim, porque é frágil (cada nova entity vai esquecer `HasColumnName` ou divergir do padrão), audit fields ficam órfãos.

### B — EFCore.NamingConventions (escolhida)

- Bom, porque automatiza para todas tabelas/colunas/índices/FKs; cobre audit sem `HasColumnName`.
- Ruim, porque introduz NuGet externo (~ trivial; lib madura).

### C — Custom IConventionSetPlugin

- Bom, porque zero dep externa.
- Ruim, porque vira código de manutenção interna (PgConvention, IndexConvention, ForeignKeyConvention etc.).

### D — OwnsOne (preservada por bloqueio técnico)

- Bom, porque suporta `HasIndex` em sub-prop.
- Ruim, porque tem shadow keys, table splitting, overhead de tracking.

### E — ComplexProperty universal (ideal, adiada)

- Bom, porque sem shadow keys / table splitting / overhead.
- Ruim, porque EF 10 não suporta `HasIndex` em sub-prop (bloqueio).

### F — HasConversion universal

- Bom, porque 1 só coluna por VO.
- Ruim, porque perde query LINQ por subpropriedade (`where edital.NumeroEdital.Ano == 2026` deixa de funcionar para VOs compostos).

### G — Híbrido (deferido)

- Bom, porque combina vantagens (HasConversion para 1-campo, ComplexProperty para composto).
- Ruim, porque 2 patterns conviventes — viola "1 só forma". Bloqueado pelo mesmo bug de HasIndex em ComplexProperty.

### H — Migration por Story (escolhida)

- Bom, porque histórico granular, fácil revert via nova migration.
- Ruim, porque histórico cresce linearmente.

### I — Squash periódico

- Bom, porque histórico enxuto.
- Ruim, porque perde traceability de mudança individual.

### J — Forward-only (escolhida)

- Bom, porque elimina classe inteira de bugs de `Down()` em produção.
- Ruim, porque revert exige uma nova migration explícita (não "rollback automático").

### K — Down() ativo

- Bom, porque reverso bidirecional.
- Ruim, porque `Down()` é difícil de testar e tem alto risco de perda de dados em produção.

## Mais informações

- **Guia operacional**: [`docs/guia-banco-de-dados.md`](../guia-banco-de-dados.md) — naming convention completa, tipos PG preferidos, soft delete, audit, workflow de migration, FAQ.
- **CONTRIBUTING**: seção "Migrations EF Core" referencia o guia.
- **RUNBOOK standalone**: `RUNBOOKS.md` §19 cobre como aplicar/regenerar migrations no cluster e o follow-up quando o EF tool for resolvido.
- **ADRs relacionadas**: [ADR-0007](0007-postgresql-como-banco-de-dados-primario.md), [ADR-0027](0027-idempotency-key-opt-in-com-store-postgresql.md), [ADR-0032](0032-guid-v7-para-identidade-de-entidades.md), [ADR-0039](0039-provisioning-schema-wolverine-via-deploy.md), [ADR-0040](0040-helper-wolverine-outbox-cascading-canonico.md).
- **Issues correlatas**: #155 (esta), #397 (ComplexProperty cutover diferido), #420 (FKs faltantes em `inscricoes`/`processos_seletivos`).
- **Documentação externa**:
  - [Microsoft Learn — Owned Entities](https://learn.microsoft.com/en-us/ef/core/modeling/owned-entities)
  - [Microsoft Learn — Complex Types (EF 10)](https://learn.microsoft.com/en-us/ef/core/modeling/complex-types)
  - [Microsoft Learn — Value Conversions](https://learn.microsoft.com/en-us/ef/core/modeling/value-conversions)
  - [EFCore.NamingConventions GitHub](https://github.com/efcore/EFCore.NamingConventions)
