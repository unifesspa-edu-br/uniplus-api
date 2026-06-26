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
- [ ] **F5 — Ops**: remove Dockerfiles dos módulos; ajusta `compose.override` +
  `compose.monolito`; remove o target de appsettings; OIDC.
- [ ] **F6 — App + Newman**: sobe a stack, valida `/health`, OIDC e endpoints via
  Newman; fluxo completo.
- [ ] **F7 — ADR + gate final**: `dotnet build` + `dotnet test` completo verde;
  ADR registra a topologia 3 APIs.

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
