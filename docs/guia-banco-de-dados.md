# Guia de banco de dados — uniplus-api

Referência canônica para devs que tocam persistência: naming convention, tipos PostgreSQL preferidos, soft delete, audit, value objects, migrations EF Core e troubleshooting.

> Decisões binding em [ADR-0054](adrs/0054-naming-convention-e-strategy-migrations.md). Aqui são as instruções operacionais.

## Sumário

1. [Topologia](#1-topologia)
2. [Naming convention](#2-naming-convention)
3. [Tipos PostgreSQL preferidos](#3-tipos-postgresql-preferidos)
4. [Soft delete](#4-soft-delete)
5. [Audit trail](#5-audit-trail)
6. [Value Objects](#6-value-objects)
7. [Workflow de migration](#7-workflow-de-migration)
8. [Forward-only revert](#8-forward-only-revert)
9. [Nomenclatura de migration](#9-nomenclatura-de-migration)
10. [Constraints e índices](#10-constraints-e-índices)
11. [FKs e cascade](#11-fks-e-cascade)
12. [Rodar local](#12-rodar-local)
13. [Inspecionar schema](#13-inspecionar-schema)
14. [Cifragem at-rest](#14-cifragem-at-rest)
15. [FAQ](#15-faq)
16. [Promoção de enum → entidade](#16-promoção-de-enum--entidade)

---

## 1. Topologia

Uni+ usa **5 bancos PostgreSQL 18 isolados** (um por módulo), no mesmo host PG mas isolados por database:

| Banco | Usuário | DbContext | Cobertura |
|---|---|---|---|
| `uniplus_selecao` | `uniplus` | `SelecaoDbContext` | Editais, Candidatos, Inscrições, Cotas, Etapas, ProcessosSeletivos |
| `uniplus_ingresso` | `uniplus` | `IngressoDbContext` | Chamadas, Convocações, Matrículas, DocumentosMatricula |
| `uniplus_portal` | `uniplus` | `PortalDbContext` | Vazio até primeira Story tocar entity Portal |
| `uniplus_parametrizacao` | `uniplus_parametrizacao_app` | `ParametrizacaoDbContext` (a criar — F1.S3) | Catálogos cross-cutting: Modalidade, NecessidadeEspecial, TipoDocumento, Endereco |
| `uniplus_organizacao` | `uniplus_organizacao_app` | `OrganizacaoInstitucionalDbContext` (a criar — F1.S2) | AreaOrganizacional |

Os bancos legados (Selecao/Ingresso/Portal) compartilham o superusuário `uniplus`. Os bancos da Sprint 3 (Parametrizacao/Organizacao) usam **usuários `_app` dedicados, cada um dono (`OWNER`) do seu próprio banco** — o owner tem DDL completo no schema `public` (necessário a partir do PG 15, que removeu o `CREATE` implícito) e instala extensões trusted sem superusuário. Provisionamento em `docker/init-db.sql`.

Extensões habilitadas:

- `uuid-ossp`, `pg_trgm` — em todos os bancos de aplicação.
- `btree_gist` — em `uniplus_selecao`, `uniplus_parametrizacao` e `uniplus_organizacao`: requerida pelos exclusion constraints GIST das junction tables de `AreasDeInteresse` ([ADR-0060](adrs/0060-junction-tables-por-entidade-com-view-unificada.md)). Em dev é habilitada via `init-db.sql`; em standalone/HML/PROD, via a primeira migration de cada DbContext (idempotente, `CREATE EXTENSION IF NOT EXISTS`).

Schemas usados em cada banco:

- `public` — domínio do módulo + `idempotency_cache` (compartilhada por Selecao via `Infrastructure.Core`).
- `wolverine.*` — outbox/envelopes/dead-letters do Wolverine (auto-provisionado via `AutoBuildMessageStorageOnStartup`, ver [ADR-0039](adrs/0039-provisioning-schema-wolverine-via-deploy.md)).

Migrations EF Core são **por DbContext** — cada banco tem seu `__EFMigrationsHistory` independente, sem coordenação cross-módulo.

## 2. Naming convention

Aplicada automaticamente via NuGet `EFCore.NamingConventions` + `UseSnakeCaseNamingConvention()` no helper `UseUniPlusNpgsqlConventions` (`Infrastructure.Core/Persistence/UniPlusDbContextOptionsExtensions.cs`). Cobre:

| Elemento | Regra | Exemplo |
|---|---|---|
| Tabela | `snake_case` plural pt-BR (definido via `ToTable("...")`) | `editais`, `inscricoes`, `processos_seletivos` |
| Coluna | `snake_case` automático (sem `HasColumnName`) | `created_at`, `numero_edital`, `nome_civil` |
| PK | `pk_<tabela>` | `pk_editais` |
| FK | `fk_<tabela>_<referenciada>_<coluna>` | `fk_etapas_editais_edital_id` |
| Índice | `ix_<tabela>_<coluna>` | `ix_inscricoes_candidato_id_edital_id` |
| Unique index | `ix_<tabela>_<coluna>` (com `IsUnique()`) | `ix_candidatos_cpf` |

**Regras práticas**:

- ✅ **Não usar `HasColumnName`** para mapear `CreatedAt` → `created_at`. A convention faz.
- ✅ Tabelas em `ToTable("editais")` (plural pt-BR) — a convention **não pluraliza**, então é manual.
- ❌ **Não criar identificadores `PascalCase` quoted** (`"CreatedAt"`) — quebra padrão e gera fricção em scripts manuais.

## 3. Tipos PostgreSQL preferidos

| Conceito C# | Tipo PG | Comentário |
|---|---|---|
| `Guid` (Id) | `uuid` | Sempre Guid v7 ([ADR-0032](adrs/0032-guid-v7-para-identidade-de-entidades.md)) |
| `DateTimeOffset` (audit) | `timestamp with time zone` | UTC always — interceptor garante |
| `string` (limite explícito) | `varchar(N)` | Sempre defina limite via `HasMaxLength` |
| `string` (texto longo) | `text` | Quando o conteúdo varia muito (>1000 chars) |
| `decimal` (dinheiro/nota) | `numeric(p,s)` | Sempre `HasPrecision(p, s)` explícito |
| Enum | `int4` | `HasConversion<int>()` por entidade |
| JSON estruturado | `jsonb` | Sempre `jsonb`, nunca `json` |
| Bool | `boolean` | EF padrão |
| `byte[]` | `bytea` | Para payloads cifrados (Idempotency, etc.) |

**Anti-patterns proibidos**:

- ❌ `char(N)` — Postgres faz padding com espaços; corrompe strings.
- ❌ `text` sem necessidade — sempre defina limite quando aplicável.
- ❌ `timestamp without time zone` — perde fuso, gera bugs em deploy multi-região.
- ❌ `numeric` sem precisão — fica com precisão padrão variável.

## 4. Soft delete

Padrão obrigatório em **todas as entidades de domínio** (herdam de `EntityBase`):

```
public abstract class EntityBase
{
    public Guid Id { get; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; }
    public bool IsDeleted { get; }
    public DateTimeOffset? DeletedAt { get; }
    public string? DeletedBy { get; }
    // …
}
```

Aplicado automaticamente via:

- `SoftDeleteInterceptor` (`Infrastructure.Core/Persistence/Interceptors/SoftDeleteInterceptor.cs`): converte `DELETE` em `UPDATE` (`is_deleted=true`, `deleted_at=NOW()`, `deleted_by=<user>`).
- `HasQueryFilter(e => !e.IsDeleted)` em cada `IEntityTypeConfiguration` — consultas LINQ ignoram registros soft-deletados automaticamente.

**Para acessar registros soft-deletados** (admin, audit, restore):

```
context.Editais.IgnoreQueryFilters().Where(e => e.IsDeleted).ToListAsync();
```

**Nunca remover fisicamente** registros de domínio — `Remove` no DbContext + SaveChanges aciona o interceptor, que converte para `UPDATE`. Hard delete só em casos LGPD (direito ao esquecimento) com Story dedicada e revisão de compliance.

## 5. Audit trail

`AuditableInterceptor` (`Infrastructure.Core/Persistence/Interceptors/AuditableInterceptor.cs`) popula automaticamente:

| Campo | Quando | Fonte |
|---|---|---|
| `created_at` | Insert | `DateTimeOffset.UtcNow` |
| `updated_at` | Insert + Update | `DateTimeOffset.UtcNow` |
| `created_by`, `updated_by` | Apenas se entity implementa `IAuditableEntity` | `IUserContext.UserId` (token JWT) |

**Para opt-in de `created_by`/`updated_by`** em uma entidade:

```csharp
public sealed class MinhaEntidade : EntityBase, IAuditableEntity
{
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    // …
}
```

Sem implementar `IAuditableEntity`, audit fica nos 4 campos default do `EntityBase` (`created_at`, `updated_at`, `is_deleted`, `deleted_at`, `deleted_by`).

## 6. Value Objects

**Padrão atual: `OwnsOne` para todos os VOs**. A migração para `ComplexProperty` (EF 10 nativo) está **diferida** em [#397](https://github.com/unifesspa-edu-br/uniplus-api/issues/397) por limitação técnica do EF 10 (`HasIndex` em ComplexProperty não suportado; necessário para Cpf — RN01).

### Quando criar VO

- Tipo de valor com **invariantes** (CPF validado, Email com regex, NotaFinal com precisão).
- **Imutável** (record).
- Factory `Criar()` retorna `Result<T>` (validação na construção).

### Mapeamento em Configurations

```csharp
// VO de 1 campo (Cpf, Email, NotaFinal, ProtocoloConvocacao):
builder.OwnsOne(c => c.Cpf, cpf =>
{
    cpf.Property(v => v.Valor).HasColumnName("cpf").HasMaxLength(11).IsRequired();
    cpf.HasIndex(v => v.Valor).IsUnique(); // unique constraint funciona em OwnsOne
});

// VO composto (NumeroEdital, PeriodoInscricao, FormulaCalculo, NomeSocial):
builder.OwnsOne(e => e.NumeroEdital, ne =>
{
    ne.Property(n => n.Numero).HasColumnName("numero_edital").IsRequired();
    ne.Property(n => n.Ano).HasColumnName("ano_edital").IsRequired();
});
```

**Por que `HasColumnName` aqui** (contradiz §2): convention default geraria `cpf_valor` (verbose). Para VOs 1-campo, manter o nome da coluna semântico (`cpf`, `email`, `protocolo`). VOs compostos seguem convention com prefixo (`numero_edital_numero`...).

### ValueObjectConventions

Existe `ValueObjectConventions.ConfigureValueObjectConverters()` (`Infrastructure.Core/Persistence/Converters/`) com 4 converters (`Cpf`, `Email`, `NomeSocial`, `NotaFinal`). **Não está ativada** porque é incompatível com `OwnsOne` para o mesmo tipo CLR. Fica disponível para o cutover futuro (#397) ou para uso pontual em entity onde VO seja primitive property.

## 7. Workflow de migration

### Cenário: Story altera entity (campo novo, índice novo, FK nova)

```bash
# 1. Editar a entity em src/<modulo>/Unifesspa.UniPlus.<Modulo>.Domain/Entities/
# 2. Editar a Configuration em src/<modulo>/Unifesspa.UniPlus.<Modulo>.Infrastructure/Persistence/Configurations/
# 3. Gerar migration:
dotnet ef migrations add Adiciona<Coisa> \
  --project src/<modulo>/Unifesspa.UniPlus.<Modulo>.Infrastructure \
  --output-dir Persistence/Migrations \
  --context <Modulo>DbContext

# 4. Revisar SQL gerado (Up/Down + indexes + constraints)
# 5. Build + testes (suite integração local)
dotnet build UniPlus.slnx
dotnet test tests/Unifesspa.UniPlus.<Modulo>.IntegrationTests

# 6. Commit migration + snapshot + Configuration juntos no MESMO PR
git add src/<modulo>/Unifesspa.UniPlus.<Modulo>.Infrastructure/Persistence/Migrations/
git add src/<modulo>/Unifesspa.UniPlus.<Modulo>.Infrastructure/Persistence/Configurations/
git commit -m "feat(<modulo>): adiciona <coisa>"
```

### Em produção / standalone

`MigrationHostedService<TContext>` aplica a migration no `StartAsync` do host, antes do `WolverineRuntime` iniciar (ordem garantida por fitness test — ver [ADR-0039](adrs/0039-provisioning-schema-wolverine-via-deploy.md) e [#419](https://github.com/unifesspa-edu-br/uniplus-api/issues/419)). Pod restart → migration aplicada → app pronto.

### Bug atual do EF tool (.NET 10)

`dotnet ef migrations add` falha com `FileNotFoundException: System.Runtime, Version=10.0.0.0` no SDK 10.0.104 + dotnet-ef 10.0.4/10.0.5/10.0.7 (testado). Workaround temporário:

- Escrever migration manualmente (espelhando o Up/Down de migrations existentes).
- Ou rodar `dotnet ef` em container `mcr.microsoft.com/dotnet/sdk:10.0` montando o repo.
- Acompanhar [dotnet/efcore](https://github.com/dotnet/efcore/issues) por release que corrija.

**Convention snake_case está preparada mas não ligada no runtime** ([ADR-0054](adrs/0054-naming-convention-e-strategy-migrations.md)) — espera Story de normalização do schema (regenerar `InitialCreate` ou migration `NormalizaAuditColumnsParaSnakeCase`) para ser ativada via `UseSnakeCaseNamingConvention()` no helper. Design-time factories já invocam a convention para que migrations geradas saiam corretas.

## 8. Forward-only revert

`Down()` em migrations é **proibido em produção**. Política:

- Cada migration nova é **adição forward**. Schema só evolui pra frente.
- Para reverter uma mudança ruim: nova migration `Reverte<X>` ou `Remove<X>` com `Up()` que desfaz, `Down()` vazio ou `throw new InvalidOperationException("Migrations são forward-only — ver guia de banco de dados.")`.
- O `__EFMigrationsHistory` preserva o histórico completo (audit).

**Em dev local**, está OK rodar `dotnet ef database update <NomeAnterior>` para iterar localmente. Não acontece em prod.

## 9. Nomenclatura de migration

Padrão: `{timestamp}_{Verbo}{Objeto}.cs` (EF gera o timestamp; você escreve o resto).

Verbos pt-BR em **indicativo presente** (alinhado com conventional commits):

- ✅ `Adiciona<Coisa>` — `AdicionaCampoBonus`, `AdicionaTabelaDocumento`
- ✅ `Remove<Coisa>` — `RemoveColunaObsoleta`
- ✅ `Renomeia<Coisa>` — `RenomeiaCandidatosParaInscritos`
- ✅ `Cria<Coisa>` — `CriaIndiceComposto` (uso raro; prefira "Adiciona" mais comum)
- ✅ `Corrige<Coisa>` — `CorrigeCardinalidadeMatricula`
- ✅ `Promove<Coisa>` — para migrations que promovem schema entre módulos
- ❌ `Migration1`, `Update1`, `Adicionar` (infinitivo), `AddIndex` (inglês) — proibidos.

## 10. Constraints e índices

### Quando criar índice

- ✅ Coluna usada em `WHERE` recorrente (>10% dos queries do endpoint).
- ✅ FK (índice automático recomendado para JOIN).
- ✅ Unique constraint (`IsUnique()`).
- ❌ Coluna usada apenas em `INSERT`/`UPDATE` sem `WHERE` por ela.

### Sintaxe

```csharp
// Index simples
builder.HasIndex(i => i.NumeroInscricao).IsUnique();

// Index composto
builder.HasIndex(i => new { i.CandidatoId, i.EditalId });

// Em OwnsOne sub-prop
builder.OwnsOne(c => c.Cpf, cpf =>
{
    cpf.Property(v => v.Valor).HasColumnName("cpf");
    cpf.HasIndex(v => v.Valor).IsUnique();
});
```

### Migration SQL raw (quando EF não basta)

Se precisar de índice especial (parcial, GIN sobre jsonb, etc.):

```csharp
migrationBuilder.Sql(@"
    CREATE INDEX ix_editais_metadata_gin
    ON editais USING GIN (metadata jsonb_path_ops);
");
```

## 11. FKs e cascade

| Cenário | `OnDelete` | Justificativa |
|---|---|---|
| Aggregate root → child (Edital → Etapa) | `Cascade` | Deletar agregado leva os children junto |
| Cross-aggregate (Inscricao → Candidato) | `Restrict` (default) | Não deletar agregado se há referência |
| Audit/log fields | `SetNull` ou `Restrict` | Preservar audit mesmo após delete |

**Lembre que soft delete é o padrão** — `Cascade` físico raramente é exercido. `HasQueryFilter` esconde soft-deletados em consultas, mas a integridade referencial física ainda vale.

## 12. Rodar local

### PostgreSQL via docker compose

```bash
# do root do uniplus-api/
docker compose -f docker/docker-compose.yml up postgres -d

# Bancos criados automaticamente via docker/init-db.sql:
# uniplus_selecao, uniplus_ingresso, uniplus_portal (usuário uniplus)
# uniplus_parametrizacao, uniplus_organizacao (usuários _app dedicados)
```

### Aplicar migrations local

Não é necessário aplicar manualmente — o `MigrationHostedService` do host aplica no `StartAsync` da API. Apenas:

```bash
docker compose -f docker/docker-compose.yml -f docker/docker-compose.override.yml up -d
```

Sobe APIs + Postgres + Kafka + Redis + MinIO + Keycloak.

### Reset local

```bash
docker compose down -v        # apaga volumes (perde dados)
docker compose up postgres -d # recria bancos vazios; APIs aplicam migrations no próximo start
```

## 13. Inspecionar schema

```bash
# Conectar via psql
docker compose exec postgres psql -U postgres -d uniplus_selecao

# Listar tabelas
\dt

# Inspecionar tabela
\d+ editais

# Listar indices
\di

# Listar FKs
SELECT conname, conrelid::regclass, confrelid::regclass
  FROM pg_constraint WHERE contype = 'f';

# Conferir migrations aplicadas
SELECT * FROM "__EFMigrationsHistory";
```

## 14. Cifragem at-rest

Dados sensíveis (Idempotency-Key requests, PII) são cifrados **antes** de gravar no banco via `IUniPlusEncryptionService` (Vault Transit em prod/standalone, AES-GCM local em dev).

- **Path canônico**: `Idempotency-Key` ([ADR-0027](adrs/0027-idempotency-key-opt-in-com-store-postgresql.md)).
- **Não criar criptografia ad-hoc** em entity — use `IUniPlusEncryptionService` se a Story exigir.
- **Não cifrar campos como PK/FK** — quebra índices.
- **CPF e demais PII em logs**: mascarados via `PiiMaskingEnricher` do Serilog (regra de domínio, não banco).

## 15. FAQ

### Preciso `ALTER COLUMN` para mudar tipo?

Sim, via migration. Se for mudança breaking (ex.: `varchar(50)` → `varchar(10)` com truncamento), gere a migration com `Sql()` raw para validação prévia + cópia controlada:

```csharp
migrationBuilder.Sql(@"
    UPDATE editais SET codigo = LEFT(codigo, 10) WHERE LENGTH(codigo) > 10;
");
migrationBuilder.AlterColumn<string>("codigo", "editais", maxLength: 10);
```

### Como adiciono enum value?

C# enum value novo + nova migration que faz `ALTER TYPE` (se for PG enum) ou simplesmente adiciona o int na tabela (se usar `HasConversion<int>`). Para `HasConversion<int>` (padrão Uni+), basta adicionar o valor no enum C# — sem migration SQL.

### Como mudo tipo de coluna sem perder dados?

3 migrations:

1. **Adiciona coluna nova** (`coluna_novo`) com o tipo desejado.
2. **Copia dados** via `Sql("UPDATE … SET coluna_novo = coluna_old::novo_tipo")`.
3. **Remove coluna antiga** e renomeia `coluna_novo` → `coluna_old`.

Forward-only. Cada passo committed separadamente para permitir rollback parcial via outra migration de adição.

### Posso indexar uma subpropriedade de VO?

- Com `OwnsOne`: ✅ sim — `cpf.HasIndex(v => v.Valor).IsUnique()`.
- Com `ComplexProperty` (EF 10): ❌ ainda não — usar SQL raw via `migrationBuilder.Sql("CREATE UNIQUE INDEX ...")`.

### Quando criar um IDesignTimeDbContextFactory?

Já existe um por DbContext (`<Modulo>DbContextDesignTimeFactory` em cada `Infrastructure/Persistence/`). Usado pelo `dotnet ef` CLI; não precisa criar novo a menos que esteja adicionando um DbContext novo.

### Como restauro um registro soft-deleted?

```csharp
var entity = await context.Editais
    .IgnoreQueryFilters()
    .FirstOrDefaultAsync(e => e.Id == id);

if (entity is not null && entity.IsDeleted)
{
    entity.Restaurar(); // método no domínio que zera IsDeleted/DeletedAt/DeletedBy
    await context.SaveChangesAsync();
}
```

Confira se a entidade expõe método de restauração (não é padrão — implementar caso-a-caso).

### Onde vejo o schema atual em produção/standalone?

Procedimentos de cluster (SSH, `psql` no host, troubleshooting) vivem no repositório `uniplus-infra`, fora deste repo. Consulte RUNBOOKS lá ou peça acesso ao SRE/DevOps.

## 16. Promoção de enum → entidade

Quando um enum modelado como `int4` em coluna de tabela (`HasConversion<int>`) precisa virar uma **entidade plena** (com aggregate próprio, código strongly-typed, atributos extensíveis), o cutover é feito em **três Stories sequenciais** para evitar schema drift entre o domain model e o banco. O padrão foi consolidado na promoção `TipoProcesso → TipoEdital` (Stories #454 → #455) e vale para qualquer enum futuro que ganhar status de entidade.

### Etapa 1 — Migration preparatória (drop + add FK NULL)

Story de schema-only que **dropa a coluna `<nome>_<enum>` (`int`)** e **adiciona uma coluna FK preparatória `<nome>_id uuid NULL`**, sem `HasOne` configurado e sem constraint.

- A propriedade na entidade vira `Guid? <Nome>Id { get; private set; }`.
- Configurations EF removem o `HasConversion<int>()`; a propriedade passa a ser mapeada nativamente como `uuid` nullable (snake_case automático via `EFCore.NamingConventions`).
- Migration `Up()` executa `DropColumn` + `AddColumn`. `Down()` lança `NotSupportedException("Forward-only migration per ADR-0054 §J.")`.
- Command/Validator: parâmetro `<Nome>Id` opcional e nullable; validator rejeita `Guid.Empty` quando informado.
- DTOs e schemas OpenAPI são regerados — a baseline `contracts/openapi.<modulo>.json` muda neste passo (`UPDATE_OPENAPI_BASELINE=1 dotnet test --filter SpecRuntime`).

Referência: [Story #454](https://github.com/unifesspa-edu-br/uniplus-api/issues/454) — migration `DropEnumColumnsPrePromotion` em `SelecaoDbContext`.

### Etapa 2 — Criação da entidade + seed Newman

Story que **cria o agregado** (`<Nome>` em `Selecao.Domain.Entities`) com todos os atributos planejados, incluindo `Codigo` strongly-typed (substitui o enum), `Descricao`, audit fields. Configurações EF, repositório, command de criação e seed via Newman (CSV/JSON committed sob `seeds/`).

- A coluna FK introduzida na Etapa 1 ainda é `NULL`able. Adiciona `HasOne(e => e.<Nome>).WithMany().HasForeignKey(e => e.<Nome>Id)` em Configuration.
- Enum legado (`Selecao.Domain.Enums.<EnumLegado>`) é **removido nesta etapa** — não na Etapa 1 — para evitar refactor amplo enquanto schema migration ainda está em vôo.
- Seed via Newman popula linhas-template em todos os ambientes (dev local, CI, HML, prod) — o CD não cria entries em runtime.

Referência: [Story #455](https://github.com/unifesspa-edu-br/uniplus-api/issues/455) — promove `TipoProcesso → TipoEdital` entidade.

### Etapa 3 — Constraint NOT NULL

Story de schema-only **separada**, executada quando dados de produção foram migrados (via script ou seed) para que toda linha tenha `<nome>_id` preenchido. Migration `AlterColumn` muda a coluna de `nullable: true` para `nullable: false`.

- Pré-requisito: query auditiva em prod (`SELECT COUNT(*) WHERE <nome>_id IS NULL`) retorna zero.
- Sem essa garantia, `AlterColumn` falha com `23502 not_null_violation` no `MigrationHostedService` no `StartAsync` do pod, derrubando o rollout.

### Por quê três Stories e não uma só

- **Risco de rollback**: três migrations forward-only permitem revert via nova migration parcial (ex.: voltar Etapa 3 sem perder Etapa 2).
- **PR review focado**: cada Story tem um único concern (schema, domain, constraint), reduzindo superfície de revisão.
- **CI testável incrementalmente**: a Etapa 1 já fica verde no CI antes de a entidade existir, permitindo merge sem aguardar #455.

### Anti-patterns proibidos

- ❌ **Drop + create entidade na mesma migration**: PR fica grande, qualquer issue de domain/EF reverte schema também. Aumenta risco de coluna órfã ou data loss em rollout parcial.
- ❌ **NOT NULL na Etapa 1**: dados não existem ainda — a coluna nasce `NULL` por design. Tentar `nullable: false` no DropColumn+AddColumn quebra inserts existentes em qualquer ambiente já com dados.
- ❌ **Manter o enum em `Domain.Enums` após Etapa 2**: causa risco de devs continuarem instanciando `<Nome>Id` a partir do enum legado, gerando GUIDs hardcoded sem entrada no agregado real.
