# CLAUDE.md — UniPlus API

## Stack e versões

- **Runtime:** .NET 10 / C# 14
- **ORM:** Entity Framework Core 10 (Npgsql)
- **Banco de dados:** PostgreSQL 18
- **Mensageria:** Apache Kafka 4.2 (KRaft)
- **Cache:** Redis 8
- **Storage:** MinIO (S3-compatible)
- **Autenticação:** Keycloak 26.5 (Gov.br)
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
- **PII masking** em logs: CPF `***.***.***-XX`, nunca logar dados sensíveis — aplicado automaticamente pelo `PiiMaskingEnricher` (registrado no pipeline Serilog via `ConfigurarSerilog`) a todas as propriedades estruturadas, inclusive aninhadas (`StructureValue`, `SequenceValue`, `DictionaryValue`)
- **Result pattern** para retorno de operações: `Result<T>` com `DomainError`
- **CQRS** via MediatR: Commands para escrita, Queries para leitura
- **Value objects** para dados de domínio: `Cpf`, `Email`, `NomeSocial`, `NotaFinal`, `NumeroEdital`
- **Factory methods** com construtores privados em todas as entidades
- **Sealed classes** por padrão (exceto bases abstratas)
- **File-scoped namespaces** em todos os arquivos .cs
- **ConfigureAwait(false)** em awaits de código de biblioteca
- **TreatWarningsAsErrors** habilitado globalmente
- **Logging com `[LoggerMessage]` source generator** — nunca usar `_logger.LogInformation(...)` e similares diretamente (ver seção abaixo)

## Logging de alta performance — `[LoggerMessage]` source generator

**Regra obrigatória:** toda chamada a `ILogger` deve passar por um método `partial` decorado com `[LoggerMessage]`. Chamadas diretas a `_logger.LogInformation`, `_logger.LogWarning`, etc. são **proibidas** — o analisador `CA1848` trata isso como erro por causa do `TreatWarningsAsErrors`.

### Por que

O source generator de `[LoggerMessage]` (.NET 6+) gera código que:

1. **Evita avaliação de argumentos quando o log level está desativado** — o `IsEnabled(LogLevel.X)` é chamado antes de tocar nos parâmetros. Com `_logger.LogInformation("valor {V}", ObterValor())`, o `ObterValor()` executa sempre, mesmo quando `Information` está desligado em produção.
2. **Elimina boxing de value types** (structs, ints, longs, DateTimeOffset, etc.).
3. **Parseia o message template uma única vez**, na compilação — não a cada chamada.
4. **Zero alocações temporárias** para o array `params object[]` das extensões padrão.

### Padrão idiomático

Classe `partial`, método `private static partial void Log{Ação}` no fim da classe, `ILogger` como primeiro parâmetro:

```csharp
namespace Unifesspa.UniPlus.Selecao.Application.Behaviors;

using MediatR;
using Microsoft.Extensions.Logging;

public sealed partial class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger) => _logger = logger;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        string requestName = typeof(TRequest).Name;
        LogProcessando(_logger, requestName);                       // chamada idiomática
        TResponse response = await next(ct).ConfigureAwait(false);
        LogConcluido(_logger, requestName, stopwatch.ElapsedMilliseconds);
        return response;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Processando {RequestName}")]
    private static partial void LogProcessando(ILogger logger, string requestName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Concluído {RequestName} em {ElapsedMs}ms")]
    private static partial void LogConcluido(ILogger logger, string requestName, long elapsedMs);
}
```

### Convenções de estilo

- **Método é `private static partial void`** — `static` aproveita-se do fato de `ILogger` vir como parâmetro, evitando captura de `this`.
- **Nome começa com `Log`** seguido do verbo/ação (`LogProcessando`, `LogConcluido`, `LogValidationError`).
- **Placeholders em `{PascalCase}`** batendo com o nome do parâmetro. O Serilog e os enrichers (ex.: `PiiMaskingEnricher`) usam esses nomes como chave estruturada.
- **`Exception` sempre como último parâmetro** quando presente — o source generator reconhece e emite no `LogEvent.Exception` sem precisar `{Exception}` no template.
- **`EventId` opcional** — incluir apenas se houver consumidor que filtra por ID (raro no Uni+). Se omitido, o gerador atribui um automaticamente.

### Casos especiais — `SkipEnabledCheck`

Quando o argumento passado ao log envolver computação cara (ex.: serialização de DTO, formatação complexa), usar `SkipEnabledCheck = true` + guarda manual `IsEnabled` no call site para evitar também a avaliação do argumento **na chamada**:

```csharp
public void RegistrarResultado(ResultadoInscricao resultado)
{
    if (_logger.IsEnabled(LogLevel.Debug))
    {
        string snapshot = JsonSerializer.Serialize(resultado);   // só executa se Debug está ligado
        LogResultadoDetalhado(_logger, snapshot);
    }
}

[LoggerMessage(Level = LogLevel.Debug, Message = "Resultado detalhado: {Snapshot}", SkipEnabledCheck = true)]
private static partial void LogResultadoDetalhado(ILogger logger, string snapshot);
```

Sem `SkipEnabledCheck`, o source generator sempre gera a guarda internamente — mas a `JsonSerializer.Serialize(resultado)` executa antes da chamada do método, no call site, antes da guarda. Esse é o cenário que o analisador `CA1873` (Avoid potentially expensive logging) sinaliza.

### Referências

- [CA1848 — Use the LoggerMessage delegates](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1848)
- [CA1873 — Avoid potentially expensive logging](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1873)
- [Compile-time logging source generation](https://learn.microsoft.com/dotnet/core/extensions/logging/source-generation)
- Exemplos no projeto: `src/*/API/Middleware/GlobalExceptionMiddleware.cs`, `src/*/Application/Behaviors/LoggingBehavior.cs`

## Supressão de análise de código

`TreatWarningsAsErrors` está ligado — supressões são permanentes. Antes de suprimir, **investigar causa raiz**: warning pode apontar naming, dependência invertida ou catch-all legítimos de refactor (CA1716 "Shared" virou rename `IntegrationTests.Fixtures`, ADR-001 crosscutting).

Adotar a técnica **mais específica** possível, sempre com `Justification` preenchida:

1. `[SuppressMessage]` no símbolo ou `GlobalSuppressions.cs` com `Scope`/`Target`. Padrão.
2. `.editorconfig` com glob de caminho — apenas para política de camada (ex.: `[tests/**/*.cs]` para convenções xUnit).
3. `<NoWarn>` em csproj — evitar.
4. `#pragma warning disable` inline — evitar; aceitável só em bloco muito localizado (ex.: `catch (Exception)` em exception boundary).

Exemplos no repo: `src/*/API/GlobalSuppressions.cs` (CA1515/Program), `tests/*/Infrastructure/*ApiFactory.cs` (CA1515/fixture), `.editorconfig` seção `[tests/**/*.cs]`.

Docs: [SuppressMessageAttribute](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/suppress-warnings), [editorconfig](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/configuration-files).

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

## Workflow obrigatório

### Regra de ouro: sem issue, sem código

- **NUNCA implementar código sem uma issue/story vinculada no GitHub** — toda implementação deve estar rastreada
- **NUNCA trabalhar diretamente na `main`** — sempre criar feature branch a partir de uma issue
- **NUNCA criar diretório local avulso** — sempre clonar o repositório remoto primeiro
- Antes de iniciar qualquer implementação, verificar:
  1. Existe uma issue aberta no GitHub para o trabalho?
  2. A issue tem critérios de aceite claros?
  3. Se não existe, **criar a issue primeiro** e só depois implementar
- Ao criar a branch, vincular à issue: `feature/{issue-number}-{slug}` ou `fix/{issue-number}-{slug}`

### Fluxo de trabalho

```
1. Issue no GitHub (story, task ou bug)
2. Clonar o repositório (ou pull se já clonado)
3. Criar feature branch: git checkout -b feature/{issue-number}-{slug}
4. Implementar na branch
5. Commit(s) com conventional commits
6. Push + criar PR vinculando a issue (Closes #N)
7. Review + merge
```

### Repositórios da organização

- Organização GitHub: `unifesspa-edu-br`
- `uniplus-api` — Backend .NET 10
- `uniplus-web` — Frontend Angular 20
- `uniplus-docs` — Documentação

## Git conventions

- **Branch naming:** `feature/{issue-number}-{slug}`, `fix/{issue-number}-{slug}`, `chore/{slug}`, `docs/{slug}`
- **Commits:** conventional commits em pt-BR — `feat(selecao): adicionar endpoint de criação de edital`
- **NUNCA commitar direto na main** — sempre feature branch + PR
- **NUNCA adicionar Co-Authored-By**
- **NUNCA usar --no-verify**

## Idioma

- Documentação e strings user-facing em **português do Brasil**
- Termos técnicos em inglês mantidos sem tradução (API, CQRS, MediatR, etc.)
- Nomes de código (classes, métodos, variáveis) em português para domínio, inglês para infra
