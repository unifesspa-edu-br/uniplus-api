---
status: "accepted"
date: "2026-05-05"
decision-makers:
  - "Tech Lead (CTIC)"
---

# ADR-0048: Controllers MVC em projetos `*.API` devem ser `public`, com CA1515 suprimido por justificativa

## Contexto e enunciado do problema

ASP.NET Core MVC descobre controllers via `ControllerFeatureProvider.IsController(TypeInfo type)`, que exige `type.IsPublic == true`. Controllers marcados como `internal sealed` (default sugerido pelo analyzer `CA1515` — "Consider making public types internal") ficam silenciosamente fora do roteamento — nenhum endpoint registrado, nenhum diagnóstico claro em runtime. Foi exatamente isso que causou a regressão capturada em #173.

Por outro lado, o analyzer `CA1515` é geralmente saudável — reduz superfície pública desnecessária. Suprimi-lo globalmente perde valor.

A pergunta: como reconciliar a obrigação MVC ("público") com o analyzer geral?

## Drivers da decisão

- **Discovery MVC obrigatória**: controllers precisam ser `public` por contrato do framework.
- **Auditabilidade**: a supressão deve indicar **por que** está sendo feita, não ser CYA silencioso.
- **Cobertura de regressão**: além da supressão correta, sentinela de teste que protege contra recidiva.

## Opções consideradas

- **A. `[SuppressMessage("Performance", "CA1515:...")]` inline em cada controller, com justification explicando MVC discovery.**
- **B. `<NoWarn>CA1515</NoWarn>` no csproj de cada `*.API`.**
- **C. Suprimir CA1515 globalmente em `.editorconfig`.**

## Resultado da decisão

**Escolhida:** "A — `[SuppressMessage]` inline com `Justification` explicando MVC discovery".

Slice canônico (`EditalController`):

```csharp
[ApiController]
[Route("api/editais")]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "ASP.NET Core ControllerFeatureProvider só descobre controllers public; sem isso o MVC ignora a classe e nenhum endpoint é registrado.")]
public sealed class EditalController : ControllerBase
{
    // ...
}
```

A justificativa explica o porquê — review humano consegue auditar.

A opção B (`<NoWarn>` no csproj) silencia globalmente o analyzer no projeto, perdendo sinal de classes utilitárias `internal` válidas. Opção C agrava: silencia em toda a solution.

A sentinela de discovery MVC (`ControllersMvcDiscoverySentinelTests` em PR #325, issue #199) protege contra recidiva: se algum dev no futuro mudar `public` para `internal`, o teste falha cedo com mensagem direcional.

## Consequências

### Positivas

- Analyzer CA1515 continua ativo para o resto do código — sinal preservado.
- Justificativa inline é auditável.
- Sentinela CI protege contra recidiva.

### Negativas

- Boilerplate `[SuppressMessage]` em cada novo controller. Mitigação: pattern consolidado em CLAUDE.md do `uniplus-api`; templates de Stories indicam o snippet.
- Devs que não conhecem o pattern podem tentar `internal sealed` por reflexo do CA1515 — sentinela falha cedo, mas custa loop dev.

### Neutras

- A decisão é específica para controllers MVC. Outras supressões CA1515 (test factories `internal sealed` que precisam ser `public` para fixture genérica) seguem o mesmo padrão de inline justify.

## Confirmação

- `EditalController` em `src/selecao/Unifesspa.UniPlus.Selecao.API/Controllers/` segue o pattern.
- `ControllersMvcDiscoverySentinelTests` em `tests/Unifesspa.UniPlus.Selecao.IntegrationTests/Hosting/` protege contra recidiva.
- ADR-0036 (Controllers MVC vs Minimal API) referencia este ADR para o padrão de declaração.

## Prós e contras das opções

### A — `[SuppressMessage]` inline (escolhida)

- Bom: justificativa auditável; analyzer ativo no resto.
- Ruim: boilerplate por controller.

### B — `<NoWarn>` no csproj

- Bom: zero boilerplate em controllers.
- Ruim: silencia globalmente; perde sinal em utilitários internos.

### C — `.editorconfig` global

- Bom: silencia em toda solution.
- Ruim: perda total do sinal CA1515.

## Mais informações

- [Microsoft Learn — `ControllerFeatureProvider` (.NET 10)](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.mvc.controllers.controllerfeatureprovider?view=aspnetcore-10.0)
- [CA1515 — Consider making public types internal](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1515)
- ADR-0036 — Controllers MVC para negócio + Minimal API para shared
- Issue [#199](https://github.com/unifesspa-edu-br/uniplus-api/issues/199) — sentinela controllers MVC discovery (PR [#325](https://github.com/unifesspa-edu-br/uniplus-api/pull/325))
- Origem: PR [#173](https://github.com/unifesspa-edu-br/uniplus-api/pull/173); issue [#190](https://github.com/unifesspa-edu-br/uniplus-api/issues/190)
