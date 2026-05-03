---
status: "accepted"
date: "2026-05-03"
decision-makers:
  - "Tech Lead (CTIC)"
---

# ADR-0022: Contrato REST canônico V1 — frame transversal e índice das ADRs filhas

## Contexto e enunciado do problema

O `uniplus-api` está no início do ciclo: existe um único controller (`EditalController`, três endpoints) no módulo Seleção, o módulo Ingresso ainda não tem controllers e o pipeline OpenAPI previsto em [ADR-0015](0015-rest-contract-first-com-openapi.md) não foi cabeado. No frontend `uniplus-web`, um `ApiErrorHandlerService` parcial já consome RFC 7807 — versão antecessora do padrão atual (RFC 9457). A janela para padronizar o contrato antes de existirem versões em produção é agora.

A direção do projeto definiu o mandato: **a `uniplus-api` é uma API pública institucional desde o dia um**, mantida pelo CTIC como único *owner* da documentação, com PROEG/PROEX/CEPS/CRCA como clientes institucionais que fornecem input normativo sem authority de aprovação. A integração `gov.br ↔ Uni+` é unilateral — o Uni+ consome gov.br via OIDC e assinatura digital, mas o gov.br nunca consome endpoints da `uniplus-api`. Isso elimina o argumento histórico de "compatibilidade com Conecta" como driver para padrões de URL ou versionamento.

A escolha do frame transversal precede e binda as decisões pontuais. Padronizar o wire format de erro sem antes fixar princípios como *no-PII em response body*, *contract-first como método* e *Clean Architecture mantendo `Domain` e `Application` HTTP-agnósticos* produziria ADRs filhas conflitantes. Esta ADR existe para registrar esse frame e indexar as dez decisões pontuais que dele decorrem.

A regra MADR "1 ADR = 1 decisão" foi preservada: este documento **não decide** wire format, mapping, paginação, idempotency, versioning, HATEOAS ou portal — cada uma dessas é decidida em sua ADR filha. A decisão registrada aqui é **estruturar o contrato canônico V1 como frame transversal + dez ADRs filhas** em vez de uma única ADR monolítica.

## Drivers da decisão

- **API pública desde o dia um.** Mandato direto da liderança CTIC. Implica rigor de contrato, política de breaking-change e portal de documentação público.
- **Janela greenfield.** Sem versões em produção, decisões podem ser tomadas pelo critério de qualidade arquitetural sem custo de migração.
- **Cross-repo scope.** Decisões cobrem `uniplus-api` (backend), `uniplus-web` (consumer adapter) e o futuro `uniplus-developers` (portal). Uma ADR única por repo concentraria contexto demais.
- **MADR "1 decisão por ADR".** Wire format, mapping, paginação e versioning são decisões independentes — devem poder ser revisitadas, depreciadas ou superseded individualmente.
- **Clean Architecture preservada.** Nenhuma decisão do contrato HTTP pode contaminar `Domain` ou `Application` ([ADR-0002](0002-clean-architecture-com-quatro-camadas.md)).
- **LGPD e [ADR-0019](0019-proibir-pii-em-path-segments-de-url.md).** Nenhum dado pessoal (CPF, nome, email, endereço) trafega em path segments, query strings, response bodies de erro ou logs sem masking — baseline cross-cutting.

## Opções consideradas

- **A. ADR única monolítica** cobrindo wire format, mapping, paginação, idempotency, versioning, HATEOAS, OpenAPI e portal.
- **B. Onze ADRs independentes sem frame transversal**, cada decisão isolada.
- **C. Umbrella (esta ADR) + dez ADRs filhas binding** (sete em `uniplus-api`, uma em `uniplus-web`, uma em `uniplus-developers`, mais a ADR-0030 que completa a [ADR-0015](0015-rest-contract-first-com-openapi.md)).

## Resultado da decisão

**Escolhida:** "C — Umbrella + dez ADRs filhas binding", porque é a única opção que respeita a regra MADR "1 ADR = 1 decisão" e simultaneamente registra os princípios cross-cutting que bindam todas as filhas. A opção A viola MADR e produz documento ingerenciável; a opção B perde a coerência transversal e abre brecha para decisões filhas conflitantes.

### Princípios transversais binding (frame)

Todos os ADRs filhos desta umbrella herdam e respeitam:

1. **Strings user-facing em pt-BR.** Mensagens de erro (`title`, `detail`), códigos legais (`legal_reference`) e textos de catálogo público são em português do Brasil. Identificadores técnicos (`code`, `traceId`, header names) permanecem em inglês.
2. **Contract-first como método.** OpenAPI 3.1 é a fonte de verdade do contrato — gerado a partir do código `Microsoft.AspNetCore.OpenApi` com transformer pipeline e linted via Spectral no CI. Drift entre código e spec falha o build. Detalhamento na ADR-0030.
3. **LGPD baseline.** Nenhum response body (sucesso ou erro) carrega PII não-mascarada. `instance` da RFC 9457 carrega correlation ID opaco, nunca CPF ou identificador externo. `errors[].field` referencia o campo, `errors[].message` é genérica em pt-BR e nunca ecoa o valor rejeitado.
4. **Clean Architecture HTTP-agnóstica.** `Domain` e `Application` retornam `Result<T>` / `DomainError(Code, Message)` apenas. Nenhuma referência a `Microsoft.AspNetCore.*` fora da camada `API` — fitness test ArchUnit enforça (detalhamento na ADR-0024).
5. **Postura de API pública.** Toda mudança no contrato segue política de breaking-change documentada (publicação prévia em changelog, deprecation window mínima, notificação a integradores via portal). Detalhamento na ADR-0028.
6. **Conformidade com padrões abertos.** RFC 9457 (problem details), RFC 5988/8288 (Link header), RFC 9110 (HTTP semantics), `draft-ietf-httpapi-idempotency-key-header` para Idempotency-Key. Customização proprietária só onde os padrões deixam vácuo (ex.: taxonomia do `code`).

### As dez ADRs filhas

| Repositório | ADR | Decisão |
|---|---|---|
| `uniplus-api` | [ADR-0023](0023-wire-formato-erro-rfc-9457.md) | Wire format de erro: RFC 9457 ProblemDetails como único formato. |
| `uniplus-api` | [ADR-0024](0024-mapeamento-domain-error-http.md) | Mapping `DomainError → HTTP` via `IDomainErrorMapper` registry na camada API. |
| `uniplus-api` | [ADR-0025](0025-wire-formato-sucesso-body-direto.md) | Wire format de sucesso: body é a representação direta do recurso, sem envelope. |
| `uniplus-api` | [ADR-0026](0026-paginacao-cursor-opaco-cifrado.md) | Paginação via cursor opaco cifrado (AES-GCM) + `Link` header RFC 5988. |
| `uniplus-api` | [ADR-0027](0027-idempotency-key-store-postgresql.md) | `Idempotency-Key` opt-in com store em PostgreSQL adjacente ao outbox. |
| `uniplus-api` | [ADR-0028](0028-versionamento-per-resource-content-negotiation.md) | Versionamento per-resource via content negotiation (`application/vnd.uniplus.<resource>.v<N>+json`). |
| `uniplus-api` | [ADR-0029](0029-hateoas-level-1-links.md) | HATEOAS Level 1 com `_links` self/next/prev relativos; HAL rejeitado. |
| `uniplus-api` | [ADR-0030](0030-openapi-3-1-contract-first.md) | OpenAPI 3.1 contract-first via `Microsoft.AspNetCore.OpenApi` (completa [ADR-0015](0015-rest-contract-first-com-openapi.md)). |
| `uniplus-web` | ADR-0011 | Consumer adapter `ApiResult<T>` em nova lib `libs/shared-http`. |
| `uniplus-developers` | ADR-0001 | Arquitetura do portal de desenvolvedores (Docusaurus 3 + Redoc + GitHub Pages). |

### Escopo V1 e bloqueio de slices novos

V1 é declarada quando todas as dez decisões filhas estão `accepted` **e** suas tasks de implementação (Build Order do TechSpec, 55 passos consolidados em 34 PRs) estão merged. Durante a construção da V1, novos slices de domínio (Inscrição, Chamada, Recurso, Matrícula) ficam bloqueados — Sprint 2-3 são dedicadas ao contrato. O pilot da migração é o `EditalController` ([ADR-0024](0024-mapeamento-domain-error-http.md)).

## Consequências

### Positivas

- **Rastreabilidade.** Cada decisão de contrato é referenciável individualmente em revisões, auditorias e onboarding.
- **Evolução granular.** Uma decisão pode ser superseded sem invalidar as demais — ex.: HATEOAS Level 1 pode evoluir para Level 2 sem tocar em paginação.
- **Frame compartilhado.** Princípios cross-cutting ficam num único documento — futuras ADRs de contrato (V2, novos recursos) herdam o mesmo frame por referência.
- **Coerência cross-repo.** O umbrella é a âncora que justifica por que `uniplus-web` e `uniplus-developers` têm ADRs vinculadas e por que essas ADRs não são "infra" isolada.

### Negativas

- **Custo de manutenção do índice.** Onze ADRs precisam ser mantidas alinhadas — cada superseded ou amendment numa filha exige verificar coerência com as demais e com o frame.
- **Discoverability.** Sem ler o umbrella, alguém pode encontrar uma ADR filha (ex.: ADR-0026 sobre paginação) e perder o contexto cross-cutting (LGPD, contract-first). Mitigado por referência cruzada explícita em cada filha.
- **Esforço de revisão concentrado.** A bateria de onze PRs de ADR sobrecarrega o revisor cross-account em uma sprint.

### Neutras

- A umbrella **não** introduz código nem altera o contrato HTTP existente. É exclusivamente registro arquitetural.

## Confirmação

Mecanismos para confirmar que o frame transversal está sendo respeitado ao longo do tempo:

1. **Cobertura do índice.** As dez ADRs filhas listadas devem existir como arquivos `accepted` nos respectivos `docs/adrs/` — verificação manual no PR de cada filha (a filha referencia esta umbrella; a umbrella referencia a filha por link relativo após o merge).
2. **Fitness tests ArchUnit** ([ADR-0012](0012-archunitnet-como-fitness-tests-arquiteturais.md)) — enforçam o princípio (4) (Clean Architecture HTTP-agnóstica). Detalhamento na ADR-0024.
3. **Spectral lint no CI** — enforça princípio (2) (contract-first) e princípio (6) (conformidade com padrões abertos). Detalhamento na ADR-0030.
4. **PII regex linter** no CI — enforça princípio (3) (LGPD baseline) escaneando response shapes do OpenAPI por padrões de CPF, email e endereço sem masking. Detalhamento na task de implementação.
5. **Revisão arquitetural de toda nova ADR de contrato** — qualquer ADR futura que toque o contrato REST deve declarar conformidade com este frame ou justificar divergência.

## Mais informações

- [ADR-0011](0011-mascaramento-de-cpf-em-logs.md) — baseline LGPD em logs (princípio 3).
- [ADR-0015](0015-rest-contract-first-com-openapi.md) — REST contract-first (origem do princípio 2; completada pela ADR-0030).
- [ADR-0019](0019-proibir-pii-em-path-segments-de-url.md) — PII em URLs (princípio 3).
- [ADR-0002](0002-clean-architecture-com-quatro-camadas.md) — fronteira de camadas (princípio 4).
- [Epic uniplus-api#259](https://github.com/unifesspa-edu-br/uniplus-api/issues/259) — Contrato REST canônico da `uniplus-api` (V1).
- [Feature uniplus-api#260](https://github.com/unifesspa-edu-br/uniplus-api/issues/260) — Fundação documental — ADRs do contrato canônico V1.
- [Story uniplus-api#261](https://github.com/unifesspa-edu-br/uniplus-api/issues/261) — Documentar 11 ADRs binding do contrato canônico V1.
- [RFC 9457 — Problem Details for HTTP APIs](https://datatracker.ietf.org/doc/html/rfc9457).
- [RFC 5988 — Web Linking](https://datatracker.ietf.org/doc/html/rfc5988).
- [RFC 9110 — HTTP Semantics](https://datatracker.ietf.org/doc/html/rfc9110).
- [draft-ietf-httpapi-idempotency-key-header-07](https://datatracker.ietf.org/doc/html/draft-ietf-httpapi-idempotency-key-header-07).
