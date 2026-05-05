---
status: "accepted"
date: "2026-05-05"
decision-makers:
  - "Tech Lead (CTIC)"
---

# ADR-0036: Controllers MVC `[ApiController]` para endpoints de negócio + Minimal API restrita a shared/técnicos

## Contexto e enunciado do problema

A API tem dois shapes possíveis para endpoints HTTP em ASP.NET Core 10: controllers MVC com `[ApiController]` (`EditalController`, futuras `InscricaoController` etc.) ou Minimal API endpoints (`MapGet("/me", ...)`). Cada modelo traz custo cognitivo se misturados sem regra. Sem padrão claro, contribuidores oscilariam entre os dois e a superfície de testes de roteamento + filtros + autorização ficaria inconsistente.

Os PRs #136 (`PublicarEditalCommand` handler) e #173 (`EditalController` cascading fim-a-fim) consolidaram, na prática, um padrão que não estava em ADR. Esta ADR registra a convenção retroativamente.

## Drivers da decisão

- **Versionamento per-resource via vendor MIME** (ADR-0028) — `[VendorMediaType(Resource = "edital", Versions = [1])]` é um filtro `IActionFilter`, exige `[ApiController]`/MVC pipeline. Minimal API não invoca filtros MVC.
- **Idempotency-Key opt-in** (ADR-0027) — `[RequiresIdempotencyKey]` é também um `IActionFilter`. Mesmo motivo.
- **Model binding cursor pagination** (ADR-0031) — `[FromCursor]` model binder integra com pipeline MVC, não Minimal API.
- **Autorização por atributo** — `[Authorize(Policy = "...")]` em controllers MVC é declarativo e auditável; Minimal API requer composição via `RequireAuthorization()` em chains, frágil para review.
- **Endpoints técnicos compartilhados** (`/api/auth/me`, `/api/profile/me`, `/health`) — sem domínio, sem versionamento, sem idempotency. Minimal API é mais leve aqui.

## Opções consideradas

- **A. Tudo Minimal API.** Consistência única, performance ligeiramente melhor.
- **B. Tudo controllers MVC.** Consistência única, integração natural com filtros.
- **C. Híbrido por categoria — MVC para negócio, Minimal API para técnico/shared.**

## Resultado da decisão

**Escolhida:** "C — Híbrido por categoria", porque é a única opção que casa as restrições reais do projeto.

Endpoints de negócio (criação, listagem, transição de estado de agregados) precisam de filtros MVC (vendor MIME, Idempotency-Key, FluentValidation), model binders (cursor pagination), autorização por atributo, e contratos OpenAPI ricos via `[ProducesResponseType]`. Minimal API não suporta esse pacote sem reimplementar. Adotar A custaria reescrever 4-5 mecanismos do contrato V1.

Endpoints shared (auth, profile, health) são triviais, sem versionamento e sem mutação de estado. Adotar B aqui inflaria a superfície (`AuthController`, `ProfileController`, `HealthController` cada um com seu próprio `[ApiController]`) sem benefício — Minimal API expressa essa intenção em 5 linhas.

### Critério canônico

| Endpoint | Modelo | Razão |
|---|---|---|
| `EditalController.Listar/Criar/Publicar` | MVC `[ApiController]` | filtros + cursor pagination + Idempotency-Key + vendor MIME |
| `/api/auth/me` (`MapSharedAuthEndpoints`) | Minimal API | sem domínio, sem versionamento |
| `/api/profile/me` (`MapSharedProfileEndpoints`) | Minimal API | mesma justificativa |
| `/health` (`MapHealthChecks`) | Minimal API | infra |

Futuros controllers de negócio (Inscricao, Chamada, Matricula) seguem o pattern do `EditalController`.

## Consequências

### Positivas

- Custo cognitivo reduzido — contribuidor sabe qual modelo usar pelo tipo do endpoint.
- Reuso completo do contrato V1 (filtros, model binders, atributos OpenAPI) em endpoints de negócio.
- Endpoints técnicos permanecem leves.

### Negativas

- Dois pipelines no mesmo Program.cs — pequeno custo de orquestração (ambos rodam atrás do mesmo `UseAuthentication/Authorization`).
- Testes de integração precisam saber qual modelo está sendo testado para escolher entre `EndpointDataSource` (MVC) ou `RequestDelegate` direto (Minimal).

### Neutras

- Decisão pode ser revisitada quando MVC ou Minimal API ganharem features que tornem um deles dominante. Hoje não há horizon disso.

## Confirmação

- `EditalController` em `src/selecao/Unifesspa.UniPlus.Selecao.API/Controllers/` segue o pattern (PR #173).
- `MapSharedAuthEndpoints` e `MapSharedProfileEndpoints` em `src/shared/Unifesspa.UniPlus.Infrastructure.Core/Authentication/`/`Profile/`.
- `ControllersMvcDiscoverySentinelTests` (PR #325, issue #199) protege contra recidiva de controllers `internal sealed` que sairiam silenciosamente do roteamento.

## Prós e contras das opções

### A — Tudo Minimal API

- Bom: consistência única, performance ligeiramente melhor.
- Ruim: precisa reimplementar filtros, model binders, vendor MIME, Idempotency-Key — 4-5 mecanismos.

### B — Tudo controllers MVC

- Bom: integração natural com filtros, autorização declarativa.
- Ruim: inflaciona endpoints triviais (auth/profile/health).

### C — Híbrido por categoria

- Bom: encaixa nas restrições do contrato V1 sem reimplementar nada.
- Ruim: dois pipelines convivendo, teste de integração precisa saber distinguir.

## Mais informações

- [Microsoft Learn — Controller-based APIs vs Minimal APIs (.NET 10)](https://learn.microsoft.com/aspnet/core/fundamentals/apis?view=aspnetcore-10.0)
- ADR-0027 — Idempotency-Key store PostgreSQL
- ADR-0028 — Versionamento per-resource via content negotiation
- ADR-0030 — OpenAPI 3.1 contract-first
- ADR-0031 — Decoding de cursor opaco no boundary HTTP
- Origem: PR [#136](https://github.com/unifesspa-edu-br/uniplus-api/pull/136) e [#173](https://github.com/unifesspa-edu-br/uniplus-api/pull/173); issue [#177](https://github.com/unifesspa-edu-br/uniplus-api/issues/177)
