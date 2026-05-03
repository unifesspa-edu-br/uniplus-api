---
status: "accepted"
date: "2026-05-03"
decision-makers:
  - "Tech Lead (CTIC)"
---

# ADR-0023: Wire format de erro — RFC 9457 ProblemDetails como único formato

## Contexto e enunciado do problema

Toda resposta de erro (4xx/5xx) da `uniplus-api` precisa carregar informação útil para o consumidor: causa, contexto suficiente para acionar suporte ou autocorreção, identificadores para rastreamento. Sem padronização, o frontend lida com formatos heterogêneos (alguns endpoints retornam string, outros JSON ad-hoc, outros nada além do status code), integradores externos têm que escrever um parser por endpoint e auditores não conseguem mapear códigos retornados a normas vigentes.

O `uniplus-web` já consome `RFC 7807 Problem Details` parcialmente via `ApiErrorHandlerService` — formato antecessor. A IETF publicou RFC 9457 em 2023 substituindo a 7807 com pequenas correções (notavelmente sobre extensions JSON e content-type). O `Microsoft.AspNetCore` emite ProblemDetails nativamente desde .NET 7.

A umbrella [ADR-0022](0022-contrato-rest-canonico-umbrella.md) define como princípio cross-cutting que respostas de erro não podem carregar PII e que o contrato segue padrões abertos. Esta ADR materializa esses princípios na escolha do wire format e na lista de extensions adotadas.

## Drivers da decisão

- **Padrão aberto e ubíquo.** RFC 9457 tem parsers prontos em praticamente toda stack mainstream. Frontend, integradores externos e ferramentas de monitoração (Sentry, OpenTelemetry collectors) já reconhecem `application/problem+json`.
- **Substituição da RFC 7807 já parcialmente implementada.** O frontend consome a versão antecessora — migração para 9457 é incremental, não quebra-tudo.
- **LGPD baseline (umbrella, princípio 3).** O formato escolhido precisa permitir que `detail`, `instance` e mensagens de validação sejam emitidos sem PII e sem ecoar o valor rejeitado.
- **Rastreabilidade legal.** Erros que decorrem de regra normativa (Lei 12.711/2012, Lei 14.723/2023, resoluções Unifesspa, cláusula de edital) precisam de campo opcional para vincular o erro à norma — auditores externos (CGU, TCU) e integradores institucionais (PROEG, jurídico) consomem essa informação.
- **Trace context distribuído.** Toda resposta de erro precisa carregar `traceId` no formato W3C trace context para correlação entre `uniplus-api`, `uniplus-web`, OpenTelemetry collector e dashboards Grafana ([ADR-0018](0018-opentelemetry-para-instrumentacao-do-backend.md)).
- **Conformidade com `Microsoft.AspNetCore.OpenApi`.** O formato escolhido precisa ser emitido nativamente pelo .NET 10 sem adapter custom, ou o pipeline contract-first ([ADR-0030](0030-openapi-3-1-contract-first.md)) fica mais frágil.

## Opções consideradas

- **A. RFC 9457 ProblemDetails** com `application/problem+json` e conjunto fixo de extensions (`code`, `traceId`, `errors[]`, `legal_reference`).
- **B. Envelope custom** `{ success: false, error: { ... } }` aplicado uniformemente em 4xx (e simétrico ao envelope de sucesso).
- **C. JSON:API `errors[]`** — formato com cardinalidade explícita e atributos `id`, `links`, `source`, `meta`.
- **D. Status code + body livre por endpoint** (sem padronização adicional).

## Resultado da decisão

**Escolhida:** "A — RFC 9457 ProblemDetails", porque é o único candidato que combina padrão aberto reconhecido pela IETF, suporte nativo no `Microsoft.AspNetCore`, parser pré-existente no `uniplus-web` e capacidade de extensão tipada para os campos institucionais (`legal_reference`, `traceId`, `code`).

### Forma da resposta

Toda resposta 4xx ou 5xx é emitida com `Content-Type: application/problem+json` e corpo conforme RFC 9457. Os campos obrigatórios da RFC permanecem com a semântica da norma:

- **`type`** (URI) — referência à página do catálogo público em `developers.uniplus.unifesspa.edu.br/erros/{code}`. Enquanto o portal não está deployado, a `uniplus-api` emite uma URN provisória `urn:uniplus:problem:{code}` (RFC 8141), que migra para URI HTTPS sem breaking change na ativação do portal.
- **`title`** — frase curta em pt-BR, estável por `code` (não muda entre instâncias do mesmo erro).
- **`status`** — código HTTP, redundante com a linha de status mas exigido pela RFC para clientes que só leem o body.
- **`detail`** — frase em pt-BR explicando a instância específica do erro. **Não pode** ecoar valores rejeitados, refletir nomes de classes/exceptions internas nem expor caminhos de arquivo. Quando não há informação adicional além do `title`, omitir o campo.
- **`instance`** — correlation ID opaco da request (ULID ou similar), nunca CPF, número de inscrição ou identificador externo (princípio LGPD da umbrella + [ADR-0019](0019-proibir-pii-em-path-segments-de-url.md)).

### Extensions adotadas

Extensions obrigatórias em toda resposta de erro:

- **`code`** (string) — identificador estável da causa, regex `^[a-z]+(\.[a-z_]+)+$`, namespace `uniplus.<modulo>.<razao>` (ex.: `uniplus.edital.nao_encontrado`, `uniplus.cpf.invalido`). É a chave primária de troubleshooting, observabilidade e do catálogo público.
- **`traceId`** (string) — `trace-id` do W3C trace context (32 caracteres hex). Mesmo valor presente no header `traceparent` da response. Permite cruzar erro com span no Tempo/Grafana.

Extensions condicionais:

- **`errors`** (array) — presente apenas em erros de validação (status 422). Cada elemento contém `field` (caminho dot-notation, ex.: `inscricao.candidato.cpf`), `code` (próprio do nível de campo, mesma taxonomia do `code` raiz) e `message` (pt-BR genérica). **Nunca** carrega `value` ou qualquer reflexo do dado rejeitado.
- **`legal_reference`** (objeto) — presente quando o erro decorre de regra normativa rastreável. Campos: `norma_id` (ex.: `lei-14.723-2023`), `artigo` (ex.: `art.5,§2`), `clausula_edital` (ex.: `edital-2026-1.cap-3.item-7`), `edital_snapshot_id` (referência ao snapshot congelado dos parâmetros do edital). Todos os subcampos são opcionais entre si — basta um para a extension fazer sentido.

Extensions adicionais não definidas aqui são proibidas em V1 sem ADR de extensão. Extensions que carregam dado pessoal são proibidas por princípio (umbrella, princípio 3).

### Mensagens em pt-BR e mascaramento

`title`, `detail` e `errors[].message` são em português do Brasil. Identificadores técnicos (`code`, `traceId`, nomes de campo em `errors[].field`) permanecem em inglês/identificadores. Quando o erro ocorre em campo que naturalmente conteria PII (CPF, email, nome), a mensagem nunca ecoa o valor — só descreve a regra violada.

### Esta ADR não decide

- Como `DomainError` (Application/Domain) é convertido em ProblemDetails — decisão na [ADR-0024](0024-mapeamento-domain-error-http.md).
- Como a versão do recurso entra no `Content-Type` em respostas de sucesso — decisão na [ADR-0028](0028-versionamento-per-resource-content-negotiation.md). Respostas de erro permanecem em `application/problem+json` independente da versão do recurso.
- Como a estrutura de catálogo público em `/erros/{code}` é construída — decisão na ADR-0001 do `uniplus-developers`.

## Consequências

### Positivas

- **Parser único no consumidor.** `uniplus-web` consolida o tratamento via `ApiErrorHandlerService` evoluído para 9457 e os integradores externos não precisam de parser custom.
- **Rastreabilidade fim-a-fim.** `code` + `traceId` + `instance` cobrem três níveis distintos: causa (taxonomia), span de tracing distribuído e instância única para suporte.
- **Auditabilidade legal.** `legal_reference` é o gancho que permite ao auditor ou à PROEG/jurídico cruzar resposta de erro com norma sem ler código-fonte.
- **Suporte nativo do .NET 10.** `Microsoft.AspNetCore` emite ProblemDetails sem adapter custom; transformer pipeline da OpenAPI propaga as extensions adotadas para o spec ([ADR-0030](0030-openapi-3-1-contract-first.md)).

### Negativas

- **Custo de governança da taxonomia `code`.** Cada novo erro de domínio entra no catálogo `/erros/{code}` do portal e é registrado no mapper ([ADR-0024](0024-mapeamento-domain-error-http.md)). Adições viram revisão de PR no `uniplus-developers`.
- **`legal_reference` depende de input externo.** Conteúdo normativo das entradas iniciais do catálogo aguarda input de PROEG/PROEX/CEPS/jurídico. CTIC publica drafts e refina conforme o input chega — não bloqueia a ADR.
- **Migração do consumidor.** O `ApiErrorHandlerService` do `uniplus-web` consome 7807 hoje; precisa ser ajustado para 9457 (ADR-0011 do `uniplus-web` cobre o adapter).

### Neutras

- O wire format de sucesso é decidido em ADR separada ([ADR-0025](0025-wire-formato-sucesso-body-direto.md)) e independe desta — adoptar 9457 para erro não obriga adotar envelope para sucesso.
- A escolha aqui não restringe extensions futuras na V2 — uma nova extension entra via ADR de extensão sem invalidar esta.

## Confirmação

1. **Spectral rule no CI.** Toda response com status `4xx` ou `5xx` no spec OpenAPI deve referenciar o schema `ProblemDetails` definido com as extensions desta ADR. Detalhamento da pipeline na [ADR-0030](0030-openapi-3-1-contract-first.md).
2. **PII regex linter no CI.** Escaneia o spec OpenAPI por padrões `value`, `cpf`, `email` em response shapes de erro — falha o build se aparecer ([umbrella ADR-0022](0022-contrato-rest-canonico-umbrella.md), princípio 3).
3. **Smoke test Postman/Newman.** Suite no CI dispara cenários de erro conhecidos e verifica `Content-Type: application/problem+json`, presença de `code` válido (regex), `traceId` (32 hex chars) e ausência de PII no `detail`.
4. **Fitness test ArchUnit.** Verificação de que controllers retornam apenas `ProblemDetails` (via `IDomainErrorMapper`) ou o tipo do recurso — detalhamento na [ADR-0024](0024-mapeamento-domain-error-http.md).

## Mais informações

- [ADR-0022](0022-contrato-rest-canonico-umbrella.md) — umbrella do contrato canônico V1.
- [ADR-0024](0024-mapeamento-domain-error-http.md) — mapeamento `DomainError → HTTP` (consumidor desta decisão).
- [ADR-0011](0011-mascaramento-de-cpf-em-logs.md) — baseline LGPD em logs (mesmo princípio aplicado a wire format).
- [ADR-0019](0019-proibir-pii-em-path-segments-de-url.md) — PII em URLs (referência cruzada para `instance`).
- [ADR-0018](0018-opentelemetry-para-instrumentacao-do-backend.md) — origem do `traceId` em W3C trace context.
- [RFC 9457 — Problem Details for HTTP APIs](https://www.rfc-editor.org/rfc/rfc9457.html).
- [RFC 7807 — Problem Details for HTTP APIs (predecessora)](https://www.rfc-editor.org/rfc/rfc7807.html).
- [RFC 8141 — Uniform Resource Names (URNs)](https://www.rfc-editor.org/rfc/rfc8141.html) — base do `urn:uniplus:problem:{code}` provisório.
- [W3C Trace Context](https://www.w3.org/TR/trace-context/) — formato do `traceId`.
