# Guia Golden Path — Wolverine no `uniplus-api`

Guia operacional em pt-BR para criar novos commands, queries, handlers e domain events no `uniplus-api` usando **Wolverine + EF Core 10 + PostgreSQL 18**. Cada repositório de código mantém seu próprio guia adaptado ao contexto técnico — backend (.NET 10 / C# 14 / Wolverine / Clean Architecture) tem convenções específicas que não fazem sentido no `uniplus-web` (Angular / Nx) nem no `uniplus-docs` (especificações).

**Papéis:**

- **Este guia** é a fonte do **"como fazer"** — setup canônico, exemplos copiáveis, FAQ operacional do dia a dia.
- **As ADRs canônicas** são a fonte do **"o quê e por quê"** — decisão, contratos, escopo, guardrails, alternativas:
  - [ADR-0003](adrs/0003-wolverine-como-backbone-cqrs.md) — Wolverine como backbone CQRS in-process.
  - [ADR-0004](adrs/0004-outbox-transacional-via-wolverine.md) — Outbox transacional via Wolverine + EF Core sobre PostgreSQL.
  - [ADR-0005](adrs/0005-cascading-messages-para-drenagem-de-domain-events.md) — Cascading messages como drenagem canônica de domain events.
  - [ADR-0040](adrs/0040-helper-wolverine-outbox-cascading-canonico.md) — Helper canônico `UseWolverineOutboxCascading` para configuração compartilhada por módulo.
  - [ADR-0041](adrs/0041-padrao-retorno-handlers-wolverine-cascading.md) — Padrão de retorno `(Result, IEnumerable<object>)` em handlers que mutam agregados.
  - [ADR-0044](adrs/0044-roteamento-domain-events-pg-queue-kafka-opcional.md) — Roteamento produtivo de domain events.

Dúvidas sobre implementação no dia a dia ficam neste guia. Dúvidas sobre contrato, escopo ou reversibilidade vão para a ADR correspondente. Mudanças de escopo (novas abstrações, adoção de features avançadas do Wolverine) exigem emenda à ADR-0003 com PR explícito.

> **Princípio central:** habilitar o time antes de completar a arquitetura. Apenas duas abstrações (`ICommandBus`, `IQueryBus`) cruzam para `Application`. Drenagem de domain events não é abstração — é convenção de retorno do handler (cascading messages, ADR-0005). Features avançadas do Wolverine (sagas, scheduled messages, middleware custom específico) ficam fora do escopo até um caso de uso concreto pedir.

## Arquitetura resumida

```text
┌────────────────────────────────────────────────────────────┐
│  Application.Abstractions/Messaging/                       │
│    ICommandBus    Send<TResponse>(ICommand<TResponse>)     │
│    IQueryBus      Send<TResponse>(IQuery<TResponse>)       │
│    ICommand<T>    (marker)                                 │
│    IQuery<T>      (marker)                                 │
└──────────────────▲─────────────────────────────────────────┘
                   │ apenas estes 2 contratos vazam para Application
┌──────────────────┴─────────────────────────────────────────┐
│  Infrastructure.Core/Messaging/                            │
│    WolverineCommandBus      → IMessageBus.InvokeAsync      │
│    WolverineQueryBus        → IMessageBus.InvokeAsync      │
│    UseWolverineOutboxCascading (helper canônico, ADR-0040) │
│    WolverineValidationMiddleware  (FluentValidation)       │
│    WolverineLoggingMiddleware     (LoggerMessage)          │
│                                                            │
│  Wolverine cuida de:                                       │
│   • Discovery de handlers por convenção (assembly scan)    │
│   • Outbox transacional EF Core + PostgreSQL (ADR-0004)    │
│   • Cascading messages com atomicidade write+evento        │
│   • Middleware Command/Query                               │
└────────────────────────────────────────────────────────────┘
```

Handlers e código de domínio **nunca** importam `Wolverine.*`. Usam apenas os contratos do projeto. Isso é enforçado via fitness test ArchUnitNET ([ADR-0012](adrs/0012-archunitnet-como-fitness-tests-arquiteturais.md)).

## Configuração canônica

A configuração Wolverine vive no helper `UseWolverineOutboxCascading` em `src/shared/Unifesspa.UniPlus.Infrastructure.Core/Messaging/WolverineOutboxConfiguration.cs` ([ADR-0040](adrs/0040-helper-wolverine-outbox-cascading-canonico.md)). Cada `*.API/Program.cs` consome o helper passando connection string name + um callback opcional de roteamento específico do módulo:

```csharp
// src/selecao/Unifesspa.UniPlus.Selecao.API/Program.cs
builder.Host.UseWolverineOutboxCascading(
    builder.Configuration,
    connectionStringName: "SelecaoDb",
    configureRouting: opts =>
    {
        // Wolverine escaneia o entry assembly (Selecao.API) por padrão;
        // handlers produtivos vivem em Selecao.Application — incluir
        // explicitamente para que PublicarEditalCommandHandler (e futuros)
        // sejam descobertos. Obrigatório por ADR-0043.
        opts.Discovery.IncludeAssembly(typeof(PublicarEditalCommand).Assembly);

        // Roteamento específico do módulo Selecao (ADR-0044).
        opts.PublishMessage<EditalPublicadoEvent>().ToPostgresqlQueue("domain-events");
        opts.ListenToPostgresqlQueue("domain-events");

        if (!string.IsNullOrWhiteSpace(builder.Configuration["Kafka:BootstrapServers"]))
        {
            opts.PublishMessage<EditalPublicadoEvent>().ToKafkaTopic("edital_events");
        }
    });
```

> **`IncludeAssembly` é obrigatório** ([ADR-0043](adrs/0043-discovery-explicito-application-via-includeassembly.md)). Sem essa linha, Wolverine só escaneia o entry assembly (`*.API`) e não encontra os handlers em `*.Application` — `_commandBus.Send(...)` falha em runtime com `No handler found for ...`.

O helper centraliza as invariantes compartilhadas:

- `UseEntityFrameworkCoreTransactions` — atomicidade write+evento.
- `PersistMessagesWithPostgresql(..., schemaName: "wolverine")` — outbox no schema dedicado.
- `Policies.AutoApplyTransactions()` — handlers que tocam DbContext entram em transação automática.
- `Policies.UseDurableOutboxOnAllSendingEndpoints()` — durabilidade Kafka quando broker indisponível.
- `WolverineValidationMiddleware` + `WolverineLoggingMiddleware` — pipeline de validação + logging estruturado.
- **Auto-build do schema Wolverine desligado** — o helper **não** chama `AutoBuildMessageStorageOnStartup()`. Em produção, o operador roda `dotnet wolverine db-apply` (CLI da JasperFx) como step do pipeline de deploy, similar a `dotnet ef database update` ([ADR-0039](adrs/0039-provisioning-schema-wolverine-via-deploy.md)). Em testes integrados, a `CascadingFixture` usa `EnsureCreatedAsync` no Postgres efêmero do testcontainer.

Connection string e Kafka bootstrap são lidos lazy dentro do callback de `UseWolverine`, no startup do host — momento em que os providers de configuração já materializaram (env vars, appsettings). Esse padrão é compatível com o test fixture que injeta override via env var ([ADR-0038](adrs/0038-override-configuracao-em-testes-via-env-vars.md)).

> **Invariante crítico do projeto:** `PublishDomainEventsFromEntityFrameworkCore` está **desabilitado** por configuração — não há scraper EF varrendo `EntityBase.DomainEvents` ao final da transação. Eventos só são entregues ao bus quando o handler os retorna explicitamente via cascading ([ADR-0005](adrs/0005-cascading-messages-para-drenagem-de-domain-events.md)). Um handler que muta entidade e retorna apenas `Result` deixa os eventos acumulados sem nunca despachá-los — bug silencioso. Não há fitness test guardando essa invariante hoje (issue conhecida); revisão de código + a categoria `OutboxCascading` cobrem em parte. Reintroduzir o scraper exige PR explícito que reverta o helper `UseWolverineOutboxCascading` — não acontece por acidente.

## Fitness test ArchUnitNET

A biblioteca canônica de fitness tests arquiteturais é **ArchUnitNET** ([ADR-0012](adrs/0012-archunitnet-como-fitness-tests-arquiteturais.md)). O encapsulamento `Application` ↛ `Wolverine` é garantido por testes que falham o build no CI:

```csharp
// tests/Unifesspa.UniPlus.Selecao.ArchTests/Stage1ArchitectureRulesTests.cs
[Fact]
public void ApplicationEDomain_NaoDependemDeWolverine()
{
    Classes()
        .That()
        .ResideInAssembly(typeof(SelecaoApplicationMarker).Assembly)
        .Or()
        .ResideInAssembly(typeof(SelecaoDomainMarker).Assembly)
        .Should()
        .NotDependOnAny(
            Types()
                .That()
                .ResideInNamespace("Wolverine", useRegularExpressions: false))
        .Check(Architecture);
}
```

Regras análogas existem em `Unifesspa.UniPlus.Ingresso.ArchTests` e na suíte solution-wide `Unifesspa.UniPlus.ArchTests` (impede que qualquer assembly do produto importe `MediatR.*`).

## Passo 1 — definir o command (ou query)

**Commands** representam intenções de mudança de estado. **Queries** representam leitura. Ambos são `sealed record`s, mas com convenções de retorno distintas:

```csharp
// src/selecao/Unifesspa.UniPlus.Selecao.Application/Commands/Editais/PublicarEditalCommand.cs
namespace Unifesspa.UniPlus.Selecao.Application.Commands.Editais;

public sealed record PublicarEditalCommand(Guid EditalId) : ICommand<Result>;
```

```csharp
// src/selecao/Unifesspa.UniPlus.Selecao.Application/Queries/Editais/ObterEditalQuery.cs
namespace Unifesspa.UniPlus.Selecao.Application.Queries.Editais;

// Queries retornam o DTO direto (`?` quando "não encontrado" é caso normal).
public sealed record ObterEditalQuery(Guid Id) : IQuery<EditalDto?>;
```

```csharp
// src/selecao/Unifesspa.UniPlus.Selecao.Application/Queries/Editais/ListarEditaisQuery.cs
public sealed record ListarEditaisQuery(Guid? AfterId, int Limit) : IQuery<ListarEditaisResult>;
```

**Convenções:**

- `sealed record` — valor, igualdade estrutural, imutável.
- Namespace: `<Module>.Application.{Commands,Queries}.<Feature>`.
- Nome termina em `Command` (mutação) ou `Query` (leitura).
- **Commands** retornam `Result` ou `Result<T>` — nunca `void`, nunca `Task`, nunca o tipo de domínio direto. Mantém o contrato de erro explícito ([ADR-0046](adrs/0046-validacao-de-regras-sem-excecao-result-failure.md)).
- **Queries** retornam o **DTO de leitura direto** (`EditalDto?`, `ListarEditaisResult`) — sem `Result<T>`. Queries não falham por regra de negócio; falham por entrada inválida (`ValidationException` via middleware) ou erro técnico (mapeado pelo `GlobalExceptionMiddleware`). Essa simetria evita boilerplate de `Result.Failure` em caminhos de leitura onde "não encontrado" é resposta normal (`null` ou coleção vazia).

## Passo 2 — escrever o command handler que muta agregado

Handlers são classes `sealed class` ou `static class` convention-based. **Sem interface. Sem atributo. Sem registro no DI.** Wolverine descobre pelo nome (`<Command>Handler`) e pelo método (`Handle`).

Quando o handler **muta agregado** e o agregado emite domain events, o retorno é uma **tupla** `(Result, IEnumerable<object>)` ([ADR-0041](adrs/0041-padrao-retorno-handlers-wolverine-cascading.md)). O Wolverine reconhece o padrão, propaga a `Result` para o caller via `ICommandBus.Send<Result>` e captura o `IEnumerable<object>` como cascading messages, persistindo cada envelope no outbox dentro da `IEnvelopeTransaction` ativa — atomicidade write+evento garantida por design.

Slice de referência: `src/selecao/Unifesspa.UniPlus.Selecao.Application/Commands/Editais/PublicarEditalCommandHandler.cs`.

```csharp
public sealed class PublicarEditalCommandHandler
{
    public static async Task<(Result Resposta, IEnumerable<object> Eventos)> Handle(
        PublicarEditalCommand command,
        IEditalRepository editalRepository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(editalRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        Edital? edital = await editalRepository
            .ObterPorIdAsync(command.EditalId, cancellationToken)
            .ConfigureAwait(false);

        if (edital is null)
        {
            return (
                Result.Failure(new DomainError(
                    "Edital.NaoEncontrado",
                    $"Edital '{command.EditalId}' não encontrado.")),
                []);
        }

        if (edital.Status == StatusEdital.Publicado)
        {
            return (
                Result.Failure(new DomainError(
                    "Edital.JaPublicado",
                    $"Edital '{command.EditalId}' já está publicado.")),
                []);
        }

        edital.Publicar();
        editalRepository.Atualizar(edital);
        await unitOfWork
            .SalvarAlteracoesAsync(cancellationToken)
            .ConfigureAwait(false);

        // ADR-0005 + ADR-0041: drenagem por cascading messages.
        // Cast<object> garante o switch case `IEnumerable<object>` em
        // MessageContext.EnqueueCascadingAsync sem depender de covariância
        // implícita de IDomainEvent (interface) para object.
        return (Result.Success(), edital.DequeueDomainEvents().Cast<object>());
    }
}
```

**Pontos críticos:**

1. **Dependências vêm pelos parâmetros do método**, não pelo construtor. `IRepository<T>`, `IUnitOfWork`, `ILogger<T>`, services DI quaisquer — todos no método `Handle`. Convenção do Wolverine.
2. **Sem `IRequestHandler<,>` de MediatR.** Fitness test bloqueia o build. MediatR foi removido do projeto.
3. **`Handle` é `static` por padrão.** Reduz pressão de GC e elimina necessidade de instanciar a classe a cada request.
4. **Retorne `Result` ou `Result<T>`.** `throw` é apenas para erros técnicos inesperados ([ADR-0046](adrs/0046-validacao-de-regras-sem-excecao-result-failure.md)).
5. **Transaction é automática.** `Policies.AutoApplyTransactions()` envolve todo handler que toque `DbContext`.
6. **Domain events são retornados explicitamente.** `entity.DequeueDomainEvents().Cast<object>()` é o helper canônico do `EntityBase` — combina snapshot atômico e clear da coleção interna ([ADR-0005](adrs/0005-cascading-messages-para-drenagem-de-domain-events.md)).
7. **`DequeueDomainEvents()` deve vir DEPOIS de `SaveChangesAsync`.** Drenar antes do save abre janela em que o agregado fica sem eventos se houver rollback.

Handlers de **query** (leitura pura) seguem assinatura `Task<TResponse>` simples sem tupla, retornando o DTO direto. Slice de referência: `src/selecao/Unifesspa.UniPlus.Selecao.Application/Queries/Editais/ObterEditalQueryHandler.cs`.

```csharp
public static class ObterEditalQueryHandler
{
    public static async Task<EditalDto?> Handle(
        ObterEditalQuery query,
        IEditalRepository editalRepository,
        CancellationToken ct)
    {
        Edital? edital = await editalRepository
            .ObterPorIdAsync(query.Id, ct)
            .ConfigureAwait(false);

        if (edital is null)
            return null;

        return new EditalDto(
            edital.Id,
            edital.NumeroEdital.ToString(),
            // ... demais campos do DTO
        );
    }
}
```

`null` aqui não é erro — é resposta semanticamente válida para "edital não encontrado". O controller mapeia `null` para `404 NotFound` no caminho HTTP, sem precisar instanciar `Result.Failure`. Query handlers podem ser declarados como `static class` (sem instanciamento por request) quando o método `Handle` é `static`.

## Passo 3 — escrever o domain event handler

Domain events reagem a fatos consumados. Mesmo padrão dos command handlers: classe pública (`sealed partial` quando usa `[LoggerMessage]` source generator) + método `static Handle`:

```csharp
// src/selecao/Unifesspa.UniPlus.Selecao.Application/Events/Editais/EditalPublicadoEventHandler.cs
[SuppressMessage(
    "Naming",
    "CA1711:Identifiers should not have incorrect suffix",
    Justification = "Convenção do projeto: subscritores de domain events terminam em EventHandler.")]
public sealed partial class EditalPublicadoEventHandler
{
    public static void Handle(
        EditalPublicadoEvent @event,
        ILogger<EditalPublicadoEventHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(logger);

        LogEditalPublicadoRecebido(logger, @event.EditalId, @event.NumeroEdital);
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "EditalPublicadoEvent recebido. EditalId={EditalId} NumeroEdital={NumeroEdital}")]
    private static partial void LogEditalPublicadoRecebido(
        ILogger logger,
        Guid editalId,
        string numeroEdital);
}
```

**Regras:**

- Retorne `Task` ou `void` (não `Task<TResponse>`). Event handlers não decidem sobre resposta HTTP — são side-effects reativos.
- **Logging via `[LoggerMessage]` source generator é obrigatório** — chamadas diretas a `_logger.LogInformation(...)` etc. são bloqueadas pelo analisador `CA1848` com `TreatWarningsAsErrors`.
- **Não engula falhas transitórias.** Wolverine entrega eventos at-least-once: se o handler completar normalmente, a mensagem é considerada processada. Para falhas retryables (rede instável, serviço externo indisponível, deadlock transient), **deixe a exceção propagar** — Wolverine aplica a política de retry configurada e, esgotada, manda para dead letter. Apenas suprima erros que sejam **esperados e idempotentes** ("já processado", "estado consistente"). Handlers devem ser idempotentes por convenção (mesma mensagem entregue duas vezes não deve duplicar side-effect).
- Um evento pode ter múltiplos handlers. Wolverine invoca todos.

## Invocando bus a partir de um controller

Controllers MVC ([ADR-0036](adrs/0036-controllers-mvc-para-negocio-minimal-api-para-shared.md)) injetam `ICommandBus` ou `IQueryBus` + `IDomainErrorMapper` e seguem o padrão canônico **success-then-failure** ([ADR-0024](adrs/0024-mapeamento-domain-error-http.md)):

```csharp
[ApiController]
[Route("api/editais")]
public sealed class EditalController(
    ICommandBus commandBus,
    IQueryBus queryBus,
    IDomainErrorMapper mapper) : ControllerBase
{
    private readonly ICommandBus _commandBus = commandBus;
    private readonly IQueryBus _queryBus = queryBus;
    private readonly IDomainErrorMapper _mapper = mapper;

    [HttpPost("{id:guid}/publicar")]
    public async Task<IActionResult> Publicar(Guid id, CancellationToken ct)
    {
        Result resultado = await _commandBus.Send(new PublicarEditalCommand(id), ct);
        if (resultado.IsSuccess)
            return NoContent();
        return resultado.ToActionResult(_mapper);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> ObterPorId(Guid id, CancellationToken ct)
    {
        EditalDto? edital = await _queryBus.Send(new ObterEditalQuery(id), ct);
        return edital is null ? NotFound() : Ok(edital);
    }
}
```

**Padrão canônico de mapping `Result` → HTTP:**

- Comando bem-sucedido → controller decide o status code apropriado (`NoContent`, `Created`, `Ok` com payload).
- Comando falho → `resultado.ToActionResult(_mapper)` mapeia `DomainError` para `ProblemDetails` RFC 9457 ([ADR-0023](adrs/0023-wire-formato-erro-rfc-9457.md), [ADR-0024](adrs/0024-mapeamento-domain-error-http.md)).
- **`ToActionResult` exige `IDomainErrorMapper` injetado e só é válido para failures** — chamar em `Result.Success` lança exceção. Por isso o pattern é sempre `if (IsSuccess) return ...; return resultado.ToActionResult(_mapper);`, nunca `return resultado.ToActionResult()` direto.

Slice canônico real: `src/selecao/Unifesspa.UniPlus.Selecao.API/Controllers/EditalController.cs`.

**Nunca** injete `IMessageBus` do Wolverine diretamente em controllers ou handlers. Sempre `ICommandBus` ou `IQueryBus` — contrato enforçado pela ArchUnitNET.

## Validações via FluentValidation middleware

Validação de comando/query usa `WolverineValidationMiddleware` automaticamente — basta declarar um validator em `Application` no mesmo namespace do command:

```csharp
public sealed class PublicarEditalCommandValidator : AbstractValidator<PublicarEditalCommand>
{
    public PublicarEditalCommandValidator()
    {
        RuleFor(x => x.EditalId).NotEmpty();
    }
}
```

O middleware Wolverine intercepta antes do handler. Quando `ValidationResult` tem failures, **lança `FluentValidation.ValidationException`** — o handler nunca é invocado. A exceção é capturada pelo `GlobalExceptionMiddleware`, que produz `application/problem+json` com `422 Unprocessable Entity` (RFC 9457).

**Importante:** validação **não** retorna `Result.Failure(...)` do bus. O caminho de exception → 422 é o boundary canônico de validação no projeto ([ADR-0024](adrs/0024-mapeamento-domain-error-http.md)). `Result.Failure` continua sendo o caminho de **regra de negócio** (estado do agregado, invariantes de domínio), não de **forma** dos dados de entrada.

**Handlers não chamam validator manualmente.**

## O que NÃO fazer

Lista prescritiva. Cada item é uma violação que CI ou review vai sinalizar:

- ❌ **Não** importar `Wolverine.*` em código de `Application`, `Domain` ou módulos. Use apenas `ICommandBus`/`IQueryBus`. Fitness test ArchUnitNET bloqueia.
- ❌ **Não** declarar `IRequestHandler<,>` de MediatR. Fitness test solution-wide bloqueia (zero tolerância — MediatR foi removido).
- ❌ **Não** registrar handlers manualmente no DI (`services.AddTransient<...>`). Wolverine descobre via assembly scan ([ADR-0043](adrs/0043-discovery-explicito-application-via-includeassembly.md)).
- ❌ **Não** adicionar `[Handler]`, `[Command]` ou markers semelhantes. A convenção (classe pública + método `Handle`) basta.
- ❌ **Não** introduzir `ISaga`, `IScheduler`, `IOutbox` como contratos de aplicação. Se precisar da capacidade, abra PR propondo emenda à ADR-0003.
- ❌ **Não** lance `ValidationException` dentro de handlers. Retorne `Result.Failure(...)`.
- ❌ **Não** acesse a tabela de outbox (`wolverine.wolverine_outgoing_envelopes`) diretamente. É detalhe de infraestrutura.
- ❌ **Não** chame `IMessageBus.Publish` direto após `SaveChangesAsync` para drenar eventos. Use `entity.DequeueDomainEvents().Cast<object>()` no return da tupla — caminho via cascading respeita a `IEnvelopeTransaction` ativa.
- ❌ **Não** use `_logger.LogInformation(...)` direto. Padrão obrigatório `[LoggerMessage]` source generator (CA1848).
- ❌ **Não** habilite `EnableRetryOnFailure` em DbContext usado por handlers Wolverine — incompatível com `Policies.AutoApplyTransactions` ([ADR-0004](adrs/0004-outbox-transacional-via-wolverine.md)).

## FAQ

### Por que `static` no método `Handle`?

Reduz pressão de GC (zero alocação para a instância do handler) e enfatiza que o método é função pura sobre os parâmetros injetados. Wolverine source generator gera código otimizado em tempo de compilação — `static` torna o caminho mais direto.

### Por que dependências via parâmetros do método, não pelo construtor?

Convenção do Wolverine. O framework gera código source-generated para cada handler, eliminando reflection em runtime. O parâmetro do método é o input para esse código gerado. Como efeito colateral, fica mais fácil testar: o método é essencialmente uma função — passe os argumentos, asserte o retorno.

### Onde o outbox aparece?

Em lugar nenhum visível para quem escreve handler. Fluxo:

1. Handler retorna tupla `(Result, IEnumerable<object>)`.
2. Wolverine `CaptureCascadingMessages` percorre o `IEnumerable<object>` e instala cada envelope no `wolverine.wolverine_outgoing_envelopes` dentro da `IEnvelopeTransaction` da política `AutoApplyTransactions` + `EnrollDbContextInTransaction`.
3. Após `SaveChangesAsync` cometer com sucesso, Wolverine despacha os eventos via PostgreSQL queue (intra-módulo) e/ou Kafka (inter-módulo, [ADR-0044](adrs/0044-roteamento-domain-events-pg-queue-kafka-opcional.md)).
4. Se a transação fizer rollback, os eventos não são despachados. Atomicidade garantida por design.

### Como testar um handler?

Dois cenários, alinhados a [ADR-0046](adrs/0046-validacao-de-regras-sem-excecao-result-failure.md). Evite `UseInMemoryDatabase` — o provider in-memory diverge materialmente do PostgreSQL ([ADR-0007](adrs/0007-postgresql-18-como-banco-primario.md)) e a própria Microsoft desaconselha.

**Handler unitário (lógica pura)** — chame o método `static` direto, passe test doubles (NSubstitute) e `Bogus` para fakes, asserte o `Result` e o `IEnumerable<object>` retornado. Não precisa de fixture com Postgres real:

```csharp
[Fact]
public async Task Deve_falhar_quando_edital_nao_encontrado()
{
    var repository = Substitute.For<IEditalRepository>();
    repository.ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
        .Returns((Edital?)null);
    var unitOfWork = Substitute.For<IUnitOfWork>();

    var (resposta, eventos) = await PublicarEditalCommandHandler.Handle(
        new PublicarEditalCommand(Guid.NewGuid()),
        repository,
        unitOfWork,
        CancellationToken.None);

    resposta.IsFailure.Should().BeTrue();
    resposta.Error!.Code.Should().Be("Edital.NaoEncontrado");
    eventos.Should().BeEmpty();
}
```

**Handler de integração com Postgres** — `Testcontainers.PostgreSql` + fixture compartilhada `CascadingApiFactory` que herda da configuração produtiva via `Program.cs` real ([ADR-0045](adrs/0045-test-factory-remove-wolverine-runtime.md)). Validações cobrem `Category=OutboxCapability` e `Category=OutboxCascading` — conferir suíte em `tests/<Module>.IntegrationTests/`.

### Como adicionar um novo domain event?

1. Criar `sealed record` extendendo `DomainEventBase` em `<Module>.Domain/Events/`:

   ```csharp
   public sealed record InscricaoRealizadaEvent(
       Guid InscricaoId,
       Guid CandidatoId,
       Guid EditalId) : DomainEventBase;
   ```

2. Dentro da entidade, chamar `AddDomainEvent(new InscricaoRealizadaEvent(...))` na transição de estado que emite o evento.
3. Garantir que o command handler que mutou o agregado retorna `entity.DequeueDomainEvents().Cast<object>()` na segunda posição da tupla.
4. Opcionalmente, criar um `EventHandler` em `<Module>.Application/Events/<Feature>/` seguindo o Passo 3 acima.
5. Atualizar o `configureRouting` callback de `UseWolverineOutboxCascading` no `Program.cs` se o evento precisa cruzar módulo via Kafka ([ADR-0044](adrs/0044-roteamento-domain-events-pg-queue-kafka-opcional.md)).

### Por que o helper `UseWolverineOutboxCascading` em vez de configurar inline em cada `Program.cs`?

Centralização das invariantes compartilhadas elimina drift entre módulos ([ADR-0040](adrs/0040-helper-wolverine-outbox-cascading-canonico.md)). Se um dia alguém esquecer `Policies.UseDurableOutboxOnAllSendingEndpoints()` em um dos módulos, a inconsistência vira bug silencioso (um persiste, outro não). Helper único com 3 eixos de variação (connection string, Kafka key, routing callback) cobre todas as necessidades reais sem inflar com builder pattern.

### `PublishDomainEventsFromEntityFrameworkCore` é proibido?

É **desabilitado por configuração no projeto** — não há scraper EF varrendo `EntityBase.DomainEvents` ao final da transação. Eventos só são entregues quando o handler os retorna explicitamente. O caminho continua válido no framework como fallback excepcional para casos legados específicos (migração de código MediatR-style, agregados de bibliotecas externas), mas exigiria justificativa em ADR adicional.

### Existem handlers MediatR legados no projeto?

Não. O scaffolding inicial do projeto removeu MediatR antes do primeiro merge produtivo. O fitness test solution-wide `SolutionNaoTemMediatRTests.NenhumTipoDoProdutoDependeDeMediatR` falha o build se qualquer assembly do produto importar `MediatR.*` — zero tolerância.

### Como funcionam as categorias de teste `OutboxCapability` e `OutboxCascading`?

- **`Category=OutboxCapability`** — cobre o outbox transacional ([ADR-0004](adrs/0004-outbox-transacional-via-wolverine.md)): persistência, atomicidade, recuperação, retry, dead letters, schema versioning. Aplicada também a fixtures HTTP/OpenAPI/listagem que herdam o pipeline produtivo — o filtro varre todos os contratos que dependem do outbox real.
- **`Category=OutboxCascading`** — cobre o caminho idiomático cascading ([ADR-0005](adrs/0005-cascading-messages-para-drenagem-de-domain-events.md)): drenagem via tupla de retorno, comportamento sob cancellation, atomicidade write+evento.
- Use os filtros sem hardcodar totais — `dotnet test --filter Category=OutboxCapability` lista os cenários atuais. A suíte combinada é o gate de regressão para qualquer mudança em handler que mute agregado ou em infraestrutura de outbox.

### E se eu precisar de saga / process manager para um fluxo cross-boundary?

Abrir PR propondo **emenda à ADR-0003** com:

- Descrição do caso de uso concreto (não hipotético).
- Proposta de onde a saga vive (qual módulo).
- Proposta de novas abstrações (se necessárias) em `Application.Abstractions`.
- Consequências e reversibilidade.

Apenas após emenda aceita, adicionar features avançadas do Wolverine.

## Referências

- [ADR-0003](adrs/0003-wolverine-como-backbone-cqrs.md) — Wolverine como backbone CQRS in-process.
- [ADR-0004](adrs/0004-outbox-transacional-via-wolverine.md) — Outbox transacional via Wolverine + EF Core sobre PostgreSQL.
- [ADR-0005](adrs/0005-cascading-messages-para-drenagem-de-domain-events.md) — Cascading messages como drenagem canônica de domain events.
- [ADR-0012](adrs/0012-archunitnet-como-fitness-tests-arquiteturais.md) — ArchUnitNET como fitness tests arquiteturais.
- [ADR-0036](adrs/0036-controllers-mvc-para-negocio-minimal-api-para-shared.md) — Controllers MVC para negócio, Minimal API para shared.
- [ADR-0040](adrs/0040-helper-wolverine-outbox-cascading-canonico.md) — Helper canônico `UseWolverineOutboxCascading`.
- [ADR-0041](adrs/0041-padrao-retorno-handlers-wolverine-cascading.md) — Padrão de retorno `(Result, IEnumerable<object>)`.
- [ADR-0044](adrs/0044-roteamento-domain-events-pg-queue-kafka-opcional.md) — Roteamento produtivo de domain events.
- [ADR-0046](adrs/0046-validacao-de-regras-sem-excecao-result-failure.md) — Validação sem exceção via `Result.Failure`.
- [Wolverine — documentação oficial](https://wolverinefx.net/)
- [Wolverine — Cascading Messages](https://wolverinefx.net/guide/handlers/cascading.html)
- [Wolverine — Durable Outbox](https://wolverinefx.net/guide/durability/)

---

> **Histórico:** uma versão anterior deste guia vivia em `unifesspa-edu-br/uniplus-docs/docs/guia-wolverine-golden-path.md`, escrita para um estágio anterior do backbone (abstração `IDomainEventDispatcher` + flush automático após `SaveChangesAsync`). Pela regra de cada repositório manter seus próprios artefatos docs/, este guia é o canônico — a versão antiga foi marcada como histórica/superseded. Issue da migração: [`uniplus-api#353`](https://github.com/unifesspa-edu-br/uniplus-api/issues/353).
