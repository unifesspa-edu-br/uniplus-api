# CLAUDE.md — UniPlus API

## Stack e versões

- **Runtime:** .NET 10 / C# 14
- **ORM:** Entity Framework Core 10 (Npgsql)
- **Banco de dados:** PostgreSQL 18
- **Mensageria:** Apache Kafka 3.8 (KRaft)
- **Cache:** Redis 7
- **Storage:** MinIO (S3-compatible)
- **Autenticação:** Keycloak 26 (Gov.br)
- **CQRS:** MediatR 14
- **Validação:** FluentValidation 12
- **Logging:** Serilog 10
- **Observabilidade:** OpenTelemetry
- **Testes:** xUnit, FluentAssertions, NSubstitute, Bogus, Testcontainers, NetArchTest

## Estrutura do projeto

```
src/
├── shared/                          → Código compartilhado entre módulos
│   ├── Unifesspa.UniPlus.SharedKernel/       → Value objects, entidade base, Result pattern
│   └── Unifesspa.UniPlus.Infrastructure.Common/ → Kafka, Redis, MinIO, Serilog, health checks
├── selecao/                         → Módulo Seleção (editais, inscrições, classificação)
│   ├── Unifesspa.UniPlus.Selecao.Domain/
│   ├── Unifesspa.UniPlus.Selecao.Application/
│   ├── Unifesspa.UniPlus.Selecao.Infrastructure/
│   └── Unifesspa.UniPlus.Selecao.API/
└── ingresso/                        → Módulo Ingresso (chamadas, convocações, matrículas)
    ├── Unifesspa.UniPlus.Ingresso.Domain/
    ├── Unifesspa.UniPlus.Ingresso.Application/
    ├── Unifesspa.UniPlus.Ingresso.Infrastructure/
    └── Unifesspa.UniPlus.Ingresso.API/
tests/                               → Testes unitários, integração e arquitetura
```

## Namespace

`Unifesspa.UniPlus.{Modulo}.{Camada}`

Exemplos:
- `Unifesspa.UniPlus.SharedKernel.Domain.Entities`
- `Unifesspa.UniPlus.Selecao.Application.Commands.Editais`
- `Unifesspa.UniPlus.Ingresso.Infrastructure.Persistence`

## Regras de dependência (Clean Architecture)

```
Domain          → SharedKernel (somente)
Application     → Domain, SharedKernel
Infrastructure  → Application, Domain, SharedKernel, Infrastructure.Common
API             → Application, Infrastructure (apenas para DI registration)
```

Domain NUNCA depende de Application, Infrastructure ou API.
Application NUNCA depende de Infrastructure ou API.

## Padrões obrigatórios

- **Soft delete** em todas as entidades: `IsDeleted`, `DeletedAt`, `DeletedBy`
- **PII masking** em logs: CPF `***.***.***-XX`, nunca logar dados sensíveis
- **Result pattern** para retorno de operações: `Result<T>` com `DomainError`
- **CQRS** via MediatR: Commands para escrita, Queries para leitura
- **Value objects** para dados de domínio: `Cpf`, `Email`, `NomeSocial`, `NotaFinal`, `NumeroEdital`
- **Factory methods** com construtores privados em todas as entidades
- **Sealed classes** por padrão (exceto bases abstratas)
- **File-scoped namespaces** em todos os arquivos .cs
- **ConfigureAwait(false)** em awaits de código de biblioteca
- **TreatWarningsAsErrors** habilitado globalmente

## Comandos úteis

```bash
# Build completo
dotnet build UniPlus.slnx

# Testes
dotnet test UniPlus.slnx

# Testes de arquitetura apenas
dotnet test tests/Unifesspa.UniPlus.Selecao.ArchTests
dotnet test tests/Unifesspa.UniPlus.Ingresso.ArchTests

# Infraestrutura local (PostgreSQL, Redis, Kafka, MinIO, Keycloak)
docker compose -f docker/docker-compose.yml up -d

# APIs em modo desenvolvimento
docker compose -f docker/docker-compose.yml -f docker/docker-compose.override.yml up -d

# Migrations EF Core
dotnet ef migrations add <Nome> --project src/selecao/Unifesspa.UniPlus.Selecao.Infrastructure --startup-project src/selecao/Unifesspa.UniPlus.Selecao.API
```

## Git conventions

- **Branch naming:** `feature/{slug}`, `fix/{slug}`, `chore/{slug}`, `docs/{slug}`
- **Commits:** conventional commits em pt-BR — `feat(selecao): adicionar endpoint de criação de edital`
- **NUNCA commitar direto na main** — sempre feature branch + PR
- **NUNCA adicionar Co-Authored-By**
- **NUNCA usar --no-verify**

## Idioma

- Documentação e strings user-facing em **português do Brasil**
- Termos técnicos em inglês mantidos sem tradução (API, CQRS, MediatR, etc.)
- Nomes de código (classes, métodos, variáveis) em português para domínio, inglês para infra
