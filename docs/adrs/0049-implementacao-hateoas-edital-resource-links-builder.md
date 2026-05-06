---
status: "proposed"
date: "2026-05-06"
decision-makers:
  - "Tech Lead (CTIC)"
---

# ADR-0049: Implementação de HATEOAS Level 1 em `EditalDto` via `IResourceLinksBuilder<TDto>` na camada API

## Contexto e enunciado do problema

A [ADR-0029](0029-hateoas-level-1-links.md) decidiu que toda resposta de recurso single da `uniplus-api` carrega `_links` mínimo (`self` sempre, navigation links opcionais). A decisão é canônica mas explicitamente deixou em aberto a *forma de implementação* (§"Esta ADR não decide": *"Implementação concreta do builder de links na camada API — extension method, helper service ou middleware. Decisão de implementação."*).

A primeira aplicação concreta — `EditalDto` em resposta de `GET /api/editais/{id}` — força essa decisão. O slice `EditalController` hoje devolve `Ok(edital)` sem `_links`, e o consumer frontend (`uniplus-web`, F8 do Milestone B) reportou o gap em [uniplus-api#334](https://github.com/unifesspa-edu-br/uniplus-api/issues/334).

A questão é tripla: **(a) onde gerar os links** (Domain, Application ou API), **(b) como evitar URLs hardcoded** (`LinkGenerator` vs strings), **(c) qual contrato compartilhado** entre futuros builders (Inscrição, Recurso, Classificação).

## Drivers da decisão

- **Boundary-only.** URLs são detalhe da camada HTTP. Domain (`Edital`) e Application (`EditalDto`) não conhecem rotas. ADR-0029 §"URIs relativas" reforça: link generation é responsabilidade do servidor HTTP.
- **DI-aware.** ASP.NET Core 10 expõe `LinkGenerator` como singleton DI; reutilizar é trivial e blinda contra alterações no `[Route]` attribute do controller.
- **Pattern reusável.** Inscrição/Recurso/Classificação vão precisar do mesmo padrão. Um contrato genérico evita 3-4 reinvenções diferentes.
- **Sem reflection nem metadata custom.** Pipeline behaviors (Wolverine middleware) que pós-processam DTOs marcados por interface acoplam infra ao formato dos DTOs e exigem reflection runtime — over-engineering para o número de recursos atual.
- **Coerência com decisões já tomadas.** ADR-0025 (body como representação direta) implica `_links` é campo do DTO, não wrapper externo. ADR-0030 (OpenAPI 3.1 contract-first) é a fonte de verdade de operações; action links não duplicam.
- **Vedação binding herdada.** ADR-0029 §"Esta ADR não decide" proíbe action links em V1 (`publicar`, `cancelar`, etc.); operações de mutação são descobertas via OpenAPI.

## Opções consideradas

- **A. Interface genérica `IResourceLinksBuilder<TDto>` por recurso, registrada como singleton; controller chama `builder.Build(dto)` após o handler retornar e anexa via `dto with { Links = ... }`.**
- **B. Extension method estático `EditalDto.WithLinks(LinkGenerator generator)`** — sem DI service, lógica inline no DTO. Application camada passa a depender de `Microsoft.AspNetCore.Routing` (viola Clean Architecture).
- **C. `IResultFilter` ou `IAsyncActionFilter` que intercepta toda response e anexa `_links`** baseado em interface marker no DTO (`IHasLinks`). Reflection runtime + precedência confusa com outros filters (vendor MIME, Idempotency).
- **D. Pipeline behavior Wolverine que envelopa qualquer query handler retornando DTO marcado.** Acopla infra ao formato dos DTOs; ainda usa reflection.
- **E. Mapping no handler (`ObterEditalQueryHandler`) recebendo `LinkGenerator` injetado.** Application depende de `Microsoft.AspNetCore.Routing` — viola Clean Architecture (mesma falha de B).

## Resultado da decisão

**Escolhida:** "A — `IResourceLinksBuilder<TDto>` no boundary HTTP", porque é a única opção que:

1. Mantém **Application/Domain limpos** de dependências de URL routing (sem viés HTTP em DTOs ou handlers).
2. **Não usa reflection** nem behaviors implícitos — fluxo é explícito no controller (`dto with { Links = builder.Build(dto) }`).
3. **Replica trivialmente** para Inscrição/Recurso/Classificação: cada recurso ganha um `XxxLinksBuilder : IResourceLinksBuilder<XxxDto>` registrado uma vez no `Program.cs`.
4. **Composta com filtros existentes** (vendor MIME, Idempotency) sem precedência ambígua — execução acontece dentro do action method, antes do `Ok(dto)`.

### Forma do contrato

```csharp
namespace Unifesspa.UniPlus.Infrastructure.Core.Hateoas;

public interface IResourceLinksBuilder<in TDto> where TDto : class
{
    /// <summary>
    /// Constrói o dicionário _links para o recurso. Chave em snake_case ASCII;
    /// valor é URI relativa começando em /. self sempre presente
    /// (invariante ADR-0029).
    /// </summary>
    IReadOnlyDictionary<string, string> Build(TDto dto);
}
```

### Implementação canônica (`EditalLinksBuilder`)

```csharp
internal sealed class EditalLinksBuilder : IResourceLinksBuilder<EditalDto>
{
    private readonly LinkGenerator _linkGenerator;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public EditalLinksBuilder(LinkGenerator linkGenerator, IHttpContextAccessor httpContextAccessor)
    {
        // ... null guards
        _linkGenerator = linkGenerator;
        _httpContextAccessor = httpContextAccessor;
    }

    public IReadOnlyDictionary<string, string> Build(EditalDto dto)
    {
        // GetPathByAction com HttpContext respeita ambient PathBase (proxy
        // reverso, app.UsePathBase("/foo")). Sem HttpContext (jobs/webhooks
        // futuros invocando o builder fora de request scope), cai num path
        // sem PathBase. Em ambos os casos, path é relativo (sem scheme/host)
        // — invariante "URIs relativas à raiz da API" (ADR-0029).
        HttpContext? httpContext = _httpContextAccessor.HttpContext;

        string self = httpContext is not null
            ? _linkGenerator.GetPathByAction(httpContext, action: nameof(EditalController.ObterPorId),
                controller: "Edital", values: new { id = dto.Id })!
            : _linkGenerator.GetPathByAction(action: nameof(EditalController.ObterPorId),
                controller: "Edital", values: new { id = dto.Id })!;
        // ... idem para collection

        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["self"] = self,
            ["collection"] = collection,
        };
    }
}
```

### Por que `IHttpContextAccessor` e não passar `HttpContext` explicitamente?

A interface `IResourceLinksBuilder<TDto>.Build(TDto)` só recebe o DTO — não o `HttpContext`. Isso preserva:

- **Testabilidade isolada** — unit tests do builder podem mockar `IHttpContextAccessor` sem precisar fabricar um `HttpContext` válido.
- **Reuso fora de request scope** — jobs/webhooks futuros podem invocar o builder; quando `HttpContext` é `null`, builder cai no path-without-PathBase (correto para esses contextos).
- **Singleton lifetime preservado** — `IHttpContextAccessor` é seguro como singleton (acessa o contexto do request via `AsyncLocal`).

Alternativa rejeitada: `Build(TDto, HttpContext)` força todo caller a passar contexto, quebra a interface genérica e impede reuso fora de request.

### Localização das classes

- **`Unifesspa.UniPlus.Infrastructure.Core.Hateoas.IResourceLinksBuilder<TDto>`** — contrato cross-module em `Infrastructure.Core` (não em `SharedKernel` porque é convenção HTTP, não primitivo de domínio).
- **`Unifesspa.UniPlus.Selecao.API.Hateoas.EditalLinksBuilder`** — implementação por recurso, vive na camada API do módulo dono do recurso (mesma assembly do controller). Próximas: `InscricaoLinksBuilder`, `RecursoLinksBuilder`, `ClassificacaoLinksBuilder`.

### Registro DI

```csharp
// Program.cs
builder.Services.AddSingleton<
    IResourceLinksBuilder<EditalDto>,
    EditalLinksBuilder>();
```

Singleton porque o builder encapsula apenas o `LinkGenerator` (também singleton); função pura sem estado mutável.

### Composição no controller

```csharp
public async Task<IActionResult> ObterPorId(Guid id, CancellationToken cancellationToken)
{
    EditalDto? edital = await _queryBus.Send(new ObterEditalQuery(id), cancellationToken);
    if (edital is null) return NotFound();

    EditalDto editalComLinks = edital with { Links = _linksBuilder.Build(edital) };
    return Ok(editalComLinks);
}
```

### Esta ADR não decide

- **`_links` em coleção.** ADR-0029 §"Coleção" decidiu que coleção usa header `Link` (RFC 5988/8288) — não `_links` no body.
- **Relações futuras `inscricoes`/`classificacao` para Edital.** Adicionar quando esses endpoints existirem; entram no `EditalLinksBuilder` por edição direta (não exige nova ADR — ADR-0029 §"Catálogo completo de relações por recurso — declarado por slice na implementação").
- **Controle fino de quando emitir `_links`** (ex.: omitir em endpoints internos chamados em loop). Para V1, inclusão é incondicional. Reabrir se telemetria mostrar custo.
- **Versionamento de `_links` quando vendor MIME bumpear** (`v2` muda a forma do `_links`?). Postergado até a primeira mudança vendor MIME real acontecer.

### Por que B, C, D e E foram rejeitadas

- **B (extension method).** Application camada precisaria importar `Microsoft.AspNetCore.Routing` — viola regra de dependência (Clean Architecture: Application não conhece API).
- **C (`IResultFilter`).** Reflection sobre interface marker; precedência de filtros já é complexa (vendor MIME, Idempotency, validação); risco de bugs sutis com a chain.
- **D (Wolverine pipeline behavior).** Acopla messaging à camada de transporte (HTTP); ainda usa reflection; precedência confusa com middleware existentes (validation, logging).
- **E (handler com LinkGenerator).** Mesma falha de B — Application depende de routing HTTP. Adicionalmente, handlers passam a ter assinatura inconsistente (queries que retornam DTOs single precisam de DI extra que queries de coleção não precisam).

## Consequências

### Positivas

- **Pattern explícito e replicável.** `XxxLinksBuilder : IResourceLinksBuilder<XxxDto>` cobre todos os recursos futuros sem ADRs novas.
- **Domain/Application limpos.** Zero acoplamento HTTP nessas camadas.
- **Composição transparente.** `dto with { Links = ... }` é leitura direta no controller; sem reflection oculta.
- **Refator de URL não quebra builders.** `LinkGenerator` lê do route table; mudar `[Route("api/editais")]` propaga automaticamente.
- **Testável isolado.** Builder pode ser unit-testado mockando `LinkGenerator` (NSubstitute); integration test valida o end-to-end.

### Negativas

- **Boilerplate por recurso.** Cada DTO single ganha um `XxxLinksBuilder` + registro DI. Aceitável: ~25 linhas por recurso, replicação mecânica.
- **`EditalDto` ganha campo opcional `Links`.** Records não amam campos opcionais init-only (`with` é a única forma de setar pós-construção). Aceitável: pattern records permite `dto with { Links = ... }`.
- **Controller chama o builder explicitamente.** Possível esquecer de chamar em endpoint novo. Mitigação: integration test do endpoint cobre `_links.self` (regression imediata).

### Neutras

- **`IReadOnlyDictionary<string, string>` em vez de tipo dedicado.** ADR-0029 define `_links` como objeto string→string; usar primitivos evita um wrapper redundante. Type-safety adicional não justifica complexidade.

## Confirmação

1. **Integration test `ObterEditalEndpointTests`** — `GET /api/editais/{id}` retorna body com `_links.self` apontando para `/api/editais/{id}` e `_links.collection` apontando para `/api/editais`. Inclui assertion negativa: `_links.publicar` ausente (vedação ADR-0029).
2. **Code review** rejeita endpoint novo de recurso single sem `IResourceLinksBuilder<TDto>` invocado.
3. **Spectral rule** já enforça `_links.self` no schema OpenAPI quando declarado (ADR-0029 §"Confirmação"); regenerar spec após mudança propaga validação ao CI.

## Mais informações

- [ADR-0029](0029-hateoas-level-1-links.md) — HATEOAS Level 1 binding (decisão canônica de `_links`).
- [ADR-0030](0030-openapi-3-1-contract-first.md) — OpenAPI como fonte de verdade das operações (motivo para action links não estarem em `_links`).
- [ADR-0025](0025-wire-formato-sucesso-body-direto.md) — body como representação direta (motivo para `_links` ser campo do DTO, não wrapper).
- [ADR-0028](0028-versionamento-per-resource-content-negotiation.md) — vendor MIME (versionamento por recurso; futura mudança pode requerir versionamento de `_links`).
- [Microsoft Docs — LinkGenerator](https://learn.microsoft.com/aspnet/core/fundamentals/routing#linkgenerator) — geração de URI singleton DI.
- [Issue uniplus-api#334](https://github.com/unifesspa-edu-br/uniplus-api/issues/334) — gap reportado pelo consumer (uniplus-web F8) que motivou esta ADR.
- [PR uniplus-web#201](https://github.com/unifesspa-edu-br/uniplus-web/pull/201) — frontend que tentou usar `_links.publicar` por interpretação errada da ADR-0029; será limpo em PR follow-up.
