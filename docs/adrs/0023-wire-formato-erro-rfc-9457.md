---
status: "accepted"
date: "2026-05-03"
decision-makers:
  - "Tech Lead (CTIC)"
---

# ADR-0023: Wire format de erro — RFC 9457 ProblemDetails como único formato

## Contexto e enunciado do problema

Toda resposta de erro (4xx/5xx) da `uniplus-api` precisa carregar informação útil para o consumidor: causa, contexto suficiente para acionar suporte ou autocorreção, identificadores para rastreamento. Sem padronização, o frontend lida com formatos heterogêneos (alguns endpoints retornam string, outros JSON ad-hoc, outros nada além do status code), integradores externos têm que escrever um parser por endpoint e auditores não conseguem mapear códigos retornados a normas vigentes.

O `uniplus-web` já consome **RFC 7807 (Problem Details for HTTP APIs, predecessora)** parcialmente via `ApiErrorHandlerService`. A IETF publicou RFC 9457 em 2023 substituindo a 7807 com pequenas correções (notavelmente sobre extensions JSON e content-type). O `Microsoft.AspNetCore` emite ProblemDetails nativamente desde .NET 7.

A umbrella [ADR-0022](0022-contrato-rest-canonico-umbrella.md) define como princípio cross-cutting que respostas de erro não podem carregar PII e que o contrato segue padrões abertos. Esta ADR materializa esses princípios na escolha do wire format e na lista de extensions adotadas.

## Drivers da decisão

- **Padrão aberto e ubíquo.** RFC 9457 tem parsers prontos em praticamente toda stack mainstream. Frontend, integradores externos e ferramentas de monitoração (Sentry, OpenTelemetry collectors) já reconhecem `application/problem+json`.
- **Substituição da RFC 7807 já parcialmente implementada.** O frontend consome a versão antecessora — migração para 9457 é incremental, não quebra-tudo.
- **LGPD baseline (umbrella, princípio 3).** O formato escolhido precisa permitir que `detail`, `instance` e mensagens de validação sejam emitidos sem PII e sem ecoar o valor rejeitado.
- **Rastreabilidade legal.** Erros que decorrem de regra normativa (Lei 12.711/2012, Lei 14.723/2023, resoluções Unifesspa, cláusula de edital) precisam de campo opcional para vincular o erro à norma — auditores externos (CGU, TCU) e integradores institucionais (PROEG, jurídico) consomem essa informação.
- **Trace context distribuído.** Toda resposta de erro precisa carregar `traceId` no formato W3C trace context para correlação entre `uniplus-api`, `uniplus-web`, OpenTelemetry collector e dashboards Grafana ([ADR-0018](0018-opentelemetry-para-instrumentacao-do-backend.md)).
- **Conformidade com `Microsoft.AspNetCore.OpenApi`.** O formato escolhido precisa ser emitido nativamente pelo .NET 10 sem adapter custom, ou o pipeline contract-first ([ADR-0030](0030-openapi-3-1-contract-first-microsoft-aspnetcore-openapi.md)) fica mais frágil.

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
- **`instance`** — identificador opaco gerado por request, nunca CPF, número de inscrição ou identificador externo (princípio LGPD da umbrella + [ADR-0019](0019-proibir-pii-em-path-segments-de-url.md)). A escolha do esquema de geração (ULID, GUID v7, snowflake) cabe à camada de implementação.

### Extensions adotadas

Extensions obrigatórias em toda resposta de erro:

- **`code`** (string) — identificador estável da causa, regex `^[a-z]+(\.[a-z_]+)+$`, namespace `uniplus.<modulo>.<razao>` (ex.: `uniplus.edital.nao_encontrado`, `uniplus.cpf.invalido`). É a chave primária de troubleshooting, observabilidade e do catálogo público.
- **`traceId`** (string) — `trace-id` do W3C trace context (32 caracteres hex). É o `trace-id` do contexto W3C da requisição (o mesmo do header `traceparent` que a API recebeu ou iniciou); a API não escreve `traceparent` na resposta, porque o corpo já entrega o valor. Sem atividade de tracing corrente, o valor é gerado só para correlação com o log e não corresponde a span. Permite cruzar erro com span no Tempo/Grafana.

Extensions condicionais:

- **`errors`** (array) — presente, em qualquer status, quando há mais de uma causa ou quando alguma causa aponta um campo; uma única causa sem campo não leva `errors[]` (emenda de 2026-11-17, abaixo). Cada elemento contém `field` (o campo que a causa aponta: caminho em notação de pontos, ex.: `inscricao.candidato.cpf`, ou, nos endpoints do documento da configuração, o ponteiro JSON descrito na emenda abaixo; sai sem a chave quando a causa não aponta campo), `code` (próprio do nível de campo, mesma taxonomia do `code` raiz) e `message` (pt-BR genérica). **Nunca** carrega `value` ou qualquer reflexo do dado rejeitado.
- **`legal_reference`** (objeto) — presente quando o erro decorre de regra normativa rastreável. Campos: `norma_id` (ex.: `lei-14.723-2023`), `artigo` (ex.: `art.5,§2`), `clausula_edital` (ex.: `edital-2026-1.cap-3.item-7`), `edital_snapshot_id` (referência ao snapshot congelado dos parâmetros do edital). Todos os subcampos são opcionais entre si — basta um para a extension fazer sentido.

> **Emenda (2026-11-17) — a presença do `errors[]`, o item sem `field` e o `field` como ponteiro JSON nos endpoints do documento da configuração.**
>
> **Presença do `errors[]`.** No lugar de "presente apenas em erros de validação (status 422)", o `errors[]` **vai na resposta quando há mais de uma causa ou quando alguma causa aponta um campo, em qualquer status**. Uma única causa sem campo não leva `errors[]`: o `code` e o `detail` da raiz já são a causa. Uma única causa com campo leva `errors[]` com um item. Com mais de uma causa, o `code` e o `detail` da raiz são os da primeira, que é também o primeiro item do array.
>
> **Item sem `field`.** A causa que não aponta campo **sai sem a chave `field`**, e não com `field: null`, em todo endpoint, inclusive na gravação por dimensão. A chave ausente é a única forma de dizer "sem campo".
>
> **O `field` do documento da configuração.** A conferência única da configuração ([ADR-0174](0174-conferencia-unica-da-configuracao-do-processo-com-todas-as-causas.md)) devolve, de uma vez, todas as causas que impedem congelar o documento ([ADR-0172](0172-esquema-json-versionado-do-documento-de-configuracao.md)). Nos endpoints do documento, `errors[].field` é o **ponteiro JSON (RFC 6901)** do trecho que a causa aponta, com os nomes que o esquema publicado dá aos campos e a **posição numérica no documento enviado** em cada lista: `/cronograma/fases/2/unidadeResponsavelCodigo`, `/formularios/0/itens/3/fatoCodigo`. O ponteiro nunca usa chave de negócio. A posição é a do documento enviado, que é o guardado e o exportado: nesses endpoints o pedido é o próprio documento, e a conversão traduz a posição canônica, em que a conferência trabalha, para a posição de origem. O texto vazio (`""`) é o ponteiro da **raiz do documento**, nunca "sem campo". O `field` em notação de pontos, descrito acima, continua valendo para a gravação por dimensão ([ADR-0125](0125-dominio-fonte-unica-validacao-sem-fluentvalidation-duplicado.md)), em que o campo é o do corpo do pedido.
>
> A causa que não corresponde a trecho do documento (o catálogo, a aprovação de proteção, o pedido da retificação) segue a regra do item sem `field`. A causa que aponta uma coleção da qual o item foi retirado aponta a coleção.
>
> **Decisão registrada, a implementar depois:** cada item de `errors[]` **pode** carregar `type`, a URI do catálogo montada por `problemTypeUriFactory.Build(code)` a partir do `code` do próprio item, para que o cliente chegue à página do erro sem montar a URI. Esta emenda só registra a decisão; a implementação e o contrato ficam para a entrega própria (uniplus-api#2523).
>
> **Emenda (2026-11-17) — o código de catálogo e a chave que o documento da configuração cita sem existir.**
>
> A emenda anterior trata a causa de catálogo como causa sem `field`. Isso vale para a causa que não nasce de um campo do documento. A [ADR-0172](0172-esquema-json-versionado-do-documento-de-configuracao.md) manda o documento citar o catálogo por código e as partes por chave local, e por isso o código de catálogo ou a chave citados que não existem **são causas da configuração e apontam o campo que os cita**.
>
> - Cada causa traz, em `errors[].field`, o ponteiro JSON (RFC 6901) do campo que cita o código ou a chave, na **posição em que o item veio no documento enviado**.
> - A `message` descreve a regra violada e não repete o valor digitado.
> - A chave inexistente tem um código geral, que serve a todas as partes do documento; o que distingue a parte é o campo apontado.
> - Não bloqueiam o salvamento do rascunho: aparecem no diagnóstico e recusam a publicação.
> - A falta de `field` continua só para a causa que não nasce de um campo do documento.
>
> **Emenda (2026-11-20) — a identificação do item nas recusas da configuração.**
>
> **Contexto.** Nas recusas da publicação e da retificação, o `field` aponta o item da lista pela posição (emenda de 2026-11-17, acima). Entre a recusa e a nova leitura, outra pessoa pode gravar o processo e mudar as posições, e o cliente passa a apontar outro item.
>
> **Decisão.** Nas recusas da publicação e da retificação, que saem da conferência da configuração ([ADR-0174](0174-conferencia-unica-da-configuracao-do-processo-com-todas-as-causas.md)), cada item de `errors[]` cujo `field` passa por uma fase do cronograma, uma etapa ou um recurso da etapa traz o membro `identificadores`: uma lista de objetos `{ "lista": ..., "identificador": ... }`, um para cada um desses itens, na ordem em que aparecem no `field`. `lista` é o nome da lista como aparece no `field` (`fases`, `etapas`, `recursos`). `identificador` é o código do tipo de fase, para a fase, e o identificador opaco do item, para a etapa e o recurso; nenhum identificador é reaproveitado para outro item. Exemplos: `[{"lista":"fases","identificador":"INSCRICAO"}]` e `[{"lista":"etapas","identificador":"<id da etapa>"},{"lista":"recursos","identificador":"<id do recurso>"}]`. O identificador nunca é dado pessoal. O `field` continua sendo o ponteiro com a posição e sem chave de negócio, e o item de outra lista no caminho continua identificado só pela posição. O item cujo `field` não passa por nenhum desses três, a raiz da resposta e os demais endpoints não trazem o membro, que sai omitido, nunca `null` nem lista vazia. Esta emenda acrescenta o membro ao item de `errors[]` descrito acima (`field`, `code` e `message`) e é a decisão que a regra das extensões adicionais, abaixo, exige; vale só para este membro.
>
> **Consequências.** O cliente localiza a fase, a etapa ou o recurso mesmo depois de outra gravação. O identificador que não existe mais não aponta outro item, porque nenhum é reaproveitado: a recusa vai para o item anterior do caminho que ainda existe (a etapa, para o recurso retirado) ou, se nenhum existir, para a lista. O componente `ProblemDetails` do OpenAPI passa a descrever o item de `errors[]`, com `field`, `code`, `message` e `identificadores`, como a [ADR-0030](0030-openapi-3-1-contract-first-microsoft-aspnetcore-openapi.md) exige para as extensões desta ADR.

Extensions adicionais não definidas aqui são proibidas em V1 sem ADR de extensão. Extensions que carregam dado pessoal são proibidas por princípio (umbrella, princípio 3).

### Mensagens em pt-BR e mascaramento

`title`, `detail` e `errors[].message` são em português do Brasil. `code` e `traceId` são identificadores técnicos; `errors[].field` usa o nome que o corpo do pedido ou o esquema publicado do documento dá ao campo. Quando o erro ocorre em campo que naturalmente conteria PII (CPF, email, nome), a mensagem nunca ecoa o valor — só descreve a regra violada.

### Esta ADR não decide

- Como `DomainError` (Application/Domain) é convertido em ProblemDetails — decisão na [ADR-0024](0024-mapeamento-domain-error-http.md).
- Como a versão do recurso entra no `Content-Type` em respostas de sucesso — decisão na [ADR-0028](0028-versionamento-per-resource-content-negotiation.md). Respostas de erro permanecem em `application/problem+json` independente da versão do recurso.
- Como a estrutura de catálogo público em `/erros/{code}` é construída — decisão na ADR-0001 do `uniplus-developers`.

> **Emenda (2026-11-17) — a base do `type` é a do portal publicado.** O `type` aponta para a página do catálogo em `https://unifesspa-edu-br.github.io/uniplus-developers/erros/{code}`, o endereço em que o portal está no ar, configurado em `ProblemType:BaseUri` e conferido pelos testes de integração contra a constante do contrato. A URN provisória `urn:uniplus:problem:{code}` acabou: o portal está implantado e a `uniplus-api` não a emite. Se a instituição passar a publicar o catálogo sob o domínio institucional, muda a configuração e o portal juntos, e a constante do contrato nos testes acompanha.

## Consequências

### Positivas

- **Parser único no consumidor.** `uniplus-web` consolida o tratamento via `ApiErrorHandlerService` evoluído para 9457 e os integradores externos não precisam de parser custom.
- **Rastreabilidade fim-a-fim.** `code` + `traceId` + `instance` cobrem três níveis distintos: causa (taxonomia), span de tracing distribuído e instância única para suporte.
- **Auditabilidade legal.** `legal_reference` é o gancho que permite ao auditor ou à PROEG/jurídico cruzar resposta de erro com norma sem ler código-fonte.
- **Suporte nativo do .NET 10.** `Microsoft.AspNetCore` emite ProblemDetails sem adapter custom; transformer pipeline da OpenAPI propaga as extensions adotadas para o spec ([ADR-0030](0030-openapi-3-1-contract-first-microsoft-aspnetcore-openapi.md)).

### Negativas

- **Custo de governança da taxonomia `code`.** Cada novo erro de domínio entra no catálogo `/erros/{code}` do portal e é registrado no mapper ([ADR-0024](0024-mapeamento-domain-error-http.md)). Adições viram revisão de PR no `uniplus-developers`.
- **`legal_reference` depende de input externo.** Conteúdo normativo das entradas iniciais do catálogo aguarda input de PROEG/PROEX/CEPS/jurídico. CTIC publica drafts e refina conforme o input chega — não bloqueia a ADR.
- **Migração do consumidor.** O `ApiErrorHandlerService` do `uniplus-web` consome 7807 hoje; precisa ser ajustado para 9457 (ADR-0011 do `uniplus-web` cobre o adapter).

### Neutras

- O wire format de sucesso é decidido em ADR separada ([ADR-0025](0025-wire-formato-sucesso-body-direto.md)) e independe desta — adoptar 9457 para erro não obriga adotar envelope para sucesso.
- A escolha aqui não restringe extensions futuras na V2 — uma nova extension entra via ADR de extensão sem invalidar esta.

## Confirmação

1. **Spectral rule no CI.** Toda response com status `4xx` ou `5xx` no spec OpenAPI deve referenciar o schema `ProblemDetails` definido com as extensions desta ADR. Detalhamento da pipeline na [ADR-0030](0030-openapi-3-1-contract-first-microsoft-aspnetcore-openapi.md).
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
