---
status: "accepted"
date: "2026-05-05"
decision-makers:
  - "Tech Lead (CTIC)"
---

# ADR-0041: Padrão de retorno `(Result, IEnumerable<object>)` em handlers Wolverine que mutam agregados

## Contexto e enunciado do problema

Handlers Wolverine que mutam agregados precisam (a) retornar um `Result` para o caller via `ICommandBus.Send<Result>` (sucesso/falha tipada) E (b) drenar `EntityBase.DomainEvents` para o bus de cascading messages, que o Wolverine persiste no outbox dentro da mesma transação do `SaveChanges` (atomicidade write+evento — ADR-0026).

Ingenuamente, isso parece exigir dispatch manual: o handler chama `_commandBus.Publish(@event)` para cada evento drenado. Esse caminho falha — o `Publish` abre nova transação fora do `IEnvelopeTransaction` ativo, perdendo atomicidade.

A solução existe na Wolverine 5.32.1: cascading messages via tupla nativa de retorno. Primeira posição vira a resposta tipada do `InvokeAsync<TResponse>`, segunda é capturada por `CaptureCascadingMessages` e persistida no outbox dentro da `IEnvelopeTransaction` da política `AutoApplyTransactions` + `EnrollDbContextInTransaction`.

## Drivers da decisão

- **Atomicidade**: write+evento na mesma transação ou nada — invariante crítico do outbox.
- **Tipagem do response**: `ICommandBus.Send<Result<Guid>>` precisa receber `Result<Guid>` no chamador, não `object`.
- **Drenagem explícita** (ADR-0026): `EntityBase.DomainEvents` é populada pelo agregado durante a operação; o handler deve drenar via `DequeueDomainEvents()` antes de retornar.
- **Sem dispatcher manual**: chamar `_commandBus.Publish(...)` quebra atomicidade.

## Opções consideradas

- **A. Handler retorna apenas `Result`; eventos drenados via dispatcher manual.**
- **B. Handler retorna tupla `(Result, IEnumerable<object>)`; Wolverine captura cascading.**
- **C. Handler retorna `Result`; scraper EF Core (`PublishDomainEventsFromEntityFrameworkCore`) drena `EntityBase.DomainEvents` ao final do `SaveChanges`.**

## Resultado da decisão

**Escolhida:** "B — tupla `(Result, IEnumerable<object>)`", porque é a única opção que cumpre simultaneamente atomicidade + tipagem + drenagem explícita.

Forma canônica do handler (slice de referência: `PublicarEditalCommandHandler` em `src/selecao/Unifesspa.UniPlus.Selecao.Application/Commands/Editais/`):

```csharp
public static async Task<(Result, IEnumerable<object>)> Handle(
    PublicarEditalCommand command,
    IEditalRepository repository,
    IUnitOfWork unitOfWork,
    CancellationToken cancellationToken)
{
    Edital? edital = await repository.ObterPorIdAsync(command.Id, cancellationToken);
    if (edital is null)
        return (Result.Failure(EditalErrors.NaoEncontrado), []);

    Result<Edital> publicarResult = edital.Publicar();
    if (publicarResult.IsFailure)
        return (Result.Failure(publicarResult.Error!), []);

    await unitOfWork.SaveChangesAsync(cancellationToken);

    return (Result.Success(), edital.DequeueDomainEvents().Cast<object>());
}
```

`DequeueDomainEvents` (ADR-0034 wrap imutável; ADR-0026 drenagem explícita) tira snapshot + limpa a coleção atomicamente. O `Cast<object>()` é necessário porque o tipo cascading do Wolverine é `IEnumerable<object>`, não `IEnumerable<IDomainEvent>`.

A opção C foi explicitamente rejeitada na ADR-0026 — `PublishDomainEventsFromEntityFrameworkCore<EntityBase>` está desligado por configuração no `WolverineOutboxConfiguration`. Razão: o scraper EF lê eventos no final da transação SQL, momento em que mensagens de capabilities (sagas, processadores) já podem ter sido despachadas ignorando esses eventos. A drenagem explícita pelo handler é a fonte única.

## Consequências

### Positivas

- Atomicidade write+evento garantida pelo `IEnvelopeTransaction` ativo.
- Tipagem preservada: `ICommandBus.Send<Result<Guid>>` continua tipado.
- Drenagem visível no código do handler — review humano vê os eventos sendo emitidos.
- `Cast<object>()` é o único custo técnico — uma chamada O(n) no final.

### Negativas

- Handler que muta agregado mas não retorna `IEnumerable<object>` deixa eventos órfãos. Mitigação: revisar PR de novos handlers cascading; padrão documentado em CLAUDE.md do `uniplus-api`.
- Tupla 2-arity tem custo cognitivo para devs vindos de MediatR (que usa `IRequestHandler<TRequest, TResponse>` mono-aridade).

### Neutras

- A decisão pode ser revisitada quando Wolverine introduzir um wrapper tipo `IDomainResult<T>` que combine os dois canais. Hoje o tuple é o idiomatic.

## Confirmação

- `PublicarEditalCommandHandler` em `Selecao.Application/Commands/Editais/` segue o pattern.
- `EditalPublicadoSubscriberHandler` (test handler em `tests/.../Outbox/Cascading/`) consome o evento drenado.
- `PublicarEditalEndpointTests` valida cascading fim-a-fim (PR #173, ADR-0026).

## Prós e contras das opções

### A — `Result` + dispatcher manual

- Bom: sintaxe simples.
- Ruim: quebra atomicidade write+evento.

### B — Tupla `(Result, IEnumerable<object>)` (escolhida)

- Bom: atomicidade + tipagem + drenagem explícita.
- Ruim: tuple 2-arity custa cognitivo.

### C — `Result` + scraper EF

- Bom: handler mais simples (sem segundo retorno).
- Ruim: timing do scraper é depois do dispatch de capabilities; eventos podem ser perdidos.

## Mais informações

- [JasperFx Wolverine — Cascading Messages](https://wolverinefx.io/guide/handlers/cascading.html)
- ADR-0026 — Outbox transacional via Wolverine
- ADR-0034 — DequeueDomainEvents (wrap imutável)
- Origem: spike S10 cascading; PR [#173](https://github.com/unifesspa-edu-br/uniplus-api/pull/173); issue [#182](https://github.com/unifesspa-edu-br/uniplus-api/issues/182)
