# Repository Guidelines

## Project Structure & Module Organization
Uni+ API is a .NET 10 / C# 14 modular monolith. Production code lives under
`src/`: each module, such as `selecao`, `ingresso`, `geo`, `configuracao`, and
`organizacao-institucional`, follows `Domain`, `Application`, `Infrastructure`,
and `API` projects. `src/shared` contains cross-cutting kernel, infrastructure,
and authorization code; `src/host` composes the monolith. Tests live in
`tests/` as `*.UnitTests`, `*.IntegrationTests`, and `*.ArchTests`. OpenAPI
baselines are in `contracts/`; local infrastructure and deployment assets are
in `docker/`, `infra/`, `scripts/`, and `tools/`.

## Build, Test, and Development Commands
```bash
docker compose -f docker/docker-compose.yml up -d
dotnet restore UniPlus.slnx --locked-mode
dotnet build UniPlus.slnx
dotnet test UniPlus.slnx --filter "Category!=Integration"
dotnet test UniPlus.slnx --filter "Category=Integration"
dotnet format --exclude-diagnostics CA1515 --verify-no-changes
bash tools/forbidden-deps/check.sh
```
Never run `dotnet format` without `--exclude-diagnostics CA1515`: `xunit.analyzers` ships a
`DiagnosticSuppressor` for CA1515 that the build honors but `dotnet format` does not, so the
command rewrites every public test class to `internal` and breaks the build with
`xUnit1000: Test classes must be public`. See `CONTRIBUTING.md`.
Use Docker for PostgreSQL, Redis, Kafka, MinIO, and Keycloak. Use
`dotnet restore UniPlus.slnx --force-evaluate` only when intentionally updating
NuGet versions or lockfiles. Regenerate OpenAPI baselines with
`UPDATE_OPENAPI_BASELINE=1 dotnet test --filter SpecRuntime`.

## Coding Style & Naming Conventions
Follow `.editorconfig`: four spaces, LF, UTF-8, final newline, file-scoped
namespaces, nullable enabled, and warnings treated as errors. Use PascalCase for
types, members, constants, and static readonly fields; camelCase for locals and
parameters; `_camelCase` for private fields. Keep identifiers in English and
user-facing API messages in pt-BR. Application validation uses FluentValidation;
EF mappings use `IEntityTypeConfiguration<T>` and the global snake_case
convention. New logging should use `[LoggerMessage]`, not direct
`logger.LogInformation(...)` calls.

## Testing Guidelines
Tests use xUnit, AwesomeAssertions, NSubstitute, Bogus, Testcontainers,
ArchUnitNET, and coverlet. Name tests in the
`Method_Condition_ExpectedResult` style used by the suite. Mark integration
tests with `Category=Integration`; they require Docker. Domain and Application
coverage should stay at or above 80%.

## Commit & Pull Request Guidelines
Use Conventional Commits in pt-BR:
`feat(selecao): adiciona endpoint de inscricao`. Prefer branch names like
`feature/{issue-number}-{slug}`, `fix/{issue-number}-{slug}`, or
`docs/{slug}`. Keep branches rebased on `origin/main`; use
`git push --force-with-lease` after rewriting history. PRs need a filled
template, linked issue (`Closes #N`), one approval, current CI, and green local
build/test/format gates.

## Security & Configuration Tips
Never commit secrets or real personal data. CPF and other PII must not appear in
logs or test data. Start local configuration from `docker/.env.example`, prefer
environment/Vault-provided settings, and do not introduce test-environment
branches in production code under `src/`.
