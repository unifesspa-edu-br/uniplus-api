# Repository Guidelines

## Project Structure & Module Organization

Uni+ API is a .NET 10/C# solution organized by bounded context and Clean Architecture. Main code lives in `src/`: `src/selecao/` and `src/ingresso/` each contain `Domain`, `Application`, `Infrastructure`, and `API` projects. Shared building blocks live in `src/shared/`, including `Unifesspa.UniPlus.Kernel`, `Application.Abstractions`, and `Infrastructure.Core`.

Tests live in `tests/` and mirror modules with `*.UnitTests`, `*.IntegrationTests`, and `*.ArchTests` projects. Docker files are under `docker/`; setup notes are in `docs/`.

## Build, Test, and Development Commands

- `dotnet build --warnaserrors` builds the solution and treats warnings as failures.
- `dotnet test --filter "Category!=Integration"` runs unit and architecture tests.
- `dotnet test --filter "Category=Integration"` runs integration tests; Docker must be available.
- `dotnet format --verify-no-changes` verifies formatting.
- `docker compose -f docker/docker-compose.yml up -d` starts PostgreSQL, Redis, Kafka, MinIO, and Keycloak.
- `dotnet run --project src/selecao/Unifesspa.UniPlus.Selecao.API` runs Seleção locally.
- `dotnet run --project src/ingresso/Unifesspa.UniPlus.Ingresso.API` runs Ingresso locally.

Copy `docker/.env.example` to `docker/.env` before using Docker services.

## Coding Style & Naming Conventions

Follow `.editorconfig`: 4-space indentation, LF endings, UTF-8, file-scoped namespaces, sorted `System` usings, and strict nullable warnings. Use explicit built-in types (`int`, `string`) instead of `var` unless the type is apparent. Private fields use `_camelCase`.

Use PascalCase for classes, methods, and properties; camelCase for locals and parameters; `I` prefix for interfaces. Keep domain code framework-independent. Put FluentValidation validators in Application and EF Core mappings in Infrastructure with `IEntityTypeConfiguration<T>`.

## Testing Guidelines

The test stack is xUnit, FluentAssertions, NSubstitute, Bogus, Testcontainers, coverlet, and ArchUnitNET. Name test classes after the unit under test, for example `CpfTests` or `ValidationBehaviorTests`. Test method names may use underscores to express scenario and expectation. Add tests beside the matching module and layer.

## Commit & Pull Request Guidelines

Use pt-BR Conventional Commits, matching history: `feat(shared): ...`, `fix(messaging): ...`, `docs(claude): ...`, `chore(arch-tests): ...`. Prefer scopes such as `selecao`, `ingresso`, `shared`, `domain`, `application`, `infra`, `api`, `db`, `auth`, `ci`, `docker`, and `deps`.

Branches should use `feature/{issue-number}-{slug}`, `fix/{issue-number}-{slug}`, or similar. Keep branches rebased on `main`, avoid merge commits, and use `--force-with-lease` after history rewrites. PRs should link the issue, describe behavioral changes, mention migrations or config changes, and pass build, tests, and formatting.

## Security & Configuration Tips

Never commit secrets or local `.env` values. Do not log complete CPF or other PII; use existing masking utilities. Validate all external input, require authorization policies on API endpoints, and prefer soft delete over physical deletion.
