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
- [x] **Schema-por-módulo VALIDADO nos 4 módulos** (Configuracao `18daddd`, Organizacao `b736c14`, Selecao, Ingresso): `HasDefaultSchema` + schema na DI/design-time + migrations regeneradas (squash no schema) + SQL cru de teste/funcional qualificado. `has-pending` limpo por contexto; **integração verde (62 + 47 + 98) + ArchTests 24/24** (R8 e ordem migrations→Wolverine intactos).

## A fazer

### P2 — schema-por-módulo ✅ (mecanismo validado nos 4 módulos)
- [x] **Configuracao**: validado (`18daddd`) — 62 testes integração.
- [x] **Organizacao**: validado (`b736c14`) — 47 testes integração; SQL cru preservado (`immutable_unaccent` em public via `HasDbFunction.HasSchema("public")` + índices trigram em `organizacao.unidade`).
- [x] **Selecao**: validado (`c841fea`) — 98 testes integração; sem SQL cru remanescente (eixo de Área já removido).
- [x] **Ingresso**: validado — esqueleto, sem SQL cru; `has-pending` limpo.
- [x] **Gate**: build completo 0/0; **ArchTests 24/24** (R8 + ordem migrations→Wolverine intactos).
- [ ] Connection por módulo → banco `uniplus` + role/`search_path` por schema, e `init-db.sql` (banco `uniplus` + 4 schemas + roles; `uniplus_geo` intacto) — **feito junto do host (P3)**, onde as connection strings do processo único são configuradas.

> **Padrão validado por módulo:** (1) `const Schema` + `HasDefaultSchema(Schema)` no DbContext; (2) `schema:` na DI e na design-time factory; (3) `dotnet ef migrations add InitialCreate` com `--startup-project` no `.API` (necessário no .NET 10 p/ o tooling resolver runtime); (4) **re-adicionar SQL cru** (funções/índices via `migrationBuilder.Sql`) na nova InitialCreate, qualificado ao schema; (5) qualificar SQL cru dos testes (`INTO/FROM/UPDATE <tabela>` → `<schema>.<tabela>`); (6) validar `has-pending-model-changes` limpo + integração verde.

### P3 — composition root
- [x] **Padrão `Add{Modulo}Module` provado em Configuracao** (`e5c79b7`): registrações específicas do módulo (OpenAPI doc, `IDomainErrorRegistration`, HATEOAS, `AddIdempotency<DbContext>`, Application+Infrastructure, `AddDbContextMigrationsOnStartup<DbContext>`) extraídas para um método na camada `.API` (tipo público com `[SuppressMessage CA1515]` — consumido pelo host). Program.cs refatorado para chamá-lo. **62 testes integração verdes.**
- [x] `Add{Modulo}Module` extraído em **Organizacao, Ingresso e Selecao** (Selecao mantém Kafka/SchemaRegistry/Wolverine no Program.cs). Build 51 projetos 0/0; integração 62+47+98+8 (`13...`).
- [x] **Host criado** `src/host/Unifesspa.UniPlus.Host` (`feat(host)`): centraliza o compartilhado, chama os 4 `Add{Modulo}Module`, consolida o Wolverine numa instância (outbox banco `uniplus`/schema `wolverine`), 5 connections → `uniplus`. `HostAssemblyMarker`. **Build 52 projetos 0/0; ArchTests 24/24** (host fora do `ModulesRoster` → R8 intacto). O conflito de `Program` (4 `.API` executáveis) NÃO ocorreu.
- [ ] **Bootar o host** + smoke (precisa infra + banco `uniplus`; migrations criam os 4 schemas via `EnsureSchema`).

> **Decisões de design reveladas em P3 (resolver ao montar o host):**
> - **Health checks:** `AddUniPlusHealthChecks(config, "<Db>")` adiciona o check do banco do módulo **+ checks de infra compartilhada** (Redis/MinIO/Kafka/OIDC). Chamar 4× duplicaria os de infra → o host chama a parte de infra **uma vez** e cada módulo contribui só o check do **seu** banco. Exige fatiar `AddUniPlusHealthChecks` (infra-only + db-only) ou compor manualmente.
> - **OpenAPI:** cada módulo registra seu doc (`AddUniPlusOpenApi("<modulo>", ...)`); no host vira N docs `/openapi/<modulo>.json` (mantém os baselines `contracts/openapi.*.json`).
> - **Wolverine (P4):** fica fora de `Add{Modulo}Module` (é extensão do `IHostBuilder`, uma por processo). Cada módulo expõe um hook de routing/discovery; o host faz **um** `UseWolverineOutboxCascading` compondo os 4.

### P4 — Wolverine consolidado
- [x] **Uma instância** no host: `UseWolverineOutboxCascading(connectionStringName: "UniPlusDb", configureRouting: …4 IncludeAssembly…)`; outbox no schema `wolverine` do banco `uniplus`. Migrations on startup (nos `Add*Module`) precedem o Wolverine (invariante #419). Compila no host.
- [x] **Validado em runtime** (boot): o host sobe num processo único, os 4 `MigrationHostedService` criam os schemas de módulo e o Wolverine provisiona o outbox (schema `wolverine`) — `BootDoMonolitoTests` afirma os 5 schemas no banco único. Suite `tests/Unifesspa.UniPlus.Host.IntegrationTests` (Postgres efêmero, Wolverine **habilitado**, cache substituído por `FakeInMemoryCacheService` p/ dispensar Redis). **8 testes verdes.**
- [ ] **Externalização Kafka do Selecao DEFERIDA no spike** — o caminho de leitura in-process (P5) não a exige; o cascade handler de Kafka só dispara ao publicar edital. Religar no rollout (replicar SchemaRegistry + routing Kafka no host, ou mover para o hook do módulo).

### P5 — prova de leitura in-process (objetivo central) ✅
- [x] Host registra `IUnidadeReader` (via `AddOrganizacaoInstitucionalModule`); consumido in-process por outro módulo.
- [x] **Teste de integração (`LeituraInProcessTests`):** semeia uma `Unidade` no schema `organizacao` pelo DbContext do módulo e resolve `IUnidadeReader` do container do host (o mesmo que um handler de Configuração/Seleção resolveria) — `ObterPorIdAsync` e `ListarAtivasAsync` enxergam a Unidade viva **in-process**, sem hop de rede nem acesso direto ao schema. **Desbloqueio da #588.** Fronteira preservada: o consumidor depende de Governance.Contracts, não da Infrastructure de Organização (R8 intacto).

### P1 — prefixo de rota por módulo ✅
- [x] **Configuracao → `api/configuracao`** e **Organizacao → `api/organizacao`** (alinha ao Selecao, que já usava `api/selecao`). 6 controllers re-rotulados; HATEOAS propaga sozinho (usa `LinkGenerator.GetPathByAction`, zero edição nos builders); 6 suites de endpoint reescritas; 2 baselines `contracts/openapi.{configuracao,organizacao}.json` regenerados (diff 100% de paths, sem mudança de schema/operação); Postman da Organizacao + `contracts/README` atualizados.
- [x] **Colisão checada no monólito** (`RoteamentoSemColisaoTests` na suite do host): via `EndpointDataSource`, nenhum par (método, template) é atendido por 2+ endpoints; cada módulo expõe rotas sob seu prefixo; smoke HTTP `/health/live` 200 + `GET /api/{configuracao/campi, organizacao/unidades}` 200 no boot vivo. **13 testes no host.**
- Gate: build 0/0; Configuracao 62 (+1 skip) + Organizacao 47 + ArchTests 24/24 + host 13 — verde.

> **Achado P1 (registrar no ADR / rollout):** prefixar é **breaking change do contrato V1** — `uniplus-web` e `uniplus-developers` consomem `/api/campi`, `/api/unidades` etc. No rollout, re-sincronizar o contrato no web (fluxo `api/contracts` → `web/openapi` → `generate:api`) e regenerar o client após o merge. No spike (branch), não atinge o frontend até o merge.

### P6 / P7
- [ ] `CrossModuleReadIsolationTests`: isentar o host do `ModulesRoster` (já está) + fato novo positivo "host pode compor todos".
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
- **Co-hosting confirma boot sem conflito de `Program`** — os 4 `.API` executáveis declaram `public partial class Program` no namespace global; testes do host resolvem o entry point via `HostAssemblyMarker` (`WebApplicationFactory<HostAssemblyMarker>`) p/ evitar a ambiguidade CS0433. O processo único boota com 1 entry point (o do host).
- **Leitura in-process não exige Redis** — o `UnidadeReader` real é respaldado por cache, mas o caminho provado por P5 é DB-in-process; no teste o `ICacheService` vira fake (miss sempre + lease no-op), levando o reader direto à fonte. A durabilidade do cache fica nas suítes do próprio módulo.

## Estado atual
**P2 ✅, P3 ✅, P4 ✅ (estrutural + runtime), P5 ✅ (objetivo central provado), P1 ✅ (prefixo + colisão).** Fundação: schema param (`f39360b`) → IUnitOfWork por módulo (`a140128`) → schema-por-módulo nos 4 (`18daddd`,`b736c14`,`c841fea`,`2da84b4`) → `Add{Modulo}Module` nos 4 (`e5c79b7` + `13...`) → **host composition root** (`feat(host)`, ~commit 14) compondo os 4 módulos + Wolverine numa instância + 5 connections→`uniplus` → **prova de runtime** (`tests/Unifesspa.UniPlus.Host.IntegrationTests`): o host boota como processo único, cria os 5 schemas e lê a Unidade in-process → **P1** prefixo de rota por módulo + prova de ausência de colisão. **Build 0/0; suite do host 13/13; Configuracao 62 + Organizacao 47 + ArchTests 24/24.**

A **prova central do spike está feita**: monólito modular boota num processo único, banco único schema-por-módulo, consumidor cross-módulo lê a Unidade viva in-process via `IUnidadeReader` (desbloqueio #588) com fronteira R8 preservada, e o roteamento co-hospedado não colide (prefixo `api/{modulo}/`).

Falta apenas o **polimento de rollout**: P6/P7 + Kafka religado.

## Como retomar — próximo: P6/P7 (polimento) e ADR
Branch `spike/monolito-modular`. P5 e P1 provados em runtime. Sequência restante:
1. **P6** — `CrossModuleReadIsolationTests`: isentar o host do `ModulesRoster` (já está, R8 verde) e adicionar fato novo afirmando que **o host PODE compor todos os módulos** (contraponto positivo ao R8).
2. **P7** — `Dockerfile` do host + serviço compose único + `init-db` variante (banco `uniplus` + 4 schemas + role-por-schema; `uniplus_geo` intacto).
3. **Kafka do Selecao** religar (deferido) — ou documentar como follow-up de rollout.
4. **Follow-up frontend (rollout):** prefixo de rota é breaking change — re-sincronizar contrato no `uniplus-web` e regenerar client após o merge.

Gate final do spike: `dotnet build` + `dotnet test` completo verde (já com a suite do host) → então **escrever o ADR** (decisões em "Decisões para o ADR") e fechar, removendo este checkpoint no rollout.

Plano completo em `~/.claude/plans/qual-a-forma-replicated-journal.md`.
