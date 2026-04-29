---
status: "accepted"
date: "2026-04-28"
decision-makers:
  - "Tech Lead (CTIC)"
---

# ADR-0005: Cascading messages como drenagem canônica de domain events

## Contexto e enunciado do problema

A ADR-0004 fixou outbox transacional via Wolverine + EF Core. A primeira configuração ensaiada drenava domain events com `options.PublishDomainEventsFromEntityFrameworkCore<EntityBase>(entity => entity.DomainEvents)` — caminho que dependia do fork local Wolverine `5.32.1-pr2586` (PR upstream 2586).

Após o merge dessa configuração, a thread upstream `JasperFx/wolverine#2585` trouxe recomendação explícita do maintainer Jeremy D. Miller:

> *"If you're greenfield though, I'd recommend using Wolverine more idiomatically and just returning cascaded messages from the handler rather than the magic EF Core scraping thing that's popular in MediatR circles. I'd still hold the Wolverine way will lead to more maintainable code."*

O `uniplus-api` é greenfield. O Spike S10 validou empiricamente o caminho idiomático em 10 dimensões objetivas (acoplamento, testabilidade unitária, comportamento implícito, atomicidade transacional, alta disponibilidade, performance, manutenibilidade longitudinal, independência do fork local, entre outras). Resultado: 16 testes verdes (13 da matriz S0–S9 mais 3 do S10), com vitória clara para cascading messages.

## Drivers da decisão

- Idiomatismo do framework — caminho recomendado pelo próprio maintainer.
- Testabilidade unitária — handlers passam a ser função pura, sem fixture com Postgres real.
- Independência do fork local — cascading roda em `WolverineFx 5.32.1` oficial, eliminando dívida técnica do feed `vendors/nuget-local/`.
- Visibilidade da drenagem — a entrega de eventos vira artefato no `return` do handler, não configuração distante no boot.

## Opções consideradas

- Cascading messages do retorno do handler (`Task<IEnumerable<object>>` + `DequeueDomainEvents()`)
- `PublishDomainEventsFromEntityFrameworkCore<EntityBase>(...)` no boot
- Pipeline middleware customizado para drenagem

## Resultado da decisão

**Escolhida:** cascading messages do retorno do handler como caminho canônico de drenagem de domain events.

`PublishDomainEventsFromEntityFrameworkCore<EntityBase>(...)` é removido da configuração de produção. Handlers que mutam agregados passam a seguir o padrão:

```csharp
public sealed class PublicarEditalHandler
{
    public static async Task<IEnumerable<object>> Handle(
        PublicarEditalCommand command,
        SelecaoDbContext db,
        CancellationToken ct)
    {
        var edital = Edital.Criar(command.Numero, command.Titulo, command.Tipo);
        edital.Publicar();
        db.Editais.Add(edital);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        return edital.DequeueDomainEvents().Cast<object>();
    }
}
```

`DequeueDomainEvents()` é helper canônico no `EntityBase` — combina snapshot atômico e clear da coleção interna. Padronização defensiva contra republicação acidental em cenários onde o agregado sobreviva ao escopo do handler (cache distribuído, sagas).

Convenções:

- Handlers que mutam agregado retornam `Task<IEnumerable<object>>`, drenando via `DequeueDomainEvents()`.
- Outros formatos suportados pelo Wolverine (`OutgoingMessages`, tipo único, tupla, `IAsyncEnumerable<T>`) só com justificativa local.
- Handlers que não emitem eventos (puros side-effects, leitura, validação) mantêm assinatura `Task` ou `Task<TResult>`.
- O extension de cada módulo (`AddSelecaoModule`, `AddIngressoModule`) continua responsável pelo roteamento (`PublishMessage<T>().ToPostgresqlQueue(...)` / `.ToKafkaTopic(...)`), mas não pela drenagem.

`PublishDomainEventsFromEntityFrameworkCore` permanece caminho válido do framework como fallback para casos legados específicos (migração de código MediatR-style, agregados de bibliotecas externas) — exige justificativa em ADR adicional.

## Consequências

### Positivas

- Estilo idiomático Wolverine, recomendado pelo maintainer.
- Testes unitários puros para handlers — feedback loop drasticamente menor.
- Eliminação imediata da dependência do fork local (volta para `WolverineFx 5.32.1` oficial).
- Imune por design ao bug do PR 2586 — cascading não passa pelo `ChangeTracker`.
- Habilita futura redução de visibilidade de `EntityBase.DomainEvents` (encapsulamento).

### Neutras

- Performance ~26% pior por invocação, em magnitude absoluta sub-1ms — irrelevante para o perfil HTTP+I/O do projeto.

### Negativas

- Cada handler que muta agregado ganha ~5 linhas (assinatura + return).
- Convenção exige disciplina — sempre retornar coleção de eventos. Fica documentado em guia operacional.
- Migração dos handlers já existentes na codebase requer PR específico.

## Confirmação

- Suíte `Category=OutboxCascading` em `uniplus-api` cobre os cenários do Spike S10 com 3/3 testes verdes em ambiente com Wolverine 5.32.1 oficial.
- Suíte completa `Category=OutboxCapability ∪ Category=OutboxCascading` mantém 16/16 verde.
- Pull request review verifica que handlers que persistem agregado retornam `IEnumerable<object>` via `DequeueDomainEvents()`.

## Mais informações

- ADR-0003 define Wolverine como backbone CQRS.
- ADR-0004 define o outbox transacional (configuração de transport, persistence, retenção de dead letters).
- [Wolverine — Cascading Messages](https://wolverinefx.net/guide/handlers/cascading.html)
- [JasperFx/wolverine#2585 — thread upstream com recomendação do maintainer](https://github.com/JasperFx/wolverine/issues/2585)
- **Origem:** revisão da ADR interna Uni+ ADR-026 (não publicada).
