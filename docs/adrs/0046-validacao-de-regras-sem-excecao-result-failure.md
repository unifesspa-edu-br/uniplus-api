---
status: "accepted"
date: "2026-05-05"
decision-makers:
  - "Tech Lead (CTIC)"
---

# ADR-0046: Validação de regras de negócio sem exceção — `Result.Failure(DomainError)` para fluxo esperado

## Contexto e enunciado do problema

Handlers e métodos de domínio precisam sinalizar falhas previsíveis (entidade não encontrada, transição de estado inválida, regra de negócio violada). Há dois caminhos possíveis:

- **Exceção**: `throw new EditalNaoEncontradoException(id)`. Capturada por middleware global que mapeia para HTTP 404.
- **Result pattern**: `return Result.Failure(EditalErrors.NaoEncontrado)`. Caller verifica `IsFailure` e mapeia via `IDomainErrorMapper`.

A pergunta: qual o default?

## Drivers da decisão

- **Performance**: exceções têm custo de stack unwinding (~10-100µs); `Result` é uma struct/record, custo zero.
- **Explicitude**: `Task<Result>` sinaliza no tipo do método "isso pode falhar"; `Task<T>` pode lançar mas o tipo não diz.
- **Pipeline previsível**: handlers Wolverine usam middleware que inspeciona retorno; com Result, middleware loga falha estruturada sem custar exception.
- **Reservar exceção para o inesperado**: `GlobalExceptionMiddleware` (em `Infrastructure.Core/Middleware/`) captura tudo que escapa — esse caminho deveria ser raro, não comum.

## Opções consideradas

- **A. Exceções para tudo (transição inválida lança).**
- **B. `Result.Failure(DomainError)` para fluxo esperado; exceções para falhas inesperadas (DB indisponível, bug).**
- **C. Híbrido por caso — handler decide.**

## Resultado da decisão

**Escolhida:** "B — Result para fluxo esperado, exceção para inesperado".

Slice canônico (`PublicarEditalCommandHandler`):

```csharp
public static async Task<(Result, IEnumerable<object>)> Handle(...)
{
    Edital? edital = await repository.ObterPorIdAsync(...);
    if (edital is null)
        return (Result.Failure(EditalErrors.NaoEncontrado), []);

    Result<Edital> publicarResult = edital.Publicar();
    if (publicarResult.IsFailure)
        return (Result.Failure(publicarResult.Error!), []);

    // ...
}
```

Controller MVC mapeia `Result` para HTTP via extension `result.ToActionResult(_mapper)` que consulta `IDomainErrorMapper` (ADR-0024) e retorna `application/problem+json` (RFC 9457, ADR-0023). Códigos canônicos:

- `uniplus.selecao.edital.nao_encontrado` → 404
- `uniplus.selecao.edital.ja_publicado` → 422

Falhas inesperadas (DbUpdateException, NullReferenceException de bug) sobem para `GlobalExceptionMiddleware` que retorna `uniplus.internal.unexpected` 500. Esse caminho é o exception fallback, não o canal regular.

## Consequências

### Positivas

- Performance: custo zero em fluxos esperados (sem stack unwinding).
- Pipeline middleware Wolverine pode logar falhas tipadas sem catch.
- Tipo do método assinala "pode falhar" via `Result<T>` no retorno.

### Negativas

- Caller obrigado a verificar `IsFailure` antes de acessar `.Value` — disciplina sobre tipo. C#  expressões de pattern matching ajudam.
- Curva de aprendizado para devs vindos de stacks java-style com exceções para "esperado".

### Neutras

- A decisão é consistente com ADR-0024 (mapeamento DomainError → HTTP) e ADR-0023 (RFC 9457). Esta ADR formaliza retroativamente o uso obrigatório do Result pattern para regras de negócio.

## Confirmação

- `Selecao.Application/Commands/Editais/PublicarEditalCommandHandler` segue o pattern.
- `EditalErrors` em `Selecao.Domain/Errors/` declara códigos canônicos.
- `SelecaoDomainErrorRegistration` em `Selecao.API/Errors/` mapeia para HTTP.
- Testes `PublicarEditalEndpointTests` validam 404 (`uniplus.selecao.edital.nao_encontrado`) e 422 (`uniplus.selecao.edital.ja_publicado`).
- `GlobalExceptionMiddleware` em `Infrastructure.Core/Middleware/` captura exceções inesperadas.

## Prós e contras das opções

### A — Exceções para tudo

- Bom: estilo C# legado/java; imediato.
- Ruim: stack unwinding caro; tipo do método não declara falha; pipeline Wolverine custa wrap.

### B — Result para esperado, exceção para inesperado (escolhida)

- Bom: performance + explicitude + pipeline limpo.
- Ruim: disciplina sobre IsFailure.

### C — Híbrido caso a caso

- Bom: máxima flexibilidade.
- Ruim: convenção viola — reviewer precisa inferir; bugs por inconsistência.

## Mais informações

- ADR-0023 — ProblemDetails RFC 9457 canônico
- ADR-0024 — Mapeamento DomainError → HTTP
- Origem: PR [#173](https://github.com/unifesspa-edu-br/uniplus-api/pull/173); issue [#188](https://github.com/unifesspa-edu-br/uniplus-api/issues/188)
