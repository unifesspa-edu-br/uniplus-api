# Implementation notes

Issue: https://github.com/unifesspa-edu-br/uniplus-api/issues/732
Branch/worktree: `feature/732-move-geo-api-to-dedicated-repository` at `/home/jeferson/Projects/workspaces/uniplus/repositories/uniplus-api-732`

## Decision status

The following preparation decisions are partially confirmed:

- Repository name: confirmed and created as `unifesspa-geo-api`.
- Repository visibility and owner: public repository under `unifesspa-edu-br`, aligned with `uniplus-api`.
- Branch protection: configured on `main`.
- CODEOWNERS policy: no visible GitHub teams were returned by `gh api orgs/unifesspa-edu-br/teams`; initial protection does not require CODEOWNER review (`require_code_owner_reviews=false`) and still requires one PR approval.
- Code independence: the dedicated Geo repository must not depend on `Unifesspa.UniPlus.*` packages or projects. Required shared behavior will be copied/adapted into Geo-owned namespaces.
- Ingress/release: preserve current Geo public contract and `service.name = uniplus-geo` during extraction; final URL/alias and first release version remain open.
- Cutover/freeze: no operational window has been recorded yet; keep task 1.6 open.

## Dedicated repository creation

Repository URL:

- `https://github.com/unifesspa-edu-br/unifesspa-geo-api`

Commands used:

```bash
rtk gh repo create unifesspa-edu-br/unifesspa-geo-api \
  --public \
  --description "API Geo institucional transversal da UNIFESSPA" \
  --add-readme
rtk gh api -X PATCH repos/unifesspa-edu-br/unifesspa-geo-api \
  -F has_issues=true \
  -F has_wiki=true \
  -F allow_merge_commit=false \
  -F allow_rebase_merge=true \
  -F allow_squash_merge=false \
  -F delete_branch_on_merge=false
rtk gh api -X PUT repos/unifesspa-edu-br/unifesspa-geo-api/topics --input -
rtk gh api -X PUT repos/unifesspa-edu-br/unifesspa-geo-api/branches/main/protection --input -
```

Repository settings verified:

- Default branch: `main`.
- Visibility: `PUBLIC`.
- Merge settings: rebase merge enabled; merge commit and squash merge disabled; delete branch on merge disabled.
- Topics: `api`, `geo`, `postgis`, `reference-data`, `unifesspa`.
- Branch protection on `main`: required status checks object present with strict mode and no contexts yet; one approving review required; stale reviews dismissed; last push approval required; required conversation resolution enabled; linear history required; force pushes and deletions disabled; restrictions unset.

Checks intentionally left for later tasks:

- Required CI status check names will be added after Geo workflows exist in the new repository.
- CODEOWNERS file/review requirement will be added after an owning team or user policy is confirmed.

## Filtered history extraction

Temporary clone:

- `/tmp/uniplus-api-732-geo-history`

Source:

- `unifesspa-edu-br/uniplus-api`
- Branch: `main`
- Source commit fetched as `origin/main`: `1526851da50bd76c0713f3c1046d2ab0e9954f10`

Tooling:

- `git-filter-repo` was not installed.
- Extraction used `git filter-branch --prune-empty --index-filter` in the temporary clone.

Preserved path filter:

- `src/geo/**`
- `tests/Unifesspa.UniPlus.Geo.IntegrationTests/**`
- `contracts/openapi.geo.json`
- `docs/geo-etl-dataset-dne.md`
- `docs/adrs/0090-*` through `docs/adrs/0096-*`

Extraction result before bootstrap infrastructure:

- 71 commits.
- 241 files.
- Latest filtered commit: `4996dbd97df49a95e4d8ada2b2df0d9717c3cdd7`.

Published branch:

- Repository: `https://github.com/unifesspa-edu-br/unifesspa-geo-api`
- Branch: `bootstrap/732-extract-geo-history`
- Local checkout: `/home/jeferson/Projects/workspaces/uniplus/repositories/unifesspa-geo-api`

## Bootstrap infrastructure copied to Geo repo

Files copied directly from `uniplus-api` because they were shared repo
infrastructure and not part of the filtered Geo history:

- `.editorconfig`
- `.gitignore`
- `global.json`
- `Directory.Build.props`
- `Directory.Packages.props`
- `docker/docker-compose.yml`
- `docker/init-db.sql`
- `tools/forbidden-deps/check.sh`
- `tools/forbidden-deps/README.md`
- `tools/spectral/.spectral.yaml`

Independence gate copied from this OpenSpec change:

- `tools/forbidden-deps/check-geo-independence.sh`

Provenance record added in the new repository:

- `docs/extraction-provenance.md`

Commit pushed to `unifesspa-geo-api`:

- `7fd5c08 chore(geo): registra bootstrap da extracao historica`

Validation:

- `bash -n tools/forbidden-deps/check-geo-independence.sh` passed in the new repo checkout.
- The full independence gate is expected to fail until tasks 3.4/3.5 rename namespaces and remove UniPlus project/package references.

## Independent solution bootstrap

Solution and bootstrap docs added to the `unifesspa-geo-api` branch
`bootstrap/732-extract-geo-history`:

- `Unifesspa.Geo.slnx`
- `README.md`
- `docs/development.md`
- `nuget.config`

The solution includes the five extracted production projects and the Geo
integration test project:

- `src/geo/Unifesspa.UniPlus.Geo.Domain/Unifesspa.UniPlus.Geo.Domain.csproj`
- `src/geo/Unifesspa.UniPlus.Geo.Contracts/Unifesspa.UniPlus.Geo.Contracts.csproj`
- `src/geo/Unifesspa.UniPlus.Geo.Application/Unifesspa.UniPlus.Geo.Application.csproj`
- `src/geo/Unifesspa.UniPlus.Geo.Infrastructure/Unifesspa.UniPlus.Geo.Infrastructure.csproj`
- `src/geo/Unifesspa.UniPlus.Geo.API/Unifesspa.UniPlus.Geo.API.csproj`
- `tests/Unifesspa.UniPlus.Geo.IntegrationTests/Unifesspa.UniPlus.Geo.IntegrationTests.csproj`

Commit pushed to `unifesspa-geo-api`:

- `cf1263e chore(geo): monta solution e docs de desenvolvimento`

Known next-step blocker:

- `dotnet sln Unifesspa.Geo.slnx add ...` completed but reported missing
  shared `src/shared/Unifesspa.UniPlus.*` and
  `tests/Unifesspa.UniPlus.IntegrationTests.Fixtures` references. This is
  expected until task 3.5 copies/adapts required shared code under Geo-owned
  namespaces.

## Geo file inventory

Commands used:

```bash
rtk rg --files src/geo | wc -l
rtk rg --files tests/Unifesspa.UniPlus.Geo.IntegrationTests | wc -l
rtk rg --files src/geo tests/Unifesspa.UniPlus.Geo.IntegrationTests contracts docs docker .github \
  | rg '(^src/geo/|^tests/Unifesspa\.UniPlus\.Geo\.IntegrationTests/|^contracts/openapi\.geo\.json$|^docs/geo-etl-dataset-dne\.md$|^docs/adrs/009[0-6].*geo|^docker/|^\.github/)' \
  | sort | wc -l
```

Current counts:

- `src/geo/**`: 194 files.
- `tests/Unifesspa.UniPlus.Geo.IntegrationTests/**`: 38 files.
- Direct Geo extraction candidates across source, tests, contract, docs, Docker and workflows: 268 files.

Source projects to extract:

- `src/geo/Unifesspa.UniPlus.Geo.API`
- `src/geo/Unifesspa.UniPlus.Geo.Application`
- `src/geo/Unifesspa.UniPlus.Geo.Contracts`
- `src/geo/Unifesspa.UniPlus.Geo.Domain`
- `src/geo/Unifesspa.UniPlus.Geo.Infrastructure`

Geo integration test areas:

- `Admin`: import endpoint coverage.
- `Api`: CEP, cidades, estados, hierarquia and proximidade endpoint coverage.
- `Cep`: resolver and formatting coverage.
- `Etl`: DNE source, periodic update, importers, reconciliation, seed and parsing coverage.
- `Infrastructure`: Geo API/OpenAPI/PostGIS factories and test keys.
- `Proximidade`: query parsing and SQL coverage.
- Top-level persistence/runtime tests: `CidadePersistenceTests`, `DistritoBairroPersistenceTests`, `GeoFundacaoTests`, `GeoSchemaDdlTests`, `LogradouroPersistenceTests`, `NtsMappingTests`, `OpenApiEndpointTests`, `PaisEstadoPersistenceTests`.

Canonical contract and Geo docs to migrate or repoint:

- `contracts/openapi.geo.json`
- `docs/geo-etl-dataset-dne.md`
- `docs/adrs/0090-modulo-geo-localidades.md`
- `docs/adrs/0091-postgis-georreferencia-nts.md`
- `docs/adrs/0092-etl-carga-dne-reference-data.md`
- `docs/adrs/0093-rate-limiting-na-borda-para-reference-data-publico.md`
- `docs/adrs/0094-keyset-ordenado-via-mr-sob-cursor-opaco.md`
- `docs/adrs/0095-chave-de-ordenacao-keyset-nao-nula.md`
- `docs/adrs/0096-endereco-como-referencia-estruturada-ao-geo.md`

Infrastructure files with Geo-specific content:

- `docker/init-db.sql`: creates `uniplus_geo_app`, `uniplus_geo`, and PostGIS prerequisites for Geo.
- `docker/docker-compose.yml`: PostGIS base image comments and shared local database substrate.
- `.github/workflows/publish-images.yml`: discovers `docker/Dockerfile.*`; Geo is not currently in the published image matrix unless a `docker/Dockerfile.geo` exists.
- `.github/workflows/trivy.yml`: uses the same Dockerfile discovery pattern and must be checked when a Geo Dockerfile is moved out.

## References outside `src/geo`

Build/solution references:

- `UniPlus.slnx` includes five Geo projects and `tests/Unifesspa.UniPlus.Geo.IntegrationTests`.
- `tests/Unifesspa.UniPlus.ArchTests/Unifesspa.UniPlus.ArchTests.csproj` references all five Geo projects.

Architecture and fitness tests to update when Geo leaves the repo:

- `tests/Unifesspa.UniPlus.ArchTests/SolutionRules/CrossModuleReadIsolationTests.cs`
- `tests/Unifesspa.UniPlus.ArchTests/SolutionRules/SolutionNaoTemMediatRTests.cs`
- `tests/Unifesspa.UniPlus.ArchTests/SolutionRules/SoftDeleteOptInConventionTests.cs`
- `tests/Unifesspa.UniPlus.ArchTests/SolutionRules/OpenApiSharedSchemasInSyncTests.cs`
- `tests/Unifesspa.UniPlus.ArchTests/SolutionRules/DominioNaoUsaGuidNewGuidTests.cs`
- `tests/Unifesspa.UniPlus.ArchTests/Hosting/MigrationBeforeWolverineRuntimeOrderTests.cs`

Shared code that mentions Geo semantics but is not itself the Geo service:

- `src/shared/Unifesspa.UniPlus.Kernel/Domain/Cidades/*`
- `src/shared/Unifesspa.UniPlus.Kernel/Domain/Enderecos/*`
- `src/shared/Unifesspa.UniPlus.Infrastructure.Core/Persistence/EnderecoGeoOwnedConfiguration.cs`
- `src/shared/Unifesspa.UniPlus.Infrastructure.Core/Persistence/UniPlusDbContextOptionsExtensions.cs`
- `src/shared/Unifesspa.UniPlus.Infrastructure.Core/OpenApi/UniPlusInfoTransformer.cs`
- `src/shared/Unifesspa.UniPlus.Infrastructure.Core/Observability/UniPlusServiceNames.cs`
- `src/shared/Unifesspa.UniPlus.Governance.Contracts/InstituicaoView.cs`

Consumer-side references that must remain contract/snapshot based:

- `src/configuracao/**` uses `ReferenciaCidadeGeo`, `ReferenciaEnderecoGeo`, `EnderecoGeoInput`, `EnderecoGeoDto` and `OrigemGeoApi` as snapshot/display-cache semantics.
- `contracts/openapi.configuracao.json` and `contracts/openapi.organizacao.json` contain `EnderecoGeo*` schemas that should not become runtime dependencies on Geo internals.

## ProjectReference dependency map

Geo projects currently depend on shared local projects:

- `src/geo/Unifesspa.UniPlus.Geo.Domain/Unifesspa.UniPlus.Geo.Domain.csproj`
  - `src/shared/Unifesspa.UniPlus.Kernel`
  - `src/shared/Unifesspa.UniPlus.Governance.Contracts`
- `src/geo/Unifesspa.UniPlus.Geo.Contracts/Unifesspa.UniPlus.Geo.Contracts.csproj`
  - `src/shared/Unifesspa.UniPlus.Kernel`
  - `src/shared/Unifesspa.UniPlus.Governance.Contracts`
- `src/geo/Unifesspa.UniPlus.Geo.Application/Unifesspa.UniPlus.Geo.Application.csproj`
  - `src/shared/Unifesspa.UniPlus.Kernel`
  - `src/shared/Unifesspa.UniPlus.Application.Abstractions`
  - `src/shared/Unifesspa.UniPlus.Governance.Contracts`
  - `src/geo/Unifesspa.UniPlus.Geo.Domain`
  - `src/geo/Unifesspa.UniPlus.Geo.Contracts`
- `src/geo/Unifesspa.UniPlus.Geo.Infrastructure/Unifesspa.UniPlus.Geo.Infrastructure.csproj`
  - `src/shared/Unifesspa.UniPlus.Infrastructure.Core`
  - `src/geo/Unifesspa.UniPlus.Geo.Application`
- `src/geo/Unifesspa.UniPlus.Geo.API/Unifesspa.UniPlus.Geo.API.csproj`
  - `src/geo/Unifesspa.UniPlus.Geo.Application`
  - `src/geo/Unifesspa.UniPlus.Geo.Infrastructure`

Geo integration tests currently depend on:

- `src/geo/Unifesspa.UniPlus.Geo.API`
- `tests/Unifesspa.UniPlus.IntegrationTests.Fixtures`

External package dependencies currently visible in Geo csproj files:

- API: `FluentValidation`, `Microsoft.AspNetCore.OpenApi`, `Serilog.AspNetCore`, `WolverineFx.Postgresql`.
- Application: `FluentValidation`, `FluentValidation.DependencyInjectionExtensions`.
- Domain: `NetTopologySuite`.
- Infrastructure: `Microsoft.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore.Relational`, `Npgsql.EntityFrameworkCore.PostgreSQL`, `Npgsql.EntityFrameworkCore.PostgreSQL.NetTopologySuite`.
- Integration tests: `coverlet.collector`, `Microsoft.AspNetCore.Mvc.Testing`, `Microsoft.EntityFrameworkCore`, `Microsoft.NET.Test.Sdk`, `NSubstitute`, `xunit`, `xunit.runner.visualstudio`, `AwesomeAssertions`, `Testcontainers.PostgreSql`, `WolverineFx`.

Minimum owned-code candidates before an independent Geo repository can restore without a sibling `uniplus-api` checkout:

- `Kernel`: value objects, domain primitives, cursor/pagination primitives and Geo snapshot/reference types actually used by Geo.
- `Application.Abstractions`: only application contracts that Geo endpoints/services still need after extraction.
- `Infrastructure.Core`: host/openapi/auth/observability/persistence helpers needed by the Geo runtime, copied in reduced form instead of referenced as shared UniPlus infrastructure.
- `Governance.Contracts`: institutional view contracts required by Geo, copied/adapted if still needed.
- `Unifesspa.UniPlus.IntegrationTests.Fixtures`: test support needed by Geo integration tests, copied/adapted into Geo-owned test fixtures.

Namespace evidence from current Geo source/tests:

- Application abstractions used by Geo: `Authentication`, `Interfaces`, `Messaging`.
- Kernel primitives used by Geo: `Domain.Entities`, `Pagination`, `Results`.
- Infrastructure.Core areas used by Geo runtime/tests: `Authentication`, `Caching`, `Cors`, `DependencyInjection`, `Errors`, `Formatting`, `Hateoas`, `Idempotency`, `Logging`, `Messaging`, `Middleware`, `Observability`, `Pagination`, `Persistence`, `Persistence.Interceptors`, `Profile`, `Smoke`.
- Integration test fixtures used by Geo tests: `Authentication`, `Hosting`.
- Governance contracts are currently referenced by Geo project files/lockfiles, but direct source usage was not found in `src/geo` or Geo integration test `using` statements. Re-check during extraction; likely candidate for removal rather than copy.

Concrete source areas to copy/adapt first:

- `src/shared/Unifesspa.UniPlus.Kernel/Results/*`
- `src/shared/Unifesspa.UniPlus.Kernel/Pagination/*`
- `src/shared/Unifesspa.UniPlus.Kernel/Domain/Entities/*`
- `src/shared/Unifesspa.UniPlus.Application.Abstractions/Authentication/*`
- `src/shared/Unifesspa.UniPlus.Application.Abstractions/Interfaces/*`
- `src/shared/Unifesspa.UniPlus.Application.Abstractions/Messaging/*`
- `src/shared/Unifesspa.UniPlus.Infrastructure.Core/{Authentication,Caching,Cors,DependencyInjection,Errors,Formatting,Hateoas,Idempotency,Logging,Messaging,Middleware,Observability,Pagination,Persistence,Profile,Smoke}/**`
- `tests/Unifesspa.UniPlus.IntegrationTests.Fixtures/{Authentication,Hosting}/**`

Independence gate draft:

- Added `check-geo-independence.sh` in this OpenSpec change directory.
- Intended command in the new repository: `bash check-geo-independence.sh .`.
- The gate fails on active `ProjectReference`, `PackageReference`, `using`, `namespace`, or `packages.lock.json` entries rooted at `Unifesspa.UniPlus.*`, excluding docs and OpenSpec planning artifacts.

## Independent codebase decision

The user clarified that Geo must become a separate project, 100% independent of UniPlus packages, with its own namespace and code. Duplicating code is acceptable.

Accepted rule for the dedicated repository:

- No `ProjectReference` to `uniplus-api`.
- No `PackageReference` to `Unifesspa.UniPlus.*`.
- No production namespace rooted at `Unifesspa.UniPlus.*` for new Geo-owned code.
- External NuGet dependencies remain allowed.
- Code copied from UniPlus must be treated as Geo-owned code after extraction, ideally with provenance documented in the new repository.

Package preparation rollback:

- The earlier package-oriented investigation was invalidated by the independence requirement.
- NuGet metadata added to shared `csproj` files was removed.
- The stale `src/shared/Unifesspa.UniPlus.Infrastructure.Core/packages.lock.json` diff generated by that attempt was reverted with a reverse patch.
- No package was published to GitHub Packages or any external feed.

## Independent namespace and owned shared code

Final decisions recorded for task 1.2:

- Namespace root: `Unifesspa.Geo`.
- Allowed dependencies: external/public NuGet packages required by runtime and tests, excluding `Unifesspa.UniPlus.*`.
- Copied-code policy: code brought from `uniplus-api` is treated as Geo-owned code after extraction and its origin is recorded in `docs/extraction-provenance.md`.

Commit pushed to `unifesspa-geo-api`:

- `7d5ad02 refactor(geo): assume codigo proprio com namespace independente`

Changes in that commit:

- Renamed Geo production projects and directories from `Unifesspa.UniPlus.Geo.*` to `Unifesspa.Geo.*`.
- Renamed Geo integration tests from `tests/Unifesspa.UniPlus.Geo.IntegrationTests` to `tests/Unifesspa.Geo.IntegrationTests`.
- Copied required shared code into Geo-owned projects:
  - `src/shared/Unifesspa.Geo.Kernel`
  - `src/shared/Unifesspa.Geo.Application.Abstractions`
  - `src/shared/Unifesspa.Geo.Governance.Contracts`
  - `src/shared/Unifesspa.Geo.Infrastructure.Core`
  - `tests/Unifesspa.Geo.IntegrationTests.Fixtures`
- Updated `Unifesspa.Geo.slnx`, project references, namespaces, docs and lockfiles.

Validation in the main new-repo checkout:

```bash
rtk dotnet restore Unifesspa.Geo.slnx --locked-mode
rtk dotnet build Unifesspa.Geo.slnx --no-restore
rtk bash tools/forbidden-deps/check.sh .
rtk bash tools/forbidden-deps/check-geo-independence.sh .
rtk rg -n "Unifesspa\\.UniPlus|unifesspa\\.uniplus|Unifesspa\\.Geo\\.Geo|UniPlus\\.Geo" src tests Unifesspa.Geo.slnx Directory.Build.props Directory.Packages.props nuget.config
```

Results:

- Restore locked passed with 12 projects, 0 errors, 0 warnings.
- Build passed with 12 projects, 0 errors, 0 warnings.
- `tools/forbidden-deps/check.sh` passed.
- `tools/forbidden-deps/check-geo-independence.sh` passed for project references, package references, using directives, namespaces and lockfiles.
- The `rg` scan returned no matches for old UniPlus namespaces/package identifiers in source/tests/solution/build config.

Clean checkout validation for task 3.6:

- Clean clone path: `/tmp/unifesspa-geo-api-clean.TXIlAN`
- Source: `https://github.com/unifesspa-edu-br/unifesspa-geo-api.git`
- Branch: `bootstrap/732-extract-geo-history`

Commands:

```bash
rtk git clone --branch bootstrap/732-extract-geo-history --single-branch https://github.com/unifesspa-edu-br/unifesspa-geo-api.git /tmp/unifesspa-geo-api-clean.TXIlAN
rtk dotnet restore Unifesspa.Geo.slnx --locked-mode
rtk dotnet build Unifesspa.Geo.slnx --no-restore
rtk bash tools/forbidden-deps/check.sh .
rtk bash tools/forbidden-deps/check-geo-independence.sh .
rtk rg -n "Unifesspa\\.UniPlus|unifesspa\\.uniplus|UniPlus\\.Geo|Unifesspa\\.Geo\\.Geo" src tests Unifesspa.Geo.slnx Directory.Build.props Directory.Packages.props nuget.config
```

Results:

- Restore locked passed with 12 projects, 0 errors, 0 warnings.
- Build passed with 12 projects, 0 errors, 0 warnings.
- `tools/forbidden-deps/check.sh` passed.
- `tools/forbidden-deps/check-geo-independence.sh` passed:
  - `project-reference`
  - `package-reference`
  - `using-uniplus`
  - `namespace-uniplus`
  - `lockfile-uniplus`
- The final `rg` scan returned no matches; exit code 1 is expected for "no matches".

## OpenAPI baseline ownership and drift gate

Baseline ownership:

- `contracts/openapi.geo.json` is present in `unifesspa-geo-api` as the canonical committed baseline.
- `README.md`, `docs/development.md` and `docs/extraction-provenance.md` point to `contracts/openapi.geo.json` in the dedicated repository.

Correction pushed to `unifesspa-geo-api`:

- `be0b447 test(geo): ajusta marcadores da raiz do repo dedicado`

Changes in that commit:

- `OpenApiEndpointTests.ResolveRepoPath` now locates the repository root by `Unifesspa.Geo.slnx`, not `UniPlus.slnx`.
- `KeycloakContainerFixture` now uses `Unifesspa.Geo.slnx` as the repository root marker and reports `unifesspa-geo-api` in the missing-file error.

Validation:

```bash
rtk dotnet test Unifesspa.Geo.slnx --filter SpecRuntime
rtk dotnet test tests/Unifesspa.Geo.IntegrationTests/Unifesspa.Geo.IntegrationTests.csproj --filter FullyQualifiedName~OpenApiEndpointTests
rtk env UPDATE_OPENAPI_BASELINE=1 dotnet test tests/Unifesspa.Geo.IntegrationTests/Unifesspa.Geo.IntegrationTests.csproj --filter SpecRuntime
rtk git diff -- contracts/openapi.geo.json
```

Results:

- `SpecRuntime_DeveCasarComBaselineCommitted` passed.
- `OpenApiEndpointTests` passed with 2 tests.
- The explicit `UPDATE_OPENAPI_BASELINE=1` regeneration path passed.
- `contracts/openapi.geo.json` had no diff after regeneration.

Gaps observed before the runtime migration commit, resolved in the next section:

- Full Geo integration test migration was blocked because Docker/Keycloak runtime assets such as `docker/keycloak/realm-e2e-tests.json` were not yet present in the dedicated repo.
- Runtime validation for `/health/live`, `/health/ready`, CEP, cidades/estados, hierarquia and proximidade still needed evidence.

## Runtime, Docker and integration test migration

Commit pushed to `unifesspa-geo-api`:

- `094aaee feat(geo): migra runtime local e testes de integracao`

Changes in that commit:

- Added `.dockerignore` to keep local `bin/obj` and transient test output out of Docker builds.
- Added `docker/Dockerfile.geo` for the dedicated API image.
- Added `docker/.env.example`.
- Added local/test runtime assets referenced by compose and fixtures:
  - `docker/keycloak/README.md`
  - `docker/keycloak/realm-e2e-tests.json`
  - `docker/keycloak/realm-export.json`
  - `docker/ldap/README.md`
  - `docker/ldap/bootstrap/01-users.ldif`
  - `docker/ldap/data/seed-4devs.json`
- Updated `docs/development.md` with local infra and image build commands.
- Added a `/health/live` smoke assertion to `GeoFundacaoTests`; `/health/ready` was already covered.

Validation:

```bash
rtk docker compose -f docker/docker-compose.yml --env-file docker/.env.example config --quiet
rtk docker build -f docker/Dockerfile.geo -t unifesspa-geo-api:bootstrap .
rtk dotnet test tests/Unifesspa.Geo.IntegrationTests/Unifesspa.Geo.IntegrationTests.csproj
rtk bash tools/forbidden-deps/check.sh .
rtk bash tools/forbidden-deps/check-geo-independence.sh .
```

Results:

- Compose config validation passed.
- Docker image build passed and produced local image `unifesspa-geo-api:bootstrap`.
- Full Geo integration test project passed with 282 tests and 0 warnings.
- Forbidden dependency gate passed.
- Geo independence gate passed for project references, package references, using directives, namespaces and lockfiles.

Runtime coverage now validated in the dedicated repo:

- `/openapi/geo.json`: `OpenApiEndpointTests`.
- `/health/live`: `GeoFundacaoTests.HealthLive_SemChecksExternos_Responde200`.
- `/health/ready`: `GeoFundacaoTests.HealthReady_ContraPostgis_Responde200`.
- Main Geo endpoints: CEP, cidades, estados, hierarquia, proximidade and admin import endpoints covered by the migrated integration suite.

## CI, security and release workflows

Commit pushed to `unifesspa-geo-api`:

- `2d42706 ci(geo): configura gates e publish do repo dedicado`

Files added:

- `.github/workflows/ci.yml`
- `.github/workflows/openapi.yml`
- `.github/workflows/codeql.yml`
- `.github/workflows/trivy.yml`
- `.github/workflows/publish-image.yml`
- `.github/dependabot.yml`

CI coverage configured:

- Restore locked.
- Build.
- Full Geo integration test project.
- `dotnet format Unifesspa.Geo.slnx --verify-no-changes`.
- `tools/forbidden-deps/check.sh`.
- `tools/forbidden-deps/check-geo-independence.sh`.
- Docker build and structural image smoke.

Contract/security/release coverage configured:

- OpenAPI drift test and Spectral lint for `contracts/openapi.geo.json`.
- Dependabot for NuGet and GitHub Actions.
- CodeQL C# analysis.
- Trivy filesystem and image scans, uploading SARIF.
- GHCR publish workflow for `ghcr.io/unifesspa-edu-br/unifesspa-geo-api`, triggered by `vX.Y.Z` and `vX.Y.Z-prerelease` tags reachable from `main`, with exact tag and `sha-<short-sha>` image tags.

Formatting cleanup:

- `dotnet format` normalized whitespace/charset and a few style issues in migrated source.
- `.editorconfig` was adjusted for xUnit/test-helper conventions while keeping production naming rules.

Local validation:

```bash
rtk dotnet restore Unifesspa.Geo.slnx --locked-mode
rtk dotnet build Unifesspa.Geo.slnx --no-restore
rtk dotnet test tests/Unifesspa.Geo.IntegrationTests/Unifesspa.Geo.IntegrationTests.csproj
rtk dotnet format Unifesspa.Geo.slnx --verify-no-changes --no-restore
rtk bash tools/forbidden-deps/check.sh .
rtk bash tools/forbidden-deps/check-geo-independence.sh .
rtk npx --yes @stoplight/spectral-cli@6.15.0 lint --ruleset tools/spectral/.spectral.yaml --fail-severity=error contracts/openapi.geo.json
rtk npx --yes yaml-lint .github/workflows/ci.yml .github/workflows/openapi.yml .github/workflows/codeql.yml .github/workflows/trivy.yml .github/workflows/publish-image.yml .github/dependabot.yml
rtk docker build -f docker/Dockerfile.geo -t unifesspa-geo-api:bootstrap .
```

Results:

- Restore locked passed with 12 projects, 0 errors, 0 warnings.
- Build passed with 12 projects, 0 errors, 0 warnings.
- Full Geo integration suite passed with 282 tests and 0 warnings.
- Format verification passed.
- Forbidden dependency and Geo independence gates passed.
- Spectral returned 0 errors and 24 warnings on the preserved baseline.
- YAML lint passed for workflows and Dependabot.
- Docker build passed.

Release documentation commit pushed to `unifesspa-geo-api`:

- `7dc6368 docs(geo): documenta release rollback e consumo`

Documentation added:

- `docs/release.md`
- README link to release/consumption docs.

Known open item:

- Task 5.5 remains open because publishing the first prerelease image requires a chosen prerelease tag on a commit reachable from `main`.

## `uniplus-api` cleanup

Implemented in worktree `uniplus-api-732` on branch
`feature/732-move-geo-api-to-dedicated-repository`.

Changes:

- Removed local Geo production projects from `UniPlus.slnx` and deleted
  `src/geo/**`.
- Deleted local Geo integration tests in
  `tests/Unifesspa.UniPlus.Geo.IntegrationTests/**`.
- Removed `contracts/openapi.geo.json` from `uniplus-api`; the canonical Geo
  contract now lives in `unifesspa-geo-api`.
- Removed Geo project references from `Unifesspa.UniPlus.ArchTests`.
- Updated architecture tests to drop Geo from the local module roster and from
  the executable-entry-point roster (`CrossModuleReadIsolationTests`,
  `MigrationBeforeWolverineRuntimeOrderTests`, `SoftDeleteOptInConventionTests`,
  `SolutionNaoTemMediatRTests`, `DominioNaoUsaGuidNewGuidTests`,
  `OpenApiSharedSchemasInSyncTests`).
- Kept local `uniplus_geo`/PostGIS provisioning in `docker/init-db.sql`; the
  `geo-api` service in the dev override now pulls the published GHCR image
  (`ghcr.io/unifesspa-edu-br/unifesspa-geo-api`, pinned via `GEO_IMAGE_TAG`)
  instead of building locally.
- Deleted `docker/Dockerfile.geo`, which drops Geo from the auto-discovered
  `publish-images.yml` matrix (no workflow edit needed).
- Added ADR-0099 and updated ADR-0090, the ADR index and
  `docs/geo-etl-dataset-dne.md` to point at the dedicated repo.
- Refreshed lockfiles required for `dotnet restore --locked-mode` after the
  Geo project removal and existing `Application.Abstractions` dependency drift.

Validation:

```bash
rtk dotnet restore UniPlus.slnx --locked-mode
rtk dotnet build UniPlus.slnx --no-restore
rtk dotnet test tests/Unifesspa.UniPlus.ArchTests/Unifesspa.UniPlus.ArchTests.csproj --no-build
rtk dotnet test UniPlus.slnx --filter "Category!=Integration" --no-build
rtk bash tools/forbidden-deps/check.sh
rtk bash tools/adr-lint/validate.sh
rtk npx --yes @stoplight/spectral-cli@6.15.0 lint --ruleset tools/spectral/.spectral.yaml --fail-severity=error contracts/openapi.selecao.json contracts/openapi.ingresso.json contracts/openapi.organizacao.json contracts/openapi.configuracao.json
rtk dotnet format whitespace UniPlus.slnx --verify-no-changes --no-restore --include tests/Unifesspa.UniPlus.ArchTests/SolutionRules/CrossModuleReadIsolationTests.cs tests/Unifesspa.UniPlus.ArchTests/SolutionRules/SolutionNaoTemMediatRTests.cs tests/Unifesspa.UniPlus.ArchTests/SolutionRules/SoftDeleteOptInConventionTests.cs tests/Unifesspa.UniPlus.ArchTests/SolutionRules/OpenApiSharedSchemasInSyncTests.cs tests/Unifesspa.UniPlus.ArchTests/SolutionRules/DominioNaoUsaGuidNewGuidTests.cs tests/Unifesspa.UniPlus.ArchTests/Hosting/MigrationBeforeWolverineRuntimeOrderTests.cs src/shared/Unifesspa.UniPlus.Infrastructure.Core/Persistence/UniPlusDbContextOptionsExtensions.cs
```

Results:

- Restore locked passed with 45 projects.
- Build passed with 45 projects, 0 errors, 0 warnings.
- ArchTests passed with 24 tests.
- Non-integration test suite passed with 1419 tests.
- Forbidden dependency check passed.
- ADR lint and markdownlint passed.
- Spectral returned 0 errors and 80 warnings on the remaining UniPlus baselines;
  warnings are existing missing `description`/`operationId`.
- Scoped whitespace/import formatting for touched C# files passed.

Open validation gap:

- Full `dotnet format UniPlus.slnx --verify-no-changes --no-restore` still fails
  on 348 pre-existing files, primarily whitespace in historical migrations and
  CA1515 style findings in test classes. The touched files were validated
  separately to avoid unrelated formatting churn.

Open operational items remain:

- URL/ingress/alias and initial release version decision.
- Freeze/cutover window and rollback owners by environment.
- First prerelease image publish.
- Environment infra/Helm cutover, smoke after promotion, observability check and
  rollback rehearsal.
