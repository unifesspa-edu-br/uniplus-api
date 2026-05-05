---
status: "accepted"
date: "2026-05-05"
decision-makers:
  - "Tech Lead (CTIC)"
---

# ADR-0043: Discovery explícito da Application layer no Wolverine via `Discovery.IncludeAssembly`

## Contexto e enunciado do problema

Wolverine descobre handlers escaneando (a) o entry assembly do host e (b) qualquer assembly marcado com `[assembly: WolverineModule<T>]`. O entry assembly é `Selecao.API` (o `Program.cs`), mas os handlers produtivos vivem em `Selecao.Application` — assembly que `Selecao.API` referencia mas que, sem instrução explícita, não entra no scan do Wolverine.

Sem inclusão, o Wolverine inicializa sem registrar `PublicarEditalCommandHandler` e o caller `_commandBus.Send<Result>(new PublicarEditalCommand(...))` lança em runtime "no handler found".

A pergunta: como instruir o Wolverine a escanear `Selecao.Application`?

## Drivers da decisão

- **Clareza no startup**: a configuração do Wolverine deve ser auditável em um único ponto (`Program.cs` / helper compartilhado).
- **Sem mágica de classpath**: contribuidor lendo o código deve entender de onde os handlers vêm sem precisar conhecer convenções implícitas do Wolverine.
- **Modularidade**: cada módulo decide quais assemblies seus.

## Opções consideradas

- **A. `[assembly: WolverineModule<DiscoveryExtension>]` em Selecao.Application; classe `DiscoveryExtension : IWolverineExtension` chama `IncludeAssembly`.**
- **B. `opts.Discovery.IncludeAssembly(typeof(PublicarEditalCommand).Assembly)` no callback de `UseWolverineOutboxCascading` (em `Program.cs`).**
- **C. Marker interface `IApplicationAssemblyMarker` + reflection para listar assemblies referenciados.**

## Resultado da decisão

**Escolhida:** "B — `IncludeAssembly` explícito no callback, com tipo âncora **public** do assembly de Application".

`Selecao.API/Program.cs` chama:

```csharp
builder.Host.UseWolverineOutboxCascading(
    builder.Configuration,
    connectionStringName: "SelecaoDb",
    configureRouting: opts =>
    {
        opts.Discovery.IncludeAssembly(typeof(PublicarEditalCommand).Assembly);
        // ...routing específico...
    });
```

`PublicarEditalCommand` é o command público âncora — qualquer tipo public do assembly serviria, mas usar um command real torna explícita a relação "este assembly contém handlers de comandos".

Opção A seria viável mas adiciona uma classe extra em Application só para discovery, sem benefício sobre B. A foi adoptada nos testes (`CascadingTestDiscoveryExtension`) onde test handlers vivem no assembly de teste (não referenciado pelo Program.cs produtivo) e o `[assembly: WolverineModule<T>]` é a única forma de incluí-los.

Opção C é overengineering — reflection genérica vs uma linha explícita.

## Consequências

### Positivas

- Discovery é uma linha visível no callback do helper. Auditável.
- Sem dependência de convenção — quem lê o código sabe de onde vêm os handlers.
- Pattern uniforme: cada módulo `*.API` chama `IncludeAssembly` no seu callback.

### Negativas

- Adicionar novo assembly Application (ex.: `Selecao.Application.Sagas`) exige edição do `Program.cs` — fricção mínima.
- Tipo âncora (`PublicarEditalCommand`) precisa permanecer público. Hoje é convenção do projeto que commands sejam públicos (ver ADR-0048 para controllers).

### Neutras

- Quando Ingresso ganhar primeiro handler, o ADR-0040 nota o trigger para refatorar o helper aceitando `params Type[] applicationMarkers` — YAGNI até segundo consumidor (issue #198).

## Confirmação

- `Selecao.API/Program.cs` linha 98 chama `opts.Discovery.IncludeAssembly(typeof(PublicarEditalCommand).Assembly)`.
- `CascadingTestDiscoveryExtension` em `tests/.../Outbox/Cascading/` usa Opção A (test handlers no assembly de teste).

## Prós e contras das opções

### A — `[assembly: WolverineModule<T>]`

- Bom: descoberta automática quando o assembly é carregado; padrão idiomático Wolverine.
- Ruim: classe extra só pra discovery; menos visível em review.

### B — `IncludeAssembly` explícito no callback (escolhida)

- Bom: visível no Program.cs; sem classe extra.
- Ruim: adicionar assembly novo exige edição manual.

### C — Marker interface + reflection

- Bom: zero edição manual.
- Ruim: overengineering; dificulta debug; comportamento mágico.

## Mais informações

- [JasperFx Wolverine — Handler Discovery](https://wolverinefx.io/guide/handlers/discovery.html)
- ADR-0040 — Helper `UseWolverineOutboxCascading` (trigger para parametrizar IncludeAssembly)
- Origem: PR [#173](https://github.com/unifesspa-edu-br/uniplus-api/pull/173); issue [#184](https://github.com/unifesspa-edu-br/uniplus-api/issues/184)
