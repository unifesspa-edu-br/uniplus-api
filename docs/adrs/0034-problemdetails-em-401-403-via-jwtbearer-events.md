---
status: "accepted"
date: "2026-05-05"
decision-makers:
  - "Tech Lead (CTIC)"
---

# ADR-0034: ProblemDetails RFC 9457 em 401/403 via `JwtBearerEvents.OnChallenge`/`OnForbidden`

## Contexto e enunciado do problema

[ADR-0023](0023-problemdetails-rfc-9457-canonico-para-todo-erro.md) tornou `application/problem+json` o body canônico de **todo** erro do contrato Uni+. Endpoints de domínio cumpriam o contrato via `Result<T>.ToActionResult(IDomainErrorMapper)`; exceções não tratadas eram convertidas pelo `GlobalExceptionMiddleware`. Ficou descoberto, porém, o caminho mais frequente em produção: **falha de autenticação**.

Endpoints `[Authorize]` (incluindo `/api/auth/me` e `/api/profile/me` no shared kernel, todos os controllers MVC dos módulos) retornavam **401 com body vazio** quando o JWT estava ausente, malformado, expirado ou de issuer/audience inválidos. Razão: o `JwtBearerHandler.HandleChallengeAsync` do ASP.NET Core 10 escreve cabeçalho `WWW-Authenticate`, define status 401 e **finaliza a resposta** antes que `StatusCodePagesMiddleware` ou `ExceptionHandlerMiddleware` (os middlewares que `services.AddProblemDetails()` ativa) tenham chance de agir. Mesma dinâmica para 403 emitido pela `AuthorizationMiddleware` quando o principal está autenticado mas sem permissão.

O Codex P1 do PR #320 sinalizou o débito: o baseline OpenAPI declarava `description: "Unauthorized"` sem `content`, então o linter Spectral aceitava o contrato — mas clientes externos parsing problem+json quebravam ao receber body vazio. Comentário inline em `UniPlusOperationTransformer.cs:84` registrou o follow-up; `contracts/README.md#roadmap` listou-o como item 1.1.

## Drivers da decisão

- **Cumprir ADR-0023 em todos os caminhos de erro do contrato**, sem exceção para auth.
- **Consistência com clientes gerados** (TypeScript SDK, Postman, Spectral): media type real precisa bater com o declarado no spec.
- **Não inventar contrato falso no transformer OpenAPI**: o `UniPlusOperationTransformer` só coage `application/json` → `application/problem+json` quando há `content` declarado. Sem solução no runtime, declarar conteúdo no spec geraria divergência runtime/contract.
- **Reuso entre produção e test host**: o body 401/403 emitido em testes integrados precisa ser byte-equivalente ao de produção, senão regressões passam despercebidas.
- **LGPD**: nenhum detalhe de validação JWT (claim faltante, valor de token, motivo criptográfico) pode entrar no body — só log estruturado.

## Opções consideradas

- **A. `services.AddProblemDetails()` global, sem hook custom.** Confiar em middlewares default ASP.NET Core para escrever o body em qualquer 4xx/5xx.
- **B. `JwtBearerEvents.OnChallenge`/`OnForbidden` custom escrevendo via `IProblemDetailsService.WriteAsync`.** Compor handlers que `HandleResponse()` e emitem o body explicitamente.
- **C. `UseStatusCodePagesWithReExecute("/error/{0}")` + endpoint de erro sintético.** Re-executar o pipeline para status codes não tratados.

## Resultado da decisão

**Escolhida:** "B — `JwtBearerEvents.OnChallenge`/`OnForbidden` custom + `IProblemDetailsService`", porque é a única que efetivamente intercepta o challenge antes da resposta ser finalizada.

[dotnet/aspnetcore#44100](https://github.com/dotnet/aspnetcore/issues/44100) e a documentação oficial confirmam: o `JwtBearerHandler` é um short-circuit selado — `AddProblemDetails` sozinha (Opção A) **não consegue** interceptar challenge, e `UseStatusCodePages` (Opção C) é skipado quando `Response.HasStarted` (que o handler dispara). A única intervenção viável é o evento.

A implementação compõe o evento já existente (`JwtBearerLoggingEvents.WithStructuredLogging`) com o novo `JwtBearerProblemDetailsEvents.WithProblemDetails`, preservando logs estruturados e adicionando o body. O writer compartilhado `AuthenticationProblemDetailsWriter` é usado tanto pela infra produtiva quanto pelo `TestAuthHandler` dos testes integrados — único caminho de produção do payload, zero divergência runtime/test.

`services.AddProblemDetails()` continua registrado: ele provê `IProblemDetailsService` ao DI scope (com `CustomizeProblemDetails` aplicado) que o writer usa via `RequestServices.GetService<IProblemDetailsService>()`. O writer tem fallback manual caso o serviço não esteja registrado — robustez para projetos auxiliares que reusem o helper.

### Type URLs canônicas

| Status | `type` (URL absoluto)                                                       | `code`                            | `title`             |
| ------ | --------------------------------------------------------------------------- | --------------------------------- | ------------------- |
| 401    | `https://uniplus.unifesspa.edu.br/errors/uniplus.auth.unauthorized`         | `uniplus.auth.unauthorized`       | `Não autenticado`   |
| 403    | `https://uniplus.unifesspa.edu.br/errors/uniplus.auth.forbidden`            | `uniplus.auth.forbidden`          | `Acesso negado`     |

Extensions: `code`, `traceId` (extraído do `Activity.Current` ou GUID v7 fallback), `instance` no formato `urn:uuid:<v7>`. Nenhum detalhe da falha de validação JWT é exposto no body.

### Header `WWW-Authenticate` em 401

`HandleResponse()` desliga o caminho default do `JwtBearerHandler`, que normalmente popula `WWW-Authenticate` (RFC 7235 §4.1 e RFC 9110 §11.6.1 exigem o header em toda 401). O `AuthenticationProblemDetailsWriter.WriteUnauthorizedAsync` re-emite `WWW-Authenticate: Bearer` (sem `realm`/`error`/`error_description`) — só o desafio mínimo necessário para conformidade RFC, alinhado com a política de não expor motivo de falha. 403 não recebe o header (não é challenge).

## Consequências

### Positivas

- Endpoints `[Authorize]` cumprem RFC 9457 sem instrumentação por endpoint — basta declarar `.ProducesProblem(401)` no contrato OpenAPI.
- Clientes gerados a partir do spec passam a parsear o body corretamente.
- `UniPlusOperationTransformer` passa a coagir 401/403 para `application/problem+json` consistentemente, eliminando o "follow-up" que pendurava o comportamento.
- `TestAuthHandler` produz body byte-equivalente ao de produção via `AuthenticationProblemDetailsWriter`, então testes integrados validam o contrato real (pré-existente: status; novo: shape do body).

### Negativas

- O hook custom é mais código a manter do que o caminho default. Mitigação: o hook é uma composição estática isolada (`WithProblemDetails`), sem branch lógica; mudanças futuras de shape passam pelo `AuthenticationProblemDetailsWriter` único.
- Custo cognitivo: um dev novo pode esperar que `AddProblemDetails()` "funcione" para 401 e descobrir que precisa do hook. Mitigação: comentário inline em `OidcAuthenticationConfiguration` aponta para esta ADR.

### Neutras

- O `UniPlusOperationTransformer` permanece com a lógica defensiva "não inventa content em response vazia": se um endpoint futuro declarar 401 sem `.ProducesProblem`, o spec não mente sobre o body. A regra continua válida.

## Confirmação

- **Testes integrados** (`AuthEndpointsTests`, `ProfileEndpointsTests` em ambos os módulos) verificam status 401 + `Content-Type: application/problem+json` + shape do body (status, type, code, traceId, instance).
- **Baselines OpenAPI** (`contracts/openapi.{selecao,ingresso}.json`) declaram `application/problem+json` com `$ref ProblemDetails` para os endpoints shared. Drift check em CI falha se o spec gerado divergir.
- **Spectral** (`uniplus-error-response-uses-problem-details`) valida que toda resposta de erro nos baselines aponta para `application/problem+json`.

## Prós e contras das opções

### A — `AddProblemDetails()` apenas

- Bom, porque é o caminho default documentado pela Microsoft para erros não tratados.
- Ruim, porque `JwtBearerHandler` short-circuita antes dos middlewares que ele ativa (`StatusCodePagesMiddleware`, `ExceptionHandlerMiddleware`) e o body permanece vazio.

### B — `JwtBearerEvents.OnChallenge`/`OnForbidden` + `IProblemDetailsService`

- Bom, porque é a única intervenção que efetivamente roda antes do response ser finalizado.
- Bom, porque permite reuso do shape via `AuthenticationProblemDetailsWriter` entre produção e teste.
- Ruim, porque exige hook explícito por scheme (cada `AddXxxJwt`/scheme adicional precisa do `WithProblemDetails`). Mitigado por compor dentro de `AddOidcAuthentication`.

### C — `UseStatusCodePagesWithReExecute("/error/{0}")`

- Bom em teoria, porque desacopla shape do body do scheme.
- Ruim, porque `Response.HasStarted == true` quando `JwtBearerHandler` finaliza o challenge, então o middleware de re-execute é skipado — equivalente a A em comportamento.
- Ruim, porque adiciona endpoint sintético `/error/{statusCode}` ao roteamento, contaminando o spec OpenAPI.

## Mais informações

- [RFC 9457 — Problem Details for HTTP APIs](https://datatracker.ietf.org/doc/html/rfc9457)
- [Microsoft Learn — Configure JWT bearer authentication (.NET 10)](https://learn.microsoft.com/aspnet/core/security/authentication/configure-jwt-bearer-authentication?view=aspnetcore-10.0)
- [Microsoft Learn — Handle errors in ASP.NET Core (.NET 10)](https://learn.microsoft.com/aspnet/core/fundamentals/error-handling?view=aspnetcore-10.0#problem-details)
- [dotnet/aspnetcore#44100 — Write custom authentication failure information to response body](https://github.com/dotnet/aspnetcore/issues/44100)
- [Andrew Lock — A look behind JwtBearer authentication middleware](https://andrewlock.net/a-look-behind-the-jwt-bearer-authentication-middleware-in-asp-net-core/)
- [ADR-0023](0023-problemdetails-rfc-9457-canonico-para-todo-erro.md) — ProblemDetails como contrato canônico
- Origem: follow-up registrado em PR [#320](https://github.com/unifesspa-edu-br/uniplus-api/pull/320) (Codex P1) e [`contracts/README.md#roadmap`](../../contracts/README.md)
