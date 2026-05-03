---
status: "accepted"
date: "2026-05-03"
decision-makers:
  - "Tech Lead (CTIC)"
---

# ADR-0024: Mapeamento `DomainError → HTTP` via `IDomainErrorMapper` registry

## Contexto e enunciado do problema

A camada `Domain` (e parcialmente `Application`) retorna falhas como `Result.Failure(DomainError code, message)` — padrão estabelecido como obrigatório no `CLAUDE.md` do repo e em uso em todos os handlers Wolverine das slices existentes. `DomainError` é um value object plano com `code` (string) e `message` (string), sem nenhuma referência a HTTP, status code ou ProblemDetails.

A [ADR-0023](0023-wire-formato-erro-rfc-9457.md) decidiu que toda resposta de erro vai como ProblemDetails RFC 9457, com `code` (taxonomia `uniplus.<modulo>.<razao>`), `title`, `status`, `type` e demais extensions. Falta decidir **quem traduz** `DomainError(code, message)` na response HTTP correta — sem essa decisão, cada controller resolveria o mapeamento local, com risco de divergência entre slices, vazamento de detalhes internos via `BadRequest(domainError)` e quebra do princípio Clean Architecture (umbrella, princípio 4) que mantém `Domain` e `Application` HTTP-agnósticos.

A umbrella também listou explicitamente que esta ADR é onde fitness tests ArchUnit que enforçam `Domain` / `Application` sem dependência de `Microsoft.AspNetCore.*` ficam detalhados.

## Drivers da decisão

- **Clean Architecture preservada (umbrella, princípio 4).** `Domain` e `Application` não podem importar `Microsoft.AspNetCore.*`. Status code, content-type, location header e demais primitivas HTTP são responsabilidade exclusiva da camada `API`.
- **Fonte única da verdade.** O mapeamento `code → (status, type, title)` é a base do catálogo público em `developers.uniplus.unifesspa.edu.br/erros/{code}` (ADR-0001 do `uniplus-developers`). Se cada controller mapear localmente, o catálogo dessincroniza.
- **Auditabilidade.** Auditores externos e clientes institucionais precisam verificar que o `code` retornado pelo endpoint bate com a entrada do catálogo. Registry centralizado é trivialmente inspecionável e exportável.
- **Testabilidade.** Mapeador deve ser injetável em testes — controllers e middleware devem permitir fake/stub do `IDomainErrorMapper` sem subir o pipeline ASP.NET inteiro.
- **Detecção de drift no PR.** Adicionar um `code` novo na `Domain` sem registrar no mapper deve quebrar o build, não vazar para produção como erro genérico 500.

## Opções consideradas

- **A. Registry `IDomainErrorMapper` na camada `API` + `ResultExtensions.ToActionResult()`** + fitness tests ArchUnit enforçando boundary.
- **B. Annotation no `DomainError`** decorando `code` com `[HttpStatus(404)]` ou similar.
- **C. Switch statement por módulo dentro de cada controller** mapeando `code → status` localmente.
- **D. Convention-based via reflection** (ex.: `code` que termina em `.nao_encontrado` vira 404, `.invalido` vira 422).

## Resultado da decisão

**Escolhida:** "A — Registry `IDomainErrorMapper` na camada `API` + `ResultExtensions.ToActionResult()`", porque é a única opção que preserva Clean Architecture, dá fonte única de verdade auditável e habilita fitness tests automatizados de boundary.

### Forma do contrato

A camada `API` define a interface:

```csharp
public interface IDomainErrorMapper
{
    DomainErrorMapping Resolve(string code);
}

public sealed record DomainErrorMapping(int Status, string Type, string Title);
```

A implementação canônica `DomainErrorMappingRegistry` é uma `IReadOnlyDictionary<string, DomainErrorMapping>` registrada via `AddSingleton<IDomainErrorMapper>` no startup. O dicionário é populado por arquivos de registração por módulo (ex.: `SelecaoDomainErrors.cs`, `IngressoDomainErrors.cs`) — granularidade exata fica para a tarefa de implementação.

A camada `API` provê uma extension method na assembly:

```csharp
public static IActionResult ToActionResult<T>(this Result<T> result, IDomainErrorMapper mapper);
```

Controllers consomem o mapper apenas via essa extension. Nenhum controller chama `Resolve` diretamente nem instancia `ProblemDetails` à mão. A construção do payload RFC 9457 (com `traceId`, `instance`, `errors[]`, `legal_reference`) é responsabilidade do `GlobalExceptionMiddleware` evoluído + dessa extension — não dos controllers individuais.

### Boundary enforçado por ArchUnit

Três fitness tests ([ADR-0012](0012-archunitnet-como-fitness-tests-arquiteturais.md), pacote `TngTech.ArchUnitNET`):

1. **Cobertura do registry.** Verificação estática no CI (Roslyn analyzer dedicado ou equivalente — escolha de implementação) garante que todo `code` construído em `DomainError(...)` ou `Result.Failure(...)` no código-fonte de `Domain` e `Application` está registrado no `DomainErrorMappingRegistry`. Falha lista os codes órfãos. ArchUnitNET cobre os testes 2 e 3 abaixo, que são puramente estruturais.
2. **Direção de dependência.** Tipos em `Unifesspa.UniPlus.*.Domain` e `Unifesspa.UniPlus.*.Application` não dependem de qualquer tipo em `Microsoft.AspNetCore.*` ou em `Unifesspa.UniPlus.*.API`.
3. **Controllers sem acesso direto a `DomainError`.** Controllers retornam apenas `IActionResult` produzido por `result.ToActionResult(mapper)` ou `ObjectResult` tipado para o sucesso — nunca `BadRequest(domainError)`, `NotFound(domainError.Message)` ou similar.

### Domain e Application permanecem como já estão

Esta ADR **não muda** a forma como `Result<T>` e `DomainError` são construídos no `Domain` e `Application` — apenas formaliza que essa é a única forma e que a tradução para HTTP está num lugar fixo. O slice canônico continua sendo o módulo Seleção (`PublicarEditalCommandHandler` etc.).

### Esta ADR não decide

- Como o catálogo público em `/erros/{code}` consome o registry — decisão na ADR-0001 do `uniplus-developers`.
- Como o `GlobalExceptionMiddleware` constrói o payload final RFC 9457 com `traceId` e `instance` — detalhe de implementação que segue [ADR-0023](0023-wire-formato-erro-rfc-9457.md).
- Em que assembly da camada `API` vive o `DomainErrorMappingRegistry` (Selecao.API, Ingresso.API, ou um pacote shared `Unifesspa.UniPlus.Api.Core`) — decisão de organização da implementação, não arquitetural.

## Consequências

### Positivas

- **Clean Architecture preservada.** Único lugar onde HTTP encosta na semântica de erro é a camada `API`. Mover slices, refatorar handlers ou trocar framework HTTP não exige tocar `Domain`/`Application`.
- **Fonte única de verdade.** Registry serve simultaneamente os controllers, o catálogo público, a documentação OpenAPI (via transformer da [ADR-0030](0030-openapi-3-1-contract-first.md)) e os logs estruturados. Drift entre essas vistas é tecnicamente impossível.
- **Drift detectado em PR.** Adicionar `code` novo no `Domain` sem registrar quebra o ArchUnit no CI — chega como erro acionável no PR, nunca silencia para produção.
- **Testes mais simples.** Stub do `IDomainErrorMapper` cobre cenários de mapping em testes de controller sem precisar do registry real.

### Negativas

- **Burocracia em cada novo `code`.** Cada erro novo no `Domain` exige uma linha no arquivo de registro do módulo + uma entrada no catálogo do portal. O ArchUnit garante que a linha do registry existe; a entrada do portal vira critério de revisão de PR.
- **Risco de over-mapping.** Tentação de criar codes muito granulares ("um por exception"). Mitigação: revisão de PR; o `code` deve representar uma causa estável e referenciável publicamente, não um ponto de implementação.
- **Migração de codes existentes.** Os 14 codes em uso no `Domain` do módulo Seleção (espalhados por `Edital.*`, `Inscricao.*`, `Cpf.*`, `Email.*`, `NomeSocial.*`, `NumeroEdital.*`, `FormulaCalculo.*`, `PeriodoInscricao.*`, `NotaFinal.*`) precisam ser migrados para a taxonomia `uniplus.<modulo>.<razao>` e registrados — entra no escopo do PR pilot que migra o `EditalController` e os value objects relacionados.

### Neutras

- A interface fica trivialmente substituível por implementações alternativas (ex.: registry carregado de YAML, registry distribuído por Configuration). A canônica é estática em código por simplicidade.

## Confirmação

1. **Verificações estáticas no CI** — três checks nomeados na seção "Boundary enforçado por ArchUnit". O check 1 (cobertura) roda como Roslyn analyzer ou equivalente; os checks 2 e 3 (direção de dependência e ausência de `DomainError` em controllers) rodam em `tests/Unifesspa.UniPlus.*.ArchTests` via ArchUnitNET ([ADR-0012](0012-archunitnet-como-fitness-tests-arquiteturais.md)).
2. **Cobertura do mapper.** Suite de testes de unidade verifica que `DomainErrorMappingRegistry.Resolve` cobre 100% dos codes coletados pelo ArchUnit. Falha indica registry desincronizado com o código real.
3. **Smoke E2E (Postman/Newman).** Cenários conhecidos de erro batem `code` retornado contra valor esperado e contra entrada do catálogo `/erros/{code}` quando o portal estiver no ar ([ADR-0023](0023-wire-formato-erro-rfc-9457.md), Confirmação 3).
4. **Revisão arquitetural de PR.** Qualquer PR que adicione `code` novo no `Domain` ou `Application` deve incluir a entrada no registry e a entrada-rascunho no catálogo do portal. Critério explícito no checklist do PR template.

## Mais informações

- [ADR-0022](0022-contrato-rest-canonico-umbrella.md) — umbrella do contrato canônico V1 (princípio 4: Clean Architecture HTTP-agnóstica).
- [ADR-0023](0023-wire-formato-erro-rfc-9457.md) — wire format consumidor desta decisão.
- [ADR-0002](0002-clean-architecture-com-quatro-camadas.md) — fronteiras de camada.
- [ADR-0012](0012-archunitnet-como-fitness-tests-arquiteturais.md) — base dos fitness tests citados.
- [ADR-0003](0003-wolverine-como-backbone-cqrs.md) — handlers Wolverine que retornam `Result<T>`.
- `CONTRIBUTING.md` — convenção de `Result<T>` / `DomainError` no `Domain`/`Application`.
