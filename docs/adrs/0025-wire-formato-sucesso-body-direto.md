---
status: "accepted"
date: "2026-05-03"
decision-makers:
  - "Tech Lead (CTIC)"
---

# ADR-0025: Wire format de sucesso — body é a representação direta do recurso

## Contexto e enunciado do problema

A [ADR-0023](0023-wire-formato-erro-rfc-9457.md) decidiu o wire format de erro (RFC 9457 ProblemDetails) e a [ADR-0024](0024-mapeamento-domain-error-http.md) decidiu o mapeador `DomainError → HTTP`. Falta decidir o **wire format de sucesso**: como o body de respostas 2xx é estruturado.

Há um debate clássico nessa decisão. A opção **envelope** (`{ success: true, data: {...}, error: null }`) é frequentemente proposta como conveniência para o frontend (parser único, discriminator boolean). A opção **body direto** (recurso é o JSON, sem wrapper) é o padrão de toda API pública de referência do mercado (Stripe, GitHub, AWS, Microsoft Graph, Google Cloud) e é o que `Microsoft.AspNetCore` emite nativamente.

A decisão importa para o contrato V1 porque é o ponto onde a `uniplus-api` se posiciona como API pública alinhada com convenções REST + HTTP semantics ou como API com wire format proprietário. A umbrella ([ADR-0022](0022-contrato-rest-canonico-umbrella.md), princípio 6) já estabeleceu que a conformidade com padrões abertos é binding.

## Drivers da decisão

- **Conformidade com REST + HTTP semantics ([RFC 9110](https://www.rfc-editor.org/rfc/rfc9110.html)).** Status code é a fonte de verdade primária do resultado da operação; body é a representação do recurso. Envelope com `success: bool` no body duplica esse sinal e historicamente diverge do status code real.
- **Convergência com APIs de referência.** Toda API pública institucional/comercial relevante usa status code + body direto. Auditores externos e integradores institucionais reconhecem o padrão imediatamente; envelope custom exige documentação adicional.
- **Wire format não curva para consumidor.** Ergonomia de TypeScript/Angular é responsabilidade do consumer adapter `ApiResult<T>` (ADR-0011 do `uniplus-web`), não do wire. Curvar o wire para conveniência de uma stack consumidora compromete os demais (Postman, integradores externos, análise via cURL).
- **Suporte nativo do framework.** `Microsoft.AspNetCore.Mvc` emite body direto sem adapter custom; `Microsoft.AspNetCore.OpenApi` ([ADR-0030](0030-openapi-3-1-contract-first.md)) gera schemas alinhados sem transformer extra.
- **Content negotiation funciona naturalmente.** O `Content-Type: application/vnd.uniplus.<resource>.v<N>+json` ([ADR-0028](0028-versionamento-per-resource-content-negotiation.md)) representa o recurso em si; um envelope envolveria o recurso e exigiria nome de media type composto.
- **Simetria com erro só onde faz sentido.** Erros vão em RFC 9457 (`application/problem+json`); sucessos vão como o recurso direto. A simetria que importa é **status code semântico + body apropriado** — não wrapper uniforme.

## Opções consideradas

- **A. Body é a representação direta do recurso.** Sem wrapper. Status code semântico carrega o resultado da operação.
- **B. Envelope uniforme `{ success: bool, data: T | null, error: ProblemDetails | null }`** aplicado simetricamente em 2xx e 4xx.
- **C. Envelope só em sucesso `{ data: T }`**, mantendo RFC 9457 puro em 4xx/5xx.
- **D. JSON:API `{ data: { type, id, attributes, relationships, links, ... } }`** — formato proprietário com cardinalidade explícita.

## Resultado da decisão

**Escolhida:** "A — Body é a representação direta do recurso", porque é a única opção que respeita REST + HTTP semantics, converge com toda API pública de referência, mantém o wire format livre de obrigações de ergonomia consumidora e funciona nativamente no `Microsoft.AspNetCore`.

### Status codes adotados em sucesso

A semântica obrigatória de status code em 2xx:

- **200 OK** — leitura de recurso existente, ou operação síncrona que retorna resultado no body.
- **201 Created** — criação de recurso. Header `Location` aponta para o recurso criado; body opcional contém a representação inicial (recomendado para evitar round-trip).
- **202 Accepted** — operação assíncrona aceita; body contém referência ao job/processo (ex.: id do job + endpoint para polling de status, ou URL de webhook). Padrão usado em comandos de longa duração; o mecanismo interno de drenagem (outbox transacional + Wolverine, [ADR-0004](0004-outbox-transacional-via-wolverine.md) e [ADR-0005](0005-cascading-messages-para-drenagem-de-domain-events.md)) é independente do contrato 202.
- **204 No Content** — operação que naturalmente não retorna body (ex.: idempotent update sem campos derivados).

200 é o default para leituras e operações síncronas com resultado tipado. 204 é restrito a operações sem payload de retorno — não é o caminho default para `PATCH`/`PUT`.

### Forma do body

- **Recurso único** — o body é o JSON do recurso. Ex.: `GET /editais/{id}` retorna `{ "id": "...", "numero": "...", "status": "..." }`.
- **Coleção** — o body é um array JSON do recurso. Ex.: `GET /editais` retorna `[{ "id": "...", ... }, ...]`. Cursor + paginação são propagados **exclusivamente via headers HTTP** (`Link` por RFC 5988/8288 + headers auxiliares definidos pela [ADR-0026](0026-paginacao-cursor-opaco-cifrado.md)) — JSON array não admite sibling fields no root, então qualquer metadado que precise ser carregado fora do array vai por header, nunca por wrapper de objeto.
- **Operação que produz dado derivado** — o body é o JSON do dado derivado, sem wrapper. Ex.: `POST /editais/{id}/classificar` retorna o resultado da classificação como JSON direto.

### Erros usam wire próprio (não esta ADR)

Toda response 4xx/5xx vai como `Content-Type: application/problem+json` com schema RFC 9457 ([ADR-0023](0023-wire-formato-erro-rfc-9457.md)). Esta ADR só decide a forma do body em 2xx — não há simetria de schema entre sucesso e erro porque são semanticamente coisas diferentes.

### Por que B, C e D foram rejeitadas

- **B (envelope uniforme).** Acopla protocolo (HTTP status) a payload (`success: bool`) — sinal redundante que historicamente diverge do status code real. Toda API pública de referência rejeita esse padrão; é documentado anti-pattern (referenciado em InfoQ, Phil Sturgeon "APIs You Won't Hate", e nas próprias dev guides do AWS/GitHub/Stripe). A ergonomia consumidora que motiva o envelope é endereçada pelo `ApiResult<T>` adapter no frontend.
- **C (envelope só em sucesso).** Resolve a metade simétrica (com 9457 em erro) mas perde por (i) `data:` wrapper não agrega informação útil, (ii) ferramentas de codegen produzem types `{ data: T }` em vez de `T`, dobrando indireção sem ganho, (iii) ainda exige adapter no consumidor, então a alegada simplicidade não materializa.
- **D (JSON:API).** Formato proprietário com overhead de cardinalidade. A `uniplus-api` não tem requisitos que justifiquem JSON:API (sparse fieldsets via query, relationships explícitas, embeds inline) — adoção introduziria custo de implementação e curva de aprendizagem para integradores sem benefício material.

### Esta ADR não decide

- Como o consumidor frontend transforma o body em `Result<T> | Error<ProblemDetails>` — decisão na ADR-0011 do `uniplus-web` (`ApiResult<T>` discriminated union, opção A da hierarquia umbrella).
- Como `_links` HATEOAS são embutidos no body — decisão na [ADR-0029](0029-hateoas-level-1-links.md). Esta ADR estabelece que o body é o recurso direto; HATEOAS adiciona um campo `_links` ao recurso, não envelopa.
- Como cursor de paginação é serializado — decisão na [ADR-0026](0026-paginacao-cursor-opaco-cifrado.md). Coleção continua sendo array no body; cursor vai por `Link` header + fields auxiliares fora do array.
- Como a versão do recurso é negociada — decisão na [ADR-0028](0028-versionamento-per-resource-content-negotiation.md).

## Consequências

### Positivas

- **Alinhamento REST + HTTP.** A `uniplus-api` se posiciona como API pública institucional alinhada com convenções consagradas — postura coerente com mandato da umbrella (princípio 5, API pública desde o dia 1).
- **Convergência com mercado.** Integradores que já consomem APIs de referência (Stripe/GitHub/AWS/Microsoft Graph) reconhecem o padrão sem documentação adicional.
- **Codegen limpo.** `openapi-generator-cli` e Kiota produzem types diretos `Edital`, `Inscricao`, sem indireção `EnvelopedEdital { data: Edital }`.
- **Content negotiation natural.** Media type `application/vnd.uniplus.<resource>.v<N>+json` representa o recurso de fato — sem necessidade de schema wrapping.

### Negativas

- **Consumidor precisa adapter para tratamento uniforme.** O frontend lida com 2xx (body direto) e 4xx/5xx (ProblemDetails) como shapes diferentes. Mitigação: `ApiResult<T>` discriminated union no `uniplus-web` (ADR-0011) trata isso como concern do adapter, não do wire.
- **Discussão recorrente em onboarding.** Devs que vêm de stacks com envelope (alguns frameworks Node, algumas APIs internas legacy) podem propor reabrir a decisão. Mitigação: esta ADR registra a justificativa por escrito; reaberturas exigem nova ADR `superseded by`.

### Neutras

- A escolha não impede que collections carreguem metadados auxiliares (ex.: `total`, `cursor_next`) — mas esses vão **apenas em headers HTTP** (`Link`, `X-Total-Count` ou similares definidos pela [ADR-0026](0026-paginacao-cursor-opaco-cifrado.md)). Sibling fields no root de um array JSON é sintaxe inválida; envolver o array em objeto wrapper para acomodar metadado violaria a decisão desta ADR.

## Confirmação

1. **Spectral rule no CI.** Toda response com status `2xx` no spec OpenAPI deve ter schema que **não** comece com `data` ou `success` como propriedades root sem que a entidade real tenha esse nome. Falha indica reintrodução acidental de envelope.
2. **Smoke E2E (Postman/Newman).** Cenários de sucesso conhecidos (criar edital, buscar edital por id, listar editais) verificam que o body bate com o schema do recurso direto, sem wrapper.
3. **Revisão arquitetural de PR.** Qualquer PR que adicione tipo `EnvelopedXxx` ou `Result<T>` exposto na superfície HTTP (em vez de no `Domain`/`Application`) deve ser rejeitado.

## Mais informações

- [ADR-0022](0022-contrato-rest-canonico-umbrella.md) — umbrella (princípios 5 e 6: API pública e padrões abertos).
- [ADR-0023](0023-wire-formato-erro-rfc-9457.md) — wire format de erro (complementa esta ADR).
- [ADR-0024](0024-mapeamento-domain-error-http.md) — mapper que produz ProblemDetails em erros.
- [RFC 9110 — HTTP Semantics](https://www.rfc-editor.org/rfc/rfc9110.html).
- Phil Sturgeon — *Build APIs You Won't Hate* (2015) — referência conceitual sobre rejeição de envelope.
- Documentação pública das APIs Stripe, GitHub REST, AWS, Microsoft Graph e Google Cloud — exemplos de body direto consagrado em larga escala.
