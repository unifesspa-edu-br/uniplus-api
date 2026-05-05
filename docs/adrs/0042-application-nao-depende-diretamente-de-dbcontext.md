---
status: "accepted"
date: "2026-05-05"
decision-makers:
  - "Tech Lead (CTIC)"
---

# ADR-0042: Application layer não depende diretamente de DbContext — sempre via repository + Unit of Work

## Contexto e enunciado do problema

Exemplos canônicos do JasperFx Wolverine no guia de EF Core sugerem injetar `DbContext` direto no handler:

```csharp
public static async Task Handle(
    SomeCommand cmd,
    AppDbContext db,            // ← Wolverine resolve via DI
    CancellationToken ct)
{
    var entity = await db.Entities.FindAsync(...);
    // mutate
    await db.SaveChangesAsync(ct);
}
```

Esse padrão é tentador (zero indireção, EF features visíveis) mas **viola Clean Architecture** (ADR-0002): `DbContext` é uma classe de Infrastructure, e Application layer não pode depender de Infrastructure.

A pergunta: aderir ao exemplo do framework ou manter Clean Architecture?

## Drivers da decisão

- **ADR-0002 — Clean Architecture obrigatória**: Application → Domain + SharedKernel; nunca Infrastructure.
- **Fitness test R3 do contrato V1** (PR #319): regra arquitetural detecta dependências Application → Infrastructure e falha o build.
- **Test surface**: handler que injeta `DbContext` precisa de PG real ou InMemory provider; handler que injeta `IRepository` aceita NSubstitute mock — testável em unit, não só em integration.
- **Ergonomics**: o exemplo do Wolverine economiza ~3 linhas; a manutenibilidade longo prazo paga isso muitas vezes.

## Opções consideradas

- **A. Aderir ao exemplo Wolverine — DbContext direto no handler.**
- **B. Application depende de `IEditalRepository` + `IUnitOfWork`; Infrastructure implementa via EF Core.**
- **C. Híbrido — `DbContext` direto em queries simples (read-only); repository em commands.**

## Resultado da decisão

**Escolhida:** "B — repository + Unit of Work em todos os handlers que tocam persistência".

`Selecao.Application/Commands/Editais/PublicarEditalCommandHandler` injeta `IEditalRepository` (interface em `Selecao.Domain/Interfaces/`) + `IUnitOfWork` (em `Application.Abstractions/Interfaces/`). Implementações vivem em `Selecao.Infrastructure/Persistence/Repositories/`.

A regra arquitetural R3 (fitness test em `Selecao.ArchTests`) protege contra recidiva — se algum handler em `Selecao.Application` adicionar `using Microsoft.EntityFrameworkCore` ou referenciar `SelecaoDbContext`, o build falha.

A opção C foi rejeitada porque introduz divergência: para o reviewer, ler "este é query, OK depender direto de DbContext, mas aquele é command" exige memorizar a regra. B é uma única convenção universal.

## Consequências

### Positivas

- Application unit-testável com mocks (NSubstitute) — não precisa de PG ou InMemory provider.
- Mudança de provider (Postgres → outro RDBMS, ou Marten) afeta só Infrastructure.
- Fitness test R3 protege contra recidiva.

### Negativas

- Handler tem 1 indireção a mais que o exemplo Wolverine — leitura inicial demora um beat extra.
- Repository pode virar "anti-pattern" se ganhar métodos de query específicos demais. Mitigação: queries complexas vão para `IQueryHandler` Wolverine, não inflar o repository.

### Neutras

- A decisão é consistente com ADR-0002 e R3 — esta ADR formaliza retroativamente o que a fitness test já garante.

## Confirmação

- `PublicarEditalCommandHandler` em `Selecao.Application/Commands/Editais/` injeta `IEditalRepository` + `IUnitOfWork`.
- Fitness test R3 (PR #319) detecta dependências Application → Infrastructure.
- `IEditalRepository` em `Selecao.Domain/Interfaces/`; `EditalRepository` em `Selecao.Infrastructure/Persistence/Repositories/`.

## Prós e contras das opções

### A — DbContext direto

- Bom: zero indireção; EF features completas.
- Ruim: viola Clean Architecture; não unit-testável; trava troca de provider.

### B — Repository + UoW (escolhida)

- Bom: Clean Architecture preservada; unit-testável; flexibilidade de provider.
- Ruim: 1 indireção a mais.

### C — Híbrido

- Bom: ergonomia em queries simples.
- Ruim: divergência de pattern entre handlers; reviewer precisa memorizar a regra.

## Mais informações

- ADR-0002 — Clean Architecture obrigatória
- Fitness test R3 — Application não depende de Infrastructure (PR [#319](https://github.com/unifesspa-edu-br/uniplus-api/pull/319))
- Origem: PR [#173](https://github.com/unifesspa-edu-br/uniplus-api/pull/173); issue [#183](https://github.com/unifesspa-edu-br/uniplus-api/issues/183)
