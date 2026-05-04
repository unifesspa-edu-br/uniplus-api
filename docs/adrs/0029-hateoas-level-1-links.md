---
status: "accepted"
date: "2026-05-03"
decision-makers:
  - "Tech Lead (CTIC)"
---

# ADR-0029: HATEOAS Level 1 — `_links` mínimo embutido no recurso

## Contexto e enunciado do problema

Respostas de recurso single da `uniplus-api` precisam carregar referências para recursos relacionados (edital → suas inscrições, inscrição → seu edital, recurso → decisão). Sem essas referências, clientes hardcoded URIs e qualquer reorganização de URL no servidor quebra todos os consumidores. Sem identificação canônica de **si mesmo** dentro do payload (campo `self`), respostas embutidas (em logs, dumps, integrações) ficam ambíguas — não é claro de qual instância o JSON veio sem inspecionar a request original.

A [ADR-0025](0025-wire-formato-sucesso-body-direto.md) decidiu que body de recurso single é a representação direta — sem envelope, sem wrapper. A [ADR-0026](0026-paginacao-cursor-opaco-cifrado.md) decidiu que navegação em coleção vai por `Link` header. Falta decidir se o body de recurso single carrega referências hipermídia — se sim, em qual formato e com qual nível de sofisticação.

A discussão polariza-se entre adotar nada (REST nível 2 puro, URLs construídas pelo cliente), adotar formato pesado (HAL, JSON:API), ou adotar mínimo essencial (apenas `self` e navegação de relações imediatas, sem actions ou state transitions descobertos via hipermídia). A umbrella ([ADR-0022](0022-contrato-rest-canonico-umbrella.md), princípio 6) já estabeleceu conformidade com padrões abertos sem prescrever HAL ou JSON:API.

## Drivers da decisão

- **Discoverability mínima.** Cliente que recebe um `Edital` deve descobrir as inscrições daquele edital sem precisar saber construir `/editais/{id}/inscricoes` por convenção textual. Self-link e related-link cobrem isso com payload trivial.
- **Resolução de ambiguidade.** `_links.self` torna o payload autocontido — útil para logs, debug, integrações que armazenam JSON sem o contexto da request.
- **URLs evolvable pelo servidor.** Quando o servidor controla a forma do link (relativo, baseado em template interno), reorganizar a estrutura de URL não quebra clientes que respeitam `_links`. Clientes que hardcoded URLs absolutas continuam quebrando — esse é o trade-off, não a meta.
- **Custo proporcional.** HAL (`_embedded`, `_curies`, link objects estruturados) e JSON:API (`{data, links, included, relationships}`) trazem maquinaria que a `uniplus-api` não consome. Custo de onboarding, codegen e ferramentas adicionais não justificado.
- **Coerência com decisões já tomadas.** Body é representação direta ([ADR-0025](0025-wire-formato-sucesso-body-direto.md)) — `_links` entra como campo normal do recurso, não como wrapper. Coleção usa `Link` header ([ADR-0026](0026-paginacao-cursor-opaco-cifrado.md)) — não conflito.
- **Não introduzir actions/state machine.** Hipermídia avançada (descobrir `publicar`, `recusar` via links em vez de OpenAPI) acopla cliente a uma máquina de estados implícita no payload. OpenAPI ([ADR-0030](0030-openapi-3-1-contract-first.md)) é a fonte de verdade dessas operações; `_links` em V1 é navegação, não invocação.

## Opções consideradas

- **A. HATEOAS Level 1** — `_links` embutido em recurso single, contendo apenas `self` + relações de navegação canônicas, com URIs relativas e formato simples (`{ "self": "/editais/123" }`).
- **B. HAL completo** ([RFC 4287 + draft Kelly](https://datatracker.ietf.org/doc/html/draft-kelly-json-hal-11)) — `_links` com objetos estruturados (`{ "href", "templated", "type", "deprecation", "name", "profile", "title", "hreflang" }`), `_embedded` para inclusão de recursos, `_curies` para namespacing.
- **C. JSON:API** — payload re-estruturado em `{ "data": { ... }, "links": { ... }, "relationships": { ... }, "included": [ ... ] }`.
- **D. Sem hipermídia** — REST nível 2 puro; cliente constrói URIs por convenção documentada.

## Resultado da decisão

**Escolhida:** "A — HATEOAS Level 1", porque entrega os benefícios mínimos de discoverability e self-identification sem o custo de adoção de HAL ou JSON:API e sem introduzir hipermídia transacional (actions/forms) que o projeto não justifica em V1.

### Forma do `_links`

`_links` é um **campo opcional do recurso**, presente em respostas de recurso single. Sua estrutura:

- Tipo: objeto JSON.
- Cada key é o nome da relação (string ASCII, lowercase, snake_case).
- Cada value é um URI relativo (string).
- Relações canônicas reservadas:
  - **`self`** — URI canônica do próprio recurso. Presente sempre em recurso single.
  - **`collection`** — URI da coleção que contém o recurso. Opcional.
  - Relações específicas de domínio (ex.: `inscricoes` em `Edital`, `classificacao` em `Edital`, `decisao` em `Recurso`) — declaradas por slice na implementação.

Exemplo:

```json
{
  "id": "01HFK...",
  "numero": "PSE-2026-1",
  "status": "publicado",
  "_links": {
    "self": "/editais/01HFK...",
    "collection": "/editais",
    "inscricoes": "/editais/01HFK.../inscricoes",
    "classificacao": "/editais/01HFK.../classificacao"
  }
}
```

### Coleção

Coleção continua governada pela [ADR-0026](0026-paginacao-cursor-opaco-cifrado.md): body é array puro; navegação (`self`, `next`, `prev`, `first`) vai por `Link` header. **Não** há `_links` em coleção — a relação de navegação dela é um header per RFC 5988/8288. Cada item da coleção pode (e geralmente deve) carregar seu próprio `_links.self`.

### URIs relativas

Todas as URIs em `_links` são **relativas à raiz da API**. Isso permite que a `uniplus-api` rode em qualquer subpath sem ajustar payloads e elimina dependência de descoberta da base URL pelo servidor (que pode estar atrás de proxy reverso). Cliente concatena com sua base configurada.

### Esta ADR não decide

- Catálogo completo de relações por recurso — declarado por slice na implementação. Convenção: relações que retornam outro recurso ou coleção devem aparecer no `_links` do recurso pai quando úteis para navegação canônica.
- Implementação concreta do builder de links na camada `API` — extension method, helper service ou middleware. Decisão de implementação.
- Action links (`publicar`, `cancelar`, etc.) — **explicitamente fora do V1**. Operações de mutação são descobertas via OpenAPI ([ADR-0030](0030-openapi-3-1-contract-first.md)), não via `_links`. Adicioná-las exige nova ADR superseding.
- Adoção de HAL ou JSON:API no futuro — possível via ADR superseding se requisitos mudarem (ex.: cliente pesado que se beneficia de `_embedded`).

### Por que B, C e D foram rejeitadas

- **B (HAL completo).** Estruturas `{ "href", "templated", "type", ... }` por link aumentam payload sem benefício imediato; `_embedded` reintroduz tentação de denormalização que conflita com `Idempotency-Key` e cache; `_curies` resolve problema de namespace que a `uniplus-api` não tem. Custo de adoção (codegen, parsers, documentação) supera benefício marginal.
- **C (JSON:API).** Re-estrutura o body inteiro contradizendo [ADR-0025](0025-wire-formato-sucesso-body-direto.md) (que rejeitou wrappers `data:`). Adoção exigiria reabrir aquela decisão. Sem requisito que justifique.
- **D (sem hipermídia).** Falha em discoverability e em resolução de ambiguidade. Cliente perde a chance de adaptar a reorganização de URL no servidor; payloads em logs/dumps ficam ambíguos.

## Consequências

### Positivas

- **Discoverability barata.** Adicionar `_links` em uma resposta é um campo extra — sem reformatação, sem novo content-type.
- **Resilência a reorganização de URL.** Cliente que respeita `_links` continua funcionando se o servidor mover `/editais/{id}/inscricoes` para `/inscricoes?edital={id}` (futuro); cliente que hardcoded URLs sofre — comportamento esperado.
- **Self-identification.** Logs, integrações e dumps que armazenam o JSON têm a URI canônica embutida — auditoria e debug ficam mais simples.
- **Coerência total com decisões anteriores.** Body permanece representação direta ([ADR-0025](0025-wire-formato-sucesso-body-direto.md)); coleção continua array puro com `Link` header ([ADR-0026](0026-paginacao-cursor-opaco-cifrado.md)).

### Negativas

- **Payload ligeiramente maior.** `_links` em recurso pequeno pode dobrar o tamanho do JSON. Mitigação: relações são opt-in por endpoint; recursos podem omitir `_links` quando o cliente mais provável é interno e a economia de bytes importa (ex.: endpoints chamados em loop por jobs internos). Padrão é incluir.
- **Não enforça que cliente use.** Cliente pode ignorar `_links` e continuar hardcoding URLs. Esse é o limite de qualquer abordagem hipermídia parcial.
- **Tentação futura de adicionar action links.** Equipe pode propor `_links.publicar` ao longo do tempo. ADR registra explicitamente que isso requer nova decisão — não é evolução natural.

### Neutras

- A escolha de URIs relativas significa que clientes que armazenam links precisam saber a base URL contextualizada. Sem ambiguidade na prática (cliente conhece base que usou para chamar a API).
- `_links` é metadata; cliente que não quer hipermídia pode ignorar.

## Confirmação

1. **Spectral rule no CI** — schema de recurso single declarado no spec OpenAPI carrega propriedade opcional `_links` do tipo `Map<string, string>` (URIs relativas). Quando presente, deve incluir `self` como key obrigatória. Falha indica `_links` mal formado ou `self` ausente em endpoint que declara incluir hypermedia.
2. **Smoke E2E (Postman/Newman)** — para cada slice em `EditalController` (e demais conforme migração), cenário de leitura verifica que body inclui `_links.self` com URI relativa que, quando concatenada à base da API, resolve para 200.
3. **Revisão arquitetural de PR** — qualquer adição de relação nova em `_links` (ex.: nova subentidade) entra em PR review; toda tentativa de adicionar action link (mutação) é rejeitada com referência a esta ADR.

## Mais informações

- [ADR-0022](0022-contrato-rest-canonico-umbrella.md) — umbrella (princípio 6: padrões abertos com customização justificada).
- [ADR-0025](0025-wire-formato-sucesso-body-direto.md) — body como representação direta; `_links` é campo, não wrapper.
- [ADR-0026](0026-paginacao-cursor-opaco-cifrado.md) — navegação de coleção via `Link` header (não duplicada em `_links`).
- [ADR-0030](0030-openapi-3-1-contract-first.md) — OpenAPI como fonte de verdade das operações (motivo para não ter action links em V1).
- [Roy Fielding — REST APIs must be hypertext-driven](https://roy.gbiv.com/untangled/2008/rest-apis-must-be-hypertext-driven) — referência conceitual sobre HATEOAS.
- [draft-kelly-json-hal-11 — HAL](https://datatracker.ietf.org/doc/html/draft-kelly-json-hal-11) — opção B rejeitada.
- [JSON:API](https://jsonapi.org/) — opção C rejeitada.
- [RFC 5988 / 8288 — Web Linking](https://www.rfc-editor.org/rfc/rfc8288.html) — base do `Link` header usado em coleção.
