# Uni+ API

Backend da plataforma **Uni+** da [UNIFESSPA](https://unifesspa.edu.br) — Universidade Federal do Sul e Sudeste do Pará.

## Sobre

Sistema que gerencia o ciclo de vida completo dos processos seletivos e ingresso de estudantes na UNIFESSPA. Desenvolvido pelo Departamento de Sistemas Acadêmicos (DISI/CTIC).

## Módulos

| Módulo | Namespace | Descrição |
|---|---|---|
| **Seleção** | `Unifesspa.UniPlus.Selecao` | Processos seletivos — editais, inscrições, classificação, resultados |
| **Ingresso** | `Unifesspa.UniPlus.Ingresso` | Matrícula — homologação de documentos, chamadas de vagas |
| **SharedKernel** | `Unifesspa.UniPlus.SharedKernel` | Value objects, interfaces e eventos compartilhados |

## Stack

- **Runtime:** C# 14 / .NET 10
- **Banco de dados:** PostgreSQL 18
- **Mensageria:** Apache Kafka
- **Cache:** Redis
- **ORM:** Entity Framework Core
- **CQRS/messaging:** Wolverine 5.x (`ICommandBus`, `IQueryBus`) + FluentValidation 12 — pipeline de validação e logging por middleware nativo do Wolverine
- **Autenticação:** Keycloak (SSO UNIFESSPA) + Gov.br (Login Único)
- **Observabilidade:** OpenTelemetry → Grafana

## Arquitetura

Cada módulo segue **Clean Architecture** com camadas:

```
Domain        → Entidades, value objects, domain events
Application   → Use cases (commands/queries via Wolverine), DTOs, validações (FluentValidation)
Infrastructure→ EF Core, Kafka, Redis, MinIO, Keycloak
API           → Controllers, middleware, filtros
```

Os módulos são **independentes** — cada um compila e faz deploy como serviço separado no Kubernetes. Comunicação exclusivamente via eventos Kafka.

## Pré-requisitos

- .NET 10 SDK (10.0.100+)
- Docker e Docker Compose v2

## Como executar

```bash
# Criar arquivo de variáveis de ambiente
cp docker/.env.example docker/.env

# Subir infraestrutura (PostgreSQL, Redis, Kafka, MinIO, Keycloak)
docker compose -f docker/docker-compose.yml up -d

# Executar módulo Seleção
dotnet run --project src/selecao/Unifesspa.UniPlus.Selecao.API

# Executar módulo Ingresso
dotnet run --project src/ingresso/Unifesspa.UniPlus.Ingresso.API
```

Para o guia completo com troubleshooting, veja [docs/setup-ambiente-local.md](docs/setup-ambiente-local.md).

## Testes

```bash
# Testes unitários
dotnet test

# Testes de integração (requer Docker)
dotnet test --filter "Category=Integration"

# Mutation testing (domain layer)
dotnet stryker
```

## Contribuindo

1. Crie uma branch: `feature/{slug}`, `fix/{slug}`
2. Commits em conventional commits (pt-BR): `feat(selecao): adicionar endpoint de inscrição`
3. Abra um Pull Request para `main`
4. Aguarde revisão e aprovação

## Licença

[MIT](LICENSE)
