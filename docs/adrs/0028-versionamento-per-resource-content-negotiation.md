---
status: "accepted"
date: "2026-05-03"
decision-makers:
  - "Tech Lead (CTIC)"
---

# ADR-0028: Versionamento per-resource via content negotiation

## Contexto e enunciado do problema

Toda API pública evolui — campos são adicionados, semânticas se alteram, regras de validação mudam. Sem estratégia de versionamento explícita, qualquer alteração breaking quebra clientes existentes; com estratégia inadequada, mudanças pequenas exigem orquestração desproporcional entre servidor e consumidores.

A [ADR-0015](0015-rest-contract-first-com-openapi.md) decidiu que a `uniplus-api` é contract-first com OpenAPI mas deixou a granularidade do versionamento em aberto. O `EditalController` atual está exposto em `/api/v1/editais` — versão acoplada ao path. Esse esquema funciona enquanto há um único recurso em produção; rapidamente vira problema quando recursos heterogêneos (Edital, Inscrição, Recurso, Classificação) precisam evoluir em cadências independentes, porque o path embute uma versão monolítica que força decisões em bloco: ou todos os recursos sobem para `/v2/` (custo alto, breaking change cascateado) ou ficam stuck em `/v1/` (débito técnico permanente).

Uma decisão precisa ser tomada antes de a `uniplus-api` ganhar mais recursos públicos: como expressar a versão de um recurso individual sem amarrá-la à versão dos demais. A umbrella ([ADR-0022](0022-contrato-rest-canonico-umbrella.md), princípio 5) já estabeleceu que a postura é a de uma API pública desde o dia 1 — versionamento é contrato com terceiros, não detalhe de implementação.

## Drivers da decisão

- **Cadência independente por recurso.** Edital pode evoluir 3 vezes em um ano enquanto Recurso permanece estável; precisa de mecanismo onde `Edital v2` coexiste com `Recurso v1` sem coordenação artificial.
- **Coerência com [ADR-0019](0019-proibir-pii-em-path-segments-de-url.md).** Path deve carregar identidade de recurso, não metadado de protocolo. Versão é metadado.
- **Coerência com [ADR-0025](0025-wire-formato-sucesso-body-direto.md).** Body é representação direta do recurso; o `Content-Type` deve descrever essa representação, incluindo a versão dela.
- **Padrão aberto.** Vendor media types (`application/vnd.<vendor>.<resource>.v<N>+json`) é convenção registrada na IANA, reconhecida por content-negotiation libraries de toda stack mainstream.
- **Sem registro centralizado de cliente.** Versionamento Stripe-style (`Api-Version: 2026-05-04` em header) requer dashboard onde cada cliente registra sua data-versão; institucionalmente fora do escopo no V1 (não temos onde clientes se registrarem nem auditoria de quem está em qual versão).
- **Breaking change rastreável.** Cada nova versão precisa entrar no changelog público antes da deploy; sem isso, integradores são surpreendidos.
- **Janela de coexistência mínima previsível.** Integradores institucionais (PROEG, jurídico, futuro SIGAA) precisam de garantia escrita de quanto tempo a versão antiga continua aceita após a nova ser lançada.

## Opções consideradas

- **A. Versionamento per-resource via content negotiation** com media types `application/vnd.uniplus.<resource>.v<N>+json`, URL clean (sem prefixo de versão), 406 com `available_versions` quando a versão pedida não existe, ciclo de deprecation via `Sunset` (RFC 8594) + `Deprecation` (RFC 9745), coexistência mínima 12 meses.
- **B. Versionamento por URL path** (`/v1/editais`, `/v2/editais`).
- **C. Versionamento por header de data Stripe-style** (`Api-Version: 2026-05-04`).
- **D. Sem versionamento explícito** — quebra-and-fix; integradores absorvem mudanças.

## Resultado da decisão

**Escolhida:** "A — Versionamento per-resource via content negotiation", porque é a única opção que combina cadência independente por recurso, padrão aberto reconhecido, coerência com decisões já tomadas (path sem metadado, body como recurso direto) e ausência de infraestrutura de registro de cliente.

### Forma do media type

Cada recurso versionado é representado por media type vendor:

```text
application/vnd.uniplus.<resource>.v<N>+json
```

- **`<resource>`** — slug ASCII lowercase do recurso, alinhado ao path (ex.: `edital`, `inscricao`, `recurso`, `classificacao`). Recurso composto separa por hífen (`edital-anexo`).
- **`<N>`** — inteiro positivo monotônico. Sem decimais, sem semver — versão é apenas marcador de breaking change; mudanças aditivas (campo novo opcional) permanecem na mesma versão.
- **`+json`** — sintaxe estruturada por RFC 6839; declara que o suffix é JSON e habilita parsers genéricos.

Exemplo:

```http
GET /editais/01HFK... HTTP/1.1
Accept: application/vnd.uniplus.edital.v1+json

HTTP/1.1 200 OK
Content-Type: application/vnd.uniplus.edital.v1+json

{ "id": "01HFK...", "numero": "PSE-2026-1", ... }
```

### URL clean (sem prefixo de versão)

Path expressa apenas identidade do recurso (`/editais`, `/editais/{id}`, `/editais/{id}/inscricoes`). Versão não entra no path. O `EditalController` atual em `/api/v1/editais` é migrado para `/api/editais` como parte do PR pilot — breaking change aceitável em estágio greenfield, com janela mínima zero porque ainda não há consumidor externo em produção.

### Negociação e fallback

- **`Accept: application/vnd.uniplus.edital.v1+json`** → 200 OK com `Content-Type` correspondente.
- **`Accept: application/json`** ou ausente → 200 OK com a **versão mais recente estável** do recurso. Suportado para conveniência durante experimentação e ferramentas genéricas; **não** é caminho recomendado para integradores institucionais, que devem fixar a versão.
- **`Accept` declara versão inexistente** (ex.: `v99`) → 406 Not Acceptable com `Content-Type: application/problem+json`, `code: uniplus.contract.versao_nao_suportada` e extension custom **`available_versions`** (array de inteiros) listando as versões aceitas para o recurso requisitado.
- **`Accept` declara recurso inexistente** → 404 Not Found pelo path; conteúdo do `Accept` é irrelevante.

Exemplo de 406:

```json
{
  "type": "urn:uniplus:problem:uniplus.contract.versao_nao_suportada",
  "title": "Versão de recurso não suportada",
  "status": 406,
  "detail": "A versão solicitada para o recurso 'edital' não existe.",
  "code": "uniplus.contract.versao_nao_suportada",
  "traceId": "0af7651916cd43dd8448eb211c80319c",
  "available_versions": [1, 2]
}
```

### Ciclo de vida de uma versão

Cada versão de recurso tem três fases observáveis:

1. **`current`** — versão recomendada para integradores; entrada vigente no portal `developers.uniplus.unifesspa.edu.br`. Nenhum header adicional; resposta padrão.
2. **`deprecated`** — versão funcional mas marcada para remoção. Toda response carrega:
   - **`Deprecation`** (RFC 9745) — timestamp HTTP-date indicando quando a deprecation foi anunciada.
   - **`Sunset`** (RFC 8594) — timestamp HTTP-date indicando quando a versão deixará de ser aceita.
   - **`Link`** (rel `successor-version`) — apontando para o media type da próxima versão.
3. **`sunset`** — versão removida. Requests com `Accept` declarando essa versão recebem 406 com `available_versions` listando apenas as ativas.

### Janela mínima de coexistência

Quando uma versão `vN+1` entra como `current`, a versão `vN` permanece aceita por **no mínimo 12 meses** corridos antes do `sunset`. O prazo é piso, não teto — versões com integrador institucional ativo (PROEG, SIGAA, parceiros) podem ter prazo maior por decisão registrada. Decisão de `sunset` antecipada (menos de 12 meses) exige nova ADR superseding parte desta.

### Changelog público

Toda nova versão de recurso e toda transição de fase (deprecated, sunset) entra como entrada datada no changelog público em `developers.uniplus.unifesspa.edu.br/changelog`. Critérios mínimos da entrada:

- Recurso afetado e versão (ex.: `edital v2`).
- Tipo da mudança (versão nova, deprecation, sunset).
- Resumo em pt-BR (1-3 frases) do que mudou e por quê.
- Para versão nova: link para o diff de schema entre `vN-1` e `vN`.
- Para deprecation: timestamp do `Sunset` planejado.

A entrada é parte do PR de implementação da versão; PR sem entrada falha em revisão arquitetural.

### Esta ADR não decide

- Implementação concreta do `MediaTypeVersionFormatter` (input/output formatter custom no MVC, middleware, ou content-type policy) — decisão de implementação na slice pilot.
- Esquema interno de versionamento de DTOs no `Application` (separar tipos `EditalV1Dto` / `EditalV2Dto` vs versionar via mapper) — escolha de organização da implementação.
- Retorno automático de qual versão foi servida em response header (`Content-Version`, `Api-Resource-Version` ou similar) — proposta razoável, mas decisão fica para PR pilot. Versão sempre está presente no `Content-Type` mesmo sem header adicional.
- Versionamento dos endpoints administrativos internos (não públicos) — fora do escopo da V1; podem permanecer sem versão até serem expostos.

### Por que B, C e D foram rejeitadas

- **B (URL path).** Acopla todos os recursos a uma cadência única; quebra a coerência com [ADR-0019](0019-proibir-pii-em-path-segments-de-url.md) (path carrega metadado, não identidade). Causa cadeia de breaking changes desproporcional quando um único recurso evolui.
- **C (Stripe-style date header).** Requer dashboard de registro de cliente, mecanismo de pinning per-account, política de "rolling release" e infraestrutura de auditoria de qual cliente está em qual versão. Custo institucional alto; sem requisito que justifique no V1. Pode ser adicionado no V2 como pinning per-account opcional sem invalidar esta ADR.
- **D (sem versionamento).** Inviável para API pública; contraria o princípio 5 da umbrella. Quebra-and-fix transfere custo para integradores institucionais e remove qualquer garantia escrita de estabilidade.

## Consequências

### Positivas

- **Cadência independente.** Edital pode subir para `v2` sem afetar Recurso ou Inscrição. Cada recurso evolui no seu próprio ritmo.
- **URL estável.** `/editais/{id}` continua funcionando ao longo de todas as evoluções; clientes que mudam de versão alteram apenas `Accept`.
- **Padrão aberto.** Vendor media types funcionam com clientes HTTP genéricos (curl, Postman, axios, httpx) sem adapter custom; OpenAPI 3.1 ([ADR-0030](0030-openapi-3-1-contract-first.md)) descreve cada versão como response separada.
- **Deprecation explícita.** `Sunset` + `Deprecation` headers (RFCs 8594/9745) permitem que clientes detectem deprecation programaticamente, sem dependência de canal humano.
- **Janela 12 meses dá previsibilidade.** Integradores institucionais conseguem planejar upgrades dentro do orçamento anual; CTIC consegue planejar lançamentos sem pressão para suporte indefinido.

### Negativas

- **Tooling client menos maduro para content negotiation versionada.** Algumas bibliotecas HTTP geram `Accept: application/json` por padrão; integradores precisam configurar explicitamente o vendor media type. Mitigação: portal documenta exemplos por linguagem; biblioteca client codegen (futuro `@unifesspa/uniplus-client`) preenche automaticamente.
- **Carga cognitiva inicial maior.** Time interno e integradores precisam aprender a convenção; é estranho para quem vem de stacks que usam path versioning. Mitigação: ADR + entrada dedicada no portal explicando o esquema.
- **Versão única por response.** Não é possível pedir "campos da v2 mas estrutura da v1" — versão é monolítica por recurso. Trade-off intencional; granularidade fina cria explosão combinatória.
- **Fallback `application/json` é ambíguo por design.** Quem usa `Accept: application/json` recebe a versão atual e sofre breaking change quando a "atual" muda. Mitigação: documentação explícita de que isso é caminho exploratório, não recomendado para integradores institucionais.

### Neutras

- A escolha não impede pinning per-account ser adicionado em V2 (via header opcional `Api-Pin: edital=v1`) sem invalidar esta ADR.
- Endpoints administrativos internos podem viver sem versionamento até serem expostos publicamente; quando forem, entram nesse esquema.

## Confirmação

1. **Spectral rule no CI** — toda response 200 declarada no spec OpenAPI deve ter `content` com chave começando por `application/vnd.uniplus.<resource>.v<N>+json`. Falha indica reintrodução de `application/json` puro como única opção em endpoint não-experimental.
2. **Smoke E2E (Postman/Newman)** — cenários cobrem (a) request com versão atual retorna 200 + `Content-Type` correspondente; (b) request com `Accept: application/json` retorna 200 + `Content-Type` da versão atual; (c) request com versão inexistente retorna 406 com `code: uniplus.contract.versao_nao_suportada` + extension `available_versions`; (d) request a recurso em fase `deprecated` retorna response normal com headers `Deprecation` e `Sunset`.
3. **Auditoria do changelog** — script no CI valida que toda entrada do changelog (`developers.uniplus.unifesspa.edu.br/changelog`) tem recurso, versão, tipo de mudança e timestamp; PR que adiciona versão sem entrada falha.
4. **Revisão arquitetural de PR** — PR que adicione versão nova de recurso deve incluir: implementação do formatter para a versão, entrada no portal, entrada no changelog, atualização do spec OpenAPI. Critério explícito no checklist do PR template.

## Mais informações

- [ADR-0022](0022-contrato-rest-canonico-umbrella.md) — umbrella (princípio 5: API pública desde o dia 1).
- [ADR-0015](0015-rest-contract-first-com-openapi.md) — REST contract-first; esta ADR especializa a granularidade do versionamento.
- [ADR-0019](0019-proibir-pii-em-path-segments-de-url.md) — path carrega identidade, não metadado.
- [ADR-0023](0023-wire-formato-erro-rfc-9457.md) — wire format do 406 retornado quando versão é inválida.
- [ADR-0025](0025-wire-formato-sucesso-body-direto.md) — body é representação direta; `Content-Type` descreve a representação.
- [ADR-0030](0030-openapi-3-1-contract-first.md) — pipeline OpenAPI que descreve cada versão como response separada.
- [RFC 6838 — Media Type Specifications and Registration Procedures](https://www.rfc-editor.org/rfc/rfc6838.html).
- [RFC 6839 — Additional Media Type Structured Syntax Suffixes](https://www.rfc-editor.org/rfc/rfc6839.html) — base do suffix `+json`.
- [RFC 8594 — The Sunset HTTP Header Field](https://www.rfc-editor.org/rfc/rfc8594.html).
- [RFC 9745 — The Deprecation HTTP Header Field](https://www.rfc-editor.org/rfc/rfc9745.html).
- [Stripe API Versioning](https://docs.stripe.com/api/versioning) — opção C rejeitada referenciada.
