---
status: "proposed"
date: "2026-05-04"
decision-makers:
  - "Tech Lead (CTIC)"
---

# ADR-0031: Decoding de cursor opaco no boundary HTTP, não em handlers de Application

## Contexto e enunciado do problema

A [ADR-0026](0026-paginacao-cursor-opaco-cifrado.md) fixa o **formato** do cursor de paginação (AES-GCM cifrado, payload opaco, propagação por `Link` header), mas é silenciosa sobre **em que camada da Clean Architecture o decode acontece**. A decisão é sutil porque a cifragem precisa de `IUniPlusEncryptionService`, registrado em `Infrastructure.Core/Cryptography/`, e a regra de dependência da [ADR-0002](0002-clean-architecture-com-quatro-camadas.md) proíbe Application de depender de Infrastructure.

O problema concreto apareceu na implementação de `GET /api/editais` (story #288): o `CursorEncoder` vive em `Infrastructure.Core/Pagination/`. Há três formas de chegar do cursor opaco ao `(afterId, limit)` que o handler precisa para consultar o repositório, e cada uma faz uma afirmação diferente sobre o que **é** um cursor: detalhe de wire format ou parte do contrato semântico da query.

A decisão é binding para todos os endpoints de coleção paginada (`GET /editais`, futuros `/inscricoes`, `/recursos`, `/chamadas`).

## Drivers da decisão

- **Regra de dependência da [ADR-0002](0002-clean-architecture-com-quatro-camadas.md).** Application depende apenas de Domain e SharedKernel. Qualquer arranjo que importe `Infrastructure.Core` em um handler quebra o fitness test R3 ([ADR-0012](0012-archunitnet-como-fitness-tests-arquiteturais.md)).
- **Coerência com o decode de outros formatos de wire.** Content negotiation (vendor MIME, [ADR-0028](0028-versionamento-per-resource-content-negotiation.md)) acontece em `VendorMediaTypeAttribute` na camada API. Decoding de query strings, headers HTTP e body JSON também é boundary. Cursor é mais um decoder de wire encoding.
- **Single Responsibility.** Handler responde "lista editais com (afterId, limit)"; controller responde "traduz HTTP para a query e formata a resposta". Misturar cursor → handler dilui as responsabilidades.
- **Custo de manutenção e teste.** Handler com primitivas é trivial de testar sem HTTP; handler com `ICursorCodec` exige fake/mock do codec em todo teste.
- **Preservação do princípio DIP.** A inversão de dependência continua válida via outras abstrações já existentes (`IEditalRepository`, `ICommandBus`, `IQueryBus`); não há valor incremental em adicionar mais uma só por causa do cursor.

## Opções consideradas

- **A. Decode no boundary HTTP (controller).** Controller injeta `CursorEncoder` direto, decoda antes de despachar a query. Query trafega só primitivas (`Guid? AfterId`, `int Limit`).
- **B. Interface `ICursorCodec` em `Application.Abstractions`, implementação em `Infrastructure.Core`.** Handler depende da abstração e decoda dentro de si; cursor errors viram `DomainError` no `Result<Page<T>>`.
- **C. Dependência direta de `CursorEncoder` no handler.** Handler importa `Infrastructure.Core/Pagination/` diretamente. Inválida — viola R3.

## Resultado da decisão

**Escolhida:** "A — Decode no boundary HTTP", porque tratar o cursor como wire encoding (e não como contrato semântico da query) preserva o princípio "uma decisão por camada" sem custo arquitetural visível, mantém o handler trivialmente testável e fica coerente com onde o sistema já decoda outros formatos de wire (vendor MIME, query strings, headers).

### Forma do contrato de query

A query CQRS recebe primitivas, não o cursor:

```csharp
public sealed record ListarEditaisQuery(Guid? AfterId, int Limit) : IQuery<ListarEditaisResult>;
```

`AfterId == null` significa "primeira página". O handler é puramente sobre paginação keyset; nada sabe sobre cifragem, base64 ou TTL.

### Responsabilidades do controller

O controller (camada API) é responsável por:

1. **Decodificar cursor.** `CursorEncoder.TryDecodeAsync` retorna `CursorDecodeResult` discriminado (Success / Invalid / Expired); cada caso vira `DomainError` que passa pelo `IDomainErrorMapper` da [ADR-0024](0024-mapeamento-domain-error-http.md) — `cursor_invalido` (400), `cursor_expirado` (410).
2. **Validar limit.** Faixa 1..100; fora disso vira `Cursor.LimitInvalido` (422).
3. **Validar `ResourceTag` do payload.** Cursor cifrado para `inscricoes` não é aceito em `/editais` mesmo que decifre — protege contra reuso cross-resource.
4. **Despachar a query** com `(AfterId, Limit)` primitivos.
5. **Encodar cursor da próxima página** a partir do `ProximoAfterId` retornado pelo handler, e montar `PageLinks` + header `Link` ([ADR-0026](0026-paginacao-cursor-opaco-cifrado.md)).

### Esta ADR não decide

- A forma do cursor cifrado, TTL, mecanismo de chave — escopo da [ADR-0026](0026-paginacao-cursor-opaco-cifrado.md).
- Mapeamento de erros do cursor para HTTP (400/410/422) — escopo das [ADR-0023](0023-wire-formato-erro-rfc-9457.md) e [ADR-0024](0024-mapeamento-domain-error-http.md).
- Versionamento de API por content negotiation — escopo da [ADR-0028](0028-versionamento-per-resource-content-negotiation.md).
- Como introduzir um caller não-HTTP que precise paginar (worker, CLI batch). Quando aparecer, a decisão é revisitar com proposta de promover `ICursorCodec` para `Application.Abstractions/Pagination/` (opção B desta ADR), sem breaking change na API pública. Não há custo retroativo: o cursor já é uma string opaca cuja origem o handler ignora.

## Consequências

### Positivas

- **Application desacoplada de `Infrastructure.Core`.** O fitness test R3 ([ADR-0012](0012-archunitnet-como-fitness-tests-arquiteturais.md)) detecta automaticamente qualquer regressão.
- **Handlers triviais de testar.** Não precisam de fake de `ICursorCodec`; recebem `Guid?` e `int`.
- **Coerência entre decoders de wire.** Vendor MIME, body JSON, query strings, cursor — todos decoded no mesmo boundary, mesma camada. Reduz carga cognitiva.
- **Cursor errors se enquadram no fluxo `Result + DomainError + IDomainErrorMapper` existente** ([ADR-0023](0023-wire-formato-erro-rfc-9457.md), [ADR-0024](0024-mapeamento-domain-error-http.md)) sem precisar de pipeline novo.

### Negativas

- **Handler não tem visão completa "listar editais" como conceito de protocolo.** Quem lê só o handler vê `(AfterId, Limit)` sem saber que a página é navegada por cursor opaco — dependência de leitura cruzada com o controller para entender o end-to-end.
- **Caller não-HTTP precisa replicar o decode.** Se aparecer um worker que pagine editais, ele importa `Infrastructure.Core` (legítimo, está em camada externa) e replica a lógica. Mitigação documentada na seção "Esta ADR não decide": promover para `ICursorCodec` na primeira aparição real do caso.
- **Lógica de "decode + validate ResourceTag + map para DomainError" no controller é repetível** — cada novo endpoint paginado vai duplicar 5–10 linhas. Mitigação no roadmap: extrair em filtro/helper na camada API quando a duplicação justificar (regra do "três").

### Neutras

- A decisão não impede `Page<T>` e `LinkHeaderBuilder` viverem em `Infrastructure.Core/Pagination/` — eles são wire format, mesma camada do cursor. Não há push para mover esses tipos.
- A decisão é **revisitável sem custo retroativo**: promover o decode para handler via `ICursorCodec` não muda o wire format do cursor nem a API pública. Refactor é interno.

## Confirmação

1. **Fitness test R3** ([ADR-0012](0012-archunitnet-como-fitness-tests-arquiteturais.md), `Stage1ArchitectureRulesTests.Camadas_RespeitamDirecaoDeDependencia`) — qualquer importação de `Infrastructure.Core` em assembly de Application falha o build. Esta é a confirmação primária; é executada no CI a cada PR.
2. **Code review checklist.** Em handlers CQRS de queries paginadas, verificar que parâmetros são primitivas (`Guid?`, `int`, etc.) e não strings de cursor opaco.
3. **Inspeção manual no controller pilot** (`EditalController.Listar`) — bloco de decode de cursor + validação de `ResourceTag` é referência para replicação. Quando um terceiro endpoint paginado aparecer, abrir spike de extração para helper compartilhado.

## Mais informações

- [ADR-0002](0002-clean-architecture-com-quatro-camadas.md) — regra de dependência das quatro camadas; origem da restrição.
- [ADR-0003](0003-wolverine-como-backbone-cqrs.md) — handlers CQRS convention-based; modelo de assinatura que esta ADR mantém limpa.
- [ADR-0012](0012-archunitnet-como-fitness-tests-arquiteturais.md) — fitness tests R1/R2/R3 que enforçam mecanicamente.
- [ADR-0023](0023-wire-formato-erro-rfc-9457.md) — wire format de erro consumido por `cursor_invalido`/`cursor_expirado`/`cursor_limit_invalido`.
- [ADR-0024](0024-mapeamento-domain-error-http.md) — `IDomainErrorMapper` registry usado pelo controller para 400/410/422.
- [ADR-0026](0026-paginacao-cursor-opaco-cifrado.md) — formato e ciclo de vida do cursor (esta ADR é complementar, não revisa nada).
- [ADR-0028](0028-versionamento-per-resource-content-negotiation.md) — content negotiation no boundary; precedente arquitetural de decoder de wire na camada API.
- PR #310 (story #288) — implementação pilot que motivou esta ADR.
