# Checkpoint — Refatoração "3 APIs" (monólito modular: módulos viram libraries)

> Artefato de trabalho da branch `spike/monolito-modular`. Execução autônoma.
> Atualizado entre cada fase para permitir retomada. Removido no rollout junto
> com o checkpoint do spike.

## Objetivo

Topologia de deploy final = **3 APIs executáveis**: **Geo**, **Portal** e
**UniPlus** (o monólito). Os 4 módulos internos (Selecao, Ingresso, Configuracao,
OrganizacaoInstitucional) **deixam de ser APIs deployáveis** e viram **class
libraries** consumidas pela API UniPlus (`Unifesspa.UniPlus.Host`).

Motivação: elimina **na raiz** a colisão de `appsettings*.json` no publish
(libraries não carregam appsettings — só a API UniPlus tem), tornando
desnecessário o target de remoção no publish (gambiarra evitada). É a forma
arquiteturalmente correta do co-hosting.

## Decisões de arquitetura (travadas)

1. **Módulos `.API` → class library**: `Sdk="Microsoft.NET.Sdk"` +
   `<FrameworkReference Include="Microsoft.AspNetCore.App" />` (controllers em lib).
   Remover `Program.cs`, `appsettings*.json`, `GlobalSuppressions` de `Program`.
2. **Entry point único dos módulos internos = API UniPlus** (`HostAssemblyMarker`).
   - Testes HTTP (factories `ApiFactoryBase<Program>`) → reapontados para o host
     via base compartilhada (sobem o monólito; conn strings das 5 → mesmo Postgres).
   - Testes de **persistência** (DbContext direto, `*DbFixture`) → **inalterados**
     (não dependem de `Program`).
3. **Baselines OpenAPI** dos módulos → gerados pelo host UniPlus (já isola
   `/openapi/{modulo}.json` via `ModuleApiGroupingConvention`).
4. **Docker**: remover `Dockerfile.{selecao,ingresso,configuracao,organizacao}`;
   `compose.override` mantém só `monolito`(UniPlus)/`geo`/`portal`-api. Remover o
   target de appsettings do host (não mais necessário).
5. **OIDC**: a stack do monólito aponta `Auth__Authority` ao realm `unifesspa` do
   Keycloak (validar fluxo de mutação autenticada).
6. **Validação final**: subir a API UniPlus via compose + **Newman** contra os
   endpoints (fluxo completo, incl. autenticação).
7. **Revisão Codex** ao fim de cada fase; atenção a gambiarras.

## Fases

- [x] **F1 — Piloto: Configuracao** vira lib; factories HTTP → host; baseline
  regenerado; build + testes verdes. Valida a abordagem inteira. (commit `5cd68ad`)
- [x] **F2 — Organizacao** (mesmo padrão). (commit `4527a95`)
- [x] **F3 — Ingresso** (esqueleto; mais simples). (commit `1c141e6`)
- [x] **F4 — Selecao** (por último — Kafka/cascading/SchemaRegistry). **98/98 verde.** (commit `21518f3`)
- [x] **F4.1 — Achados Codex Fase 1**: ArchTests (MigrationOrder→3 entry points
  executáveis; CrossModuleRead→Geo/Portal standalone) + smoke InfraCore→host +
  lock files regenerados (locked-mode OK). **ArchTests 19/19, smoke 21/21.** (commit `3f1cfd7`)
- [x] **F5 — Ops**: remove Dockerfiles dos módulos; `override.example`→monólito
  (uniplus-api + portal-api) + infra + OIDC; `init-db.sql`→3 bancos; traefik +
  frontend-test → uniplus-api; remove target appsettings do host (publish validado). (commit `0c5a2d2`)
- [x] **F6 — App + Newman**: stack `docker compose -f docker-compose.yml -f
  docker-compose.override.yml up -d --build` sobe verde (uniplus-api healthy).
  Smoke: `/health`+`/health/live` 200; GETs anônimos dos 3 módulos
  (organizacao/configuracao/selecao) 200; OpenAPI por-módulo 200; token OIDC
  (password grant, realm dev-local) 200. Newman (coleção Organizacao apontada ao
  `:5200`): **23/24 asserções** — token + negativos auth (401/400/422) + ciclo de
  vida CRUD (201→GET→409→PUT→DELETE→404) + soft-delete + idempotência, tudo via
  monólito. 1 falha = drift de contrato PRÉ-EXISTENTE do Organizacao (read expõe
  `cidade`, coleção espera `municipioSede`) — não é regressão (código do módulo
  não muda ao virar library).
- [x] **F7 — ADR + gate final**: `dotnet build` (54 projetos, 0 warnings) +
  `dotnet test` completo verde (**21 projetos, 1714 testes, 0 falhas, 1 ignorado**);
  ADR-0097 registra a topologia de 3 APIs e refina o ADR-0001.

## Estado atual

**Início.** Branch `spike/monolito-modular`. Pré-requisitos já no lugar:
- API UniPlus (`Unifesspa.UniPlus.Host`) compõe os 4 módulos, com OpenAPI por-módulo,
  idempotência module-aware, Kafka do Selecao religado (hook do módulo), boot
  validado por Testcontainers e por `docker compose`.
- `MonolitoHostFixture` (em `Host.IntegrationTests`) é o padrão de referência para
  subir o host com Postgres + 5 conn strings — base para os factories migrados.

## Como retomar

Ler este arquivo + `git log`. Cada fase é commitada. Próxima fase = primeira `[ ]`.
Plano e dimensionamento completos discutidos na sessão; achados de cada fase
registrados abaixo.

## Achados por fase

### F4 — Selecao

- **Padrão de factories**: 3 factories migradas para o composition root (host):
  - `SelecaoApiFactory : MonolitoApiFactory` (wolverine=false, HTTP-only).
  - `OidcRealApiFactory : MonolitoApiFactory` (wolverine=false) — mantém o pipeline
    `JwtBearer` real (override `ConfigureTestAuthentication` no-op) e aponta
    `Auth:Authority/Audience` ao Keycloak via novo hook `OverridesAdicionais()`.
  - `CascadingApiFactory : MonolitoApiFactory` (wolverine=true) — só adiciona o
    `DomainEventCollector`; o re-registro manual do `SelecaoDbContext` virou
    **redundante** (o host já registra via `AddSelecaoInfrastructure` com todos os
    interceptors + snake_case). Gambiarra removida.
- **CascadingFixture** convergiu para a base genérica nova
  `MonolitoPostgresFixtureBase<TFactory>` (factory tipada via `CreateFactory`),
  reaproveitando o ciclo de vida do container + 5 env vars + desligamento do Kafka.
  Antes setava só `ConnectionStrings__SelecaoDb`; o host precisa das 5.
- **Bug pego no teste (não em prod)**: `AddInMemoryCollection` lança em **chave
  duplicada** (não é last-wins). O `OverridesAdicionais()` da subclasse OIDC
  colidia com os defaults `Auth:*`. Corrigido coletando overrides num `Dictionary`
  (indexer = last-wins, chave única) antes de passar ao builder.
- **CascadingFixtureConfigurationTests**: asserção de DB ajustada de
  `uniplus_outbox_cascading` → marcador `uniplus_test` (usuário do testcontainer,
  distinto do `uniplus` do appsettings), refletindo o banco único `uniplus`.

### F4.1 — Achados Codex Fase 1 (pendentes)

- **[P1]** `MigrationOrderFixture`/`CrossModuleReadIsolationTests` ainda esperam
  `Program` executável de Configuracao → fitness tests quebram com módulo-lib.
- **[P2]** `Dockerfile.configuracao` + serviço `configuracao-api` no compose obsoletos
  (tratado na F5).
- **[P2]** `packages.lock.json` de Geo/InfraCore/etc. com drift → `dotnet restore`
  para regenerar (CI roda `--locked-mode`).

### F4.2 — Achados da 2ª revisão Codex (Fase 4 + F4.1)

- **[P2] resolvido]** Docker do Selecao órfão → tratado na F5 (Dockerfiles removidos).
- **[P2] resolvido]** Migration order não travava por módulo → reforçado: o teste
  agora assere o conjunto EXATO de DbContexts por entry point (commit `477cdae`).

### F6 — Validação em runtime (pré-existentes, fora do escopo do spike)

- **Organizacao — drift de contrato**: `POST admin/instituicao` aceita
  `municipioSede` (201) mas `GET instituicao` expõe `cidade` (null). Coleção
  Newman espera `municipioSede`. Não é regressão do refactoring (o módulo é
  byte-idêntico ao virar library) — é divergência create/read DTO do próprio
  Organizacao. Follow-up para o time do módulo.
- **Wolverine 6.0 forward-compat**: repos `internal` (UnidadeRepository,
  CampusRepository, InstituicaoRepository) disparam warning de service-location
  ("not public, requires service location; will throw in Wolverine 6.0"). Também
  pré-existente (apareceria nas APIs standalone). Quando migrar para Wolverine 6,
  tornar os repos public ou ajustar a ServiceLocationPolicy.
- **EF "errors" no 1º boot**: `SELECT ... FROM __EFMigrationsHistory ORDER BY
  migration_id` falha (Error) antes de a tabela existir — comportamento normal do
  migration runner em banco novo (cria o schema em seguida). App fica healthy.
