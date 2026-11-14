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
8. [Migration regerada, não sucessora](#8-migration-regerada-não-sucessora)
9. [Nome da migration](#9-nome-da-migration)
10. [Constraints e índices](#10-constraints-e-índices)
11. [FKs e cascade](#11-fks-e-cascade)
12. [Rodar local](#12-rodar-local)
13. [Inspecionar schema](#13-inspecionar-schema)
14. [Cifragem at-rest](#14-cifragem-at-rest)
15. [FAQ](#15-faq)
16. [Promoção de enum → entidade](#16-promoção-de-enum--entidade)

---

## 1. Topologia

Uni+ usa um **banco único `uniplus`** com **schema por módulo** ([ADR-0097](adrs/0097-topologia-de-deploy-em-tres-apis-monolito-modular.md)). Os módulos internos coabitam a API UniPlus num processo único e compartilham **um só contexto** (`MonolitoDbContext`, [ADR-0170](adrs/0170-contexto-de-persistencia-unico-para-os-modulos-do-monolito.md)): cada módulo contribui com as suas configurações e o esquema dos seus tipos, e há uma só connection string (`UniPlusDb`).

| Schema | Contribuição | Cobertura |
|---|---|---|
| `selecao` | `SelecaoContribuicao` | Processos seletivos, Editais, Candidatos, Inscrições, Cotas, Etapas |
| `ingresso` | `IngressoContribuicao` | Chamadas, Convocações, Matrículas, DocumentosMatricula |
| `configuracao` | `ConfiguracaoContribuicao` | Catálogos cross-cutting: Modalidade, NecessidadeEspecial, TipoDocumento, Endereco |
| `organizacao` | `OrganizacaoInstitucionalContribuicao` | Unidade, Instituição |
| `publicacoes` | `PublicacoesContribuicao` | Registro central dos atos normativos ([ADR-0105](adrs/0105-modulo-publicacoes-registro-central-dos-atos.md)). Nasce vazio |
| `wolverine` | — | Outbox/envelopes/dead-letters, uma única instância ([ADR-0039](adrs/0039-provisioning-schema-wolverine-via-deploy.md)) |

**Geo** (repositório dedicado, `uniplus_geo` com PostGIS) e **Portal** (`uniplus_portal`) permanecem deployables autônomos, com banco próprio.

> ⚠️ **A ausência de chave estrangeira cross-módulo deixou de ser física.** Com bancos separados, uma chave estrangeira não atravessava banco — o motor garantia o isolamento. Com schema por módulo, `REFERENCES selecao.processos_seletivos(id)` a partir de outro schema **funciona**. A referência cross-módulo continua sendo **por valor** ([ADR-0061](adrs/0061-referencia-cross-modulo-via-snapshot-copy.md)), mas agora quem a defende é o modelo, não o PostgreSQL. O teste de integração `IsolamentoCrossSchemaTests` planta um canário — cria a chave estrangeira cross-schema, exige que a consulta de detecção a encontre, remove-a — e só então assere que nenhuma existe.

Extensões habilitadas:

- `uuid-ossp`, `pg_trgm` — em todos os bancos de aplicação.
- `btree_gist` — pré-requisito das exclusion constraints GIST sobre intervalos temporais ([ADR-0060](adrs/0060-junction-tables-por-entidade-com-view-unificada.md)) e sobre a ordem dos nós da árvore de exigências. Existe **uma vez só**, no script compartilhado `Infrastructure.Core/Persistence/Scripts/Pre/001_extensao_btree_gist.sql` (idempotente, `CREATE EXTENSION IF NOT EXISTS`), que roda antes de tudo e antes dos scripts dos módulos. Os containers efêmeros dos testes de integração não executam o `init-db`, então esse script é a única fonte. A aplicação viva é a constraint `ex_tipo_ato_publicado_codigo_vigencia`, no schema `publicacoes`, que impede duas versões vivas do mesmo tipo de ato de valerem no mesmo dia. Como a constraint vive em script SQL e não no modelo do EF, `TipoAtoPublicadoPersistenceTests` consulta o `pg_constraint` para garantir que ela existe.
- `unaccent` e `pg_trgm` — busca sem acento e por proximidade; a primeira vive no script `Pre` da Organização, a segunda é declarada no modelo (`HasPostgresExtension`).

Há **uma migration**, `Inicial`, do `MonolitoDbContext`, e **um histórico** (`plataforma.__EFMigrationsHistory`). O que o EF não representa vive em scripts SQL idempotentes dentro de cada módulo, e a subida roda: trava consultiva do banco → scripts `Pre` → `MigrateAsync` → scripts pós → etapa posterior de cada módulo (§ 7). A subida roda antes do runtime Wolverine (invariante travada por `MigrationBeforeWolverineRuntimeOrderTests`).

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

**Padrão atual: `OwnsOne` para todos os VOs**. A migração para `ComplexProperty` (EF 10 nativo) está **diferida** em [#397](https://github.com/unifesspa-edu-br/uniplus-api/issues/397) por limitação técnica do EF 10 (`HasIndex` em ComplexProperty não suportado; necessário para Cpf — inscrição única por CPF e nível, UNI-REQ-0031).

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

Existe `ValueObjectConventions.ConfigureValueObjectConverters()` (`Infrastructure.Core/Persistence/Converters/`) com 4 converters (`Cpf`, `Email`, `NomeSocial`, `NotaFinal`). **Não está ativada** porque é incompatível com `OwnsOne` para o mesmo tipo CLR. Fica disponível para a transição futura (#397) ou para uso pontual em entity onde VO seja primitive property.

## 7. Workflow de migration

### Cenário: Story altera entity (campo novo, índice novo, FK nova)

```bash
# 1. Editar a entity em src/<modulo>/Unifesspa.UniPlus.<Modulo>.Domain/Entities/
# 2. Editar a Configuration em src/<modulo>/Unifesspa.UniPlus.<Modulo>.Infrastructure/Persistence/Configurations/
# 3. Apagar a migration e o snapshot da pasta Migrations do projeto de composição e recompilar
#    (o `--no-build` lê o assembly antigo e ainda "vê" a migration apagada):
rm src/host/Unifesspa.UniPlus.Monolito.Persistencia/Migrations/*
dotnet build src/host/Unifesspa.UniPlus.Host -c Release

# 4. Gerar a migration de novo (--startup-project aponta para o Host — ver nota abaixo):
dotnet ef migrations add Inicial \
  --project src/host/Unifesspa.UniPlus.Monolito.Persistencia \
  --startup-project src/host/Unifesspa.UniPlus.Host \
  --context MonolitoDbContext \
  --output-dir Migrations

# 5. Recompilar e conferir que o modelo não tem mudança pendente:
dotnet build src/host/Unifesspa.UniPlus.Host -c Release
dotnet ef migrations has-pending-model-changes \
  --project src/host/Unifesspa.UniPlus.Monolito.Persistencia \
  --startup-project src/host/Unifesspa.UniPlus.Host \
  --context MonolitoDbContext --no-build -c Release

# 6. Testes de integração dos módulos afetados (precisam de Docker)
dotnet test tests/Unifesspa.UniPlus.<Modulo>.IntegrationTests

# 7. Commit migration + snapshot + Configuration juntos no MESMO PR
```

**Ao rebasear**, descarte a migration própria (volte `Migrations/` ao estado da `main`) e gere outra do zero: o snapshot é um só e concentra os conflitos entre branches. O `has-pending-model-changes` do CI recusa o PR em que o modelo mudou e a migration não foi regerada.

### O que o EF não representa: scripts SQL por módulo

Extensões, funções, gatilhos, exclusion constraints, índices de expressão e sementes por SQL ficam em `Persistence/Scripts/` do `{Modulo}.Infrastructure`:

- `Persistence/Scripts/Pre/NNN_assunto.sql` roda **antes** da migration (o que a migration precisa já existir: extensão, função usada em coluna gerada);
- `Persistence/Scripts/NNN_assunto.sql` roda **depois** dela (o que referencia tabela: constraint, gatilho, índice de expressão, semente).

Os arquivos são recursos embutidos (`<EmbeddedResource Include="Persistence\Scripts\**\*.sql" />` no `.csproj`), aplicados em ordem ordinal do nome, **cada um numa transação própria**, e **precisam ser idempotentes**: `CREATE ... IF NOT EXISTS`, `CREATE OR REPLACE`, `DROP ... IF EXISTS` antes de `ADD`, `INSERT ... ON CONFLICT DO NOTHING`. Reaplicar é no-op.

Os objetos compartilhados existem **uma vez só**, no conjunto `Infrastructure.Core/Persistence/Scripts/Pre`, que roda primeiro: `btree_gist` e a função `plataforma.normalizar_para_comparacao`. **Script de um módulo não referencia o esquema de outro.** Sementes por código (as que dependem de regra da aplicação) ficam na etapa posterior do módulo (`IContribuicaoDoModulo.ExecutarEtapaPosteriorDaMigrationAsync`), depois dos scripts pós.

Para subir um banco de teste por inteiro (scripts `Pre`, migration e scripts pós), as fixtures chamam `UniPlusDbContext.SubirOBancoAsync()`.

### Em produção / standalone

`MigrationHostedService<MonolitoDbContext>` sobe o banco no `StartAsync` do host (trava, scripts `Pre`, migration, scripts pós, etapas posteriores), antes do `WolverineRuntime` iniciar (ordem garantida por fitness test — ver [ADR-0039](adrs/0039-provisioning-schema-wolverine-via-deploy.md) e [#419](https://github.com/unifesspa-edu-br/uniplus-api/issues/419)). Pod restart → banco subido → app pronto. A trava consultiva (`pg_advisory_lock`) serializa réplicas que subam ao mesmo tempo.

### `dotnet ef` exige `--startup-project` apontando para o Host

Sem `--startup-project`, o `dotnet ef` falha com `FileNotFoundException` em assemblies de runtime versão `10.0.0.0` (`System.Runtime`, `Microsoft.Extensions.DependencyInjection.Abstractions`, `Microsoft.AspNetCore.Http.Abstractions`, conforme o comando). **Não é bug do EF tool nem do .NET 10** — o design-time factory do contexto (`MonolitoDbContextDesignTimeFactory`) resolve dependências que só existem num projeto executável com `FrameworkReference Microsoft.AspNetCore.App` e `runtimeconfig.json`. Rodar só com `--project ...Infrastructure` executa o tool num contexto sem esse framework, e as assemblies não resolvem.

**Desde o [ADR-0097](adrs/0097-topologia-de-deploy-em-tres-apis-monolito-modular.md), os `.Infrastructure`/`.API` dos módulos internos (Selecao, Ingresso, Configuracao, OrganizacaoInstitucional, Publicacoes, Autorizacao, Discentes) são class libraries puras — sem `Program.cs` nem `runtimeconfig.json`.** `src/host/Unifesspa.UniPlus.Host` é o único executável com o setup design-time do EF para esses módulos; **sempre passar `--startup-project src/host/Unifesspa.UniPlus.Host`** em qualquer comando `dotnet ef` (`migrations add`, `migrations list`, `migrations has-pending-model-changes`, `dbcontext info`, etc.) para essa família de módulos. **Geo (repositório dedicado) e Portal são exceção**: são *deployables* autônomos com `Program.cs` próprio — para eles, `--startup-project` aponta para o próprio projeto (`src/portal/Unifesspa.UniPlus.Portal.API`, por exemplo), não para o Host.

Com isso roda no host, sem container e sem workaround. Verificação útil de que o snapshot está consistente com o modelo:

```bash
dotnet ef migrations has-pending-model-changes \
  --project src/host/Unifesspa.UniPlus.Monolito.Persistencia \
  --startup-project src/host/Unifesspa.UniPlus.Host \
  --context MonolitoDbContext
# Esperado: "No changes have been made to the model since the last migration."
```

> `has-pending-model-changes` opera sobre o modelo vs. snapshot e **não acessa o banco** — não exige Postgres rodando. Já `dotnet ef migrations list` tenta conectar ao banco design-time (`Host=design-time-stub`) para exibir o status de aplicação de cada migration; passe **`--no-connect`** para listar só as migrations do código sem essa tentativa. Sem a flag, a falha de conexão é inofensiva (as migrations ainda são listadas, apenas o "pending status" fica indisponível), mas `--no-connect` é o caminho limpo.

**Convention snake_case ativa no runtime e no design-time** ([ADR-0054](adrs/0054-naming-convention-e-strategy-migrations.md)). Tanto `UseUniPlusNpgsqlConventions` (helper de runtime consumido por `AddXxxInfrastructure`) quanto `BuildDesignTimeOptions` chamam `UseSnakeCaseNamingConvention()`, então tabelas, colunas, índices e FKs ficam em snake_case e as migrations geradas batem com o modelo em runtime (`has-pending-model-changes` retorna "No changes"). O nome de `__EFMigrationsHistory` é pinado para não sofrer a convention, evitando drift em rollouts mistos com pods sem ela.

### Quem aplica a migration no deploy

O papel do processo é declarado por `UniPlus:Migrations:Mode` (ADR-0127):

| Valor | Papel | Onde é usado |
|---|---|---|
| ausente ou `OnStartup` | aplica no boot do host | default; ambiente que não declara nada |
| `ApplyAndExit` | aplica e encerra, sem servir requisições | Job de deploy |
| `Skip` | não aplica | pod, quando o Job já cuidou |

Em `ApplyAndExit` o processo devolve `0` se aplicou, `1` se a migration falhou e `2` se não havia
contexto algum registrado — é por esse código que o Job de deploy decide se o rollout prossegue.
Os dois erros são separados de propósito: o primeiro é schema ou dado, o segundo é o Job montado
errado, e confundi-los custaria tempo no pior momento. Só os hosted services de migration são
executados: mensageria e pipeline HTTP não chegam a iniciar.

Valor fora do domínio é recusado no boot, com a lista dos aceitos. Não há default silencioso, de
propósito: um pod destinado a `Skip` que caísse em `OnStartup` aplicaria migration por conta
própria, anulando a separação.

O Job cobre também o schema do **Wolverine**: as tabelas do outbox são provisionadas pelo próprio
framework no start do runtime, e não pelas migrations EF. Em `ApplyAndExit` elas entram junto; em
`Skip` o provisionamento automático fica desligado no pod. Cobrir só o EF deixaria o DDL do outbox
acontecendo depois do rollout — o mesmo problema, por outra porta.

**O que isso não resolve:** o Job garante que uma migration quebrada falhe *antes* de qualquer pod
ser tocado. Não garante que o schema novo seja compatível com a versão anterior da aplicação —
para migration destrutiva promovida sem indisponibilidade, a resposta continua sendo expandir e
contrair em duas releases.

## 8. Migration regerada, não sucessora

Sem produção com dado real, não há histórico a preservar: a `Inicial` é **gerada de novo** a cada mudança de modelo, e nenhuma migration sucessora acompanha a mudança. Política:

- O que o modelo não declara não existe no banco: default de coluna, índice e constraint só nascem do modelo (migration) ou de um script do módulo.
- `Down()` não é usado. Para reverter uma mudança, reverta o modelo e gere a migration de novo.
- O `plataforma.__EFMigrationsHistory` tem uma linha só.
- Quando houver um banco com dado real a preservar, esta política muda por ADR, e as migrations voltam a ser sucessivas.

## 9. Nome da migration

A migration única chama-se **`Inicial`**, em `src/host/Unifesspa.UniPlus.Monolito.Persistencia/Migrations`. O nome do script SQL segue `NNN_assunto_no_indicativo_ou_substantivo.sql` (`001_vigencia_unica_do_calendario.sql`): o número ordena a aplicação dentro do módulo, e o assunto diz o que o script garante.

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

### Script SQL (quando o EF não basta)

Se precisar de índice especial (parcial, GIN sobre jsonb, de expressão) ou de qualquer objeto que o EF não modela, escreva um script idempotente em `Persistence/Scripts/` do módulo, nunca `migrationBuilder.Sql` à mão:

```sql
-- Persistence/Scripts/001_busca_por_metadados_do_edital.sql
CREATE INDEX IF NOT EXISTS ix_editais_metadata_gin
    ON selecao.editais USING GIN (metadata jsonb_path_ops);
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
# uniplus_configuracao, uniplus_organizacao (usuários _app dedicados)
```

### Aplicar a migration local

Não é necessário aplicar manualmente — o `MigrationHostedService` do host sobe o banco (scripts, migration, etapas) no `StartAsync` da API. Apenas:

```bash
docker compose -f docker/docker-compose.yml -f docker/docker-compose.override.yml up -d
```

Sobe APIs + Postgres + Kafka + Redis + MinIO + Keycloak.

### Reset local

```bash
docker compose down -v        # apaga volumes (perde dados)
docker compose up postgres -d # recria bancos vazios; a API sobe o banco no próximo start
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

# Conferir a migration aplicada
SELECT * FROM plataforma."__EFMigrationsHistory";
```

## 14. Cifragem at-rest

Dados sensíveis (Idempotency-Key requests, PII) são cifrados **antes** de gravar no banco via `IUniPlusEncryptionService` (Vault Transit em prod/standalone, AES-GCM local em dev).

- **Path canônico**: `Idempotency-Key` ([ADR-0027](adrs/0027-idempotency-key-store-postgresql.md)).
- **Não criar criptografia ad-hoc** em entity — use `IUniPlusEncryptionService` se a Story exigir.
- **Não cifrar campos como PK/FK** — quebra índices.
- **CPF e demais PII em logs**: mascarados via `PiiMaskingEnricher` do Serilog (regra de domínio, não banco).

## 15. FAQ

### Preciso `ALTER COLUMN` para mudar tipo?

Não: mude o modelo (a Configuration) e gere a migration `Inicial` de novo. Não há dado real a preservar, então não há `UPDATE` de validação nem cópia controlada.

### Como adiciono enum value?

C# enum value novo. Para `HasConversion<int>` (padrão Uni+), basta adicionar o valor no enum C#: o banco não muda. Se for PG enum, mude o modelo e gere a migration de novo.

### Como mudo tipo de coluna?

Mude a propriedade na Configuration e gere a migration `Inicial` de novo.

### Posso indexar uma subpropriedade de VO?

- Com `OwnsOne`: ✅ sim — `cpf.HasIndex(v => v.Valor).IsUnique()`.
- Com `ComplexProperty` (EF 10): ❌ ainda não — usar um script SQL do módulo com `CREATE UNIQUE INDEX IF NOT EXISTS ...`.

### Quando criar um IDesignTimeDbContextFactory?

Já existe um só, `MonolitoDbContextDesignTimeFactory`, no projeto de composição `Monolito.Persistencia`. Usado pelo `dotnet ef` CLI; o Portal tem o dele, e um módulo novo do monólito contribui com o contexto único em vez de criar outro.

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

Quando um enum modelado como `int4` em coluna de tabela (`HasConversion<int>`) precisa virar uma **entidade plena** (com aggregate próprio, código strongly-typed, atributos extensíveis), a mudança de schema é **uma mudança de modelo só**: a coluna `<nome>_<enum>` (`int`) dá lugar à FK `<nome>_id uuid`, e a migration `Inicial` é gerada de novo. Não há etapas de migration preparatória nem constraint `NOT NULL` posterior, porque não há dado a migrar.

- A entidade nasce com `Codigo` strongly-typed (substitui o enum), `Descricao` e audit fields; o enum legado é removido na mesma mudança.
- DTOs e schemas OpenAPI são regerados — a baseline `contracts/openapi.<modulo>.json` muda (`UPDATE_OPENAPI_BASELINE=1 dotnet test --filter SpecRuntime`).
- **O caminho da carga depende da natureza do dado**, per [ADR-0062](adrs/0062-seed-de-catalogos-via-newman-e-endpoints-admin.md) Emenda 2. Promoção de enum para entidade cai quase sempre no primeiro caso, porque o que era enum é vocabulário:

| Natureza | Caminho | Autor |
|---|---|---|
| vocabulário normativo — ninguém o escolheu | `HasData` no modelo (entra na migration) ou script SQL do módulo com `INSERT … ON CONFLICT DO NOTHING`, ou semente por código na etapa posterior | `created_by` nulo, honesto |
| dado que a instituição administra | endpoints admin, via Newman (CSV/JSON sob `seeds/`) | `sub` real do JWT |

- **Não usar os dois** para o mesmo vocabulário: numa base recém-subida, a collection Newman faria `POST` de códigos que a semente já inseriu, recebendo conflito onde espera 200/201 — e o passo pós-deploy falha.
- Escolhida a semente num catálogo **administrável**, ela só acrescenta (`INSERT … ON CONFLICT DO NOTHING`): nunca sobrescreve a edição do operador nem apaga a linha fisicamente, sem o soft-delete auditado da API.
- **Item semeado que nasceu errado** se corrige no modelo ou no script, e a migration é gerada de novo; num banco que já tem a linha, a semente não a sobrescreve (a correção é ato administrativo, com autor).
- **Ausência de CRUD não basta:** havendo regime de congelamento, ele vem antes. `RegraCatalogo` é seed-governado e somente-leitura, mas a [ADR-0112](adrs/0112-fronteira-append-only-do-catalogo-de-regras.md) só admite substituir enquanto nenhuma `VersaoConfiguracao` referenciar a entrada. O caminho de escrita responde *quem* altera; o congelamento responde *se* ainda pode.

### Anti-patterns proibidos

- ❌ **Manter o enum em `Domain.Enums` depois da promoção**: causa risco de devs continuarem instanciando `<Nome>Id` a partir do enum legado, gerando GUIDs hardcoded sem entrada no agregado real.
- ❌ **Semear o mesmo vocabulário por semente e por Newman.**
