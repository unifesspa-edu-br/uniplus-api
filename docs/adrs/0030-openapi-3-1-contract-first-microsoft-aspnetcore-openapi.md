---
status: "accepted"
date: "2026-05-03"
decision-makers:
  - "Tech Lead (CTIC)"
---

# ADR-0030: Geração de OpenAPI 3.1 via `Microsoft.AspNetCore.OpenApi` com pipeline de pós-processamento

## Contexto e enunciado do problema

A [ADR-0015](0015-rest-contract-first-com-openapi.md) decidiu que a `uniplus-api` é contract-first com OpenAPI mas deixou o tooling em aberto. Esta ADR completa aquela decisão registrando como o spec é gerado, validado, distribuído e mantido em sincronia com o código.

O `Program.cs` atual de cada módulo registra apenas `AddEndpointsApiExplorer()` — não há infraestrutura OpenAPI. A escolha precisa cobrir três dimensões: (i) qual gerador (built-in `Microsoft.AspNetCore.OpenApi`, `Swashbuckle.AspNetCore`, `NSwag` ou autoria manual de YAML); (ii) granularidade do spec (um por API ou um por módulo); (iii) pipeline de pós-processamento que transforma o spec gerado no contrato publicado para integradores.

A umbrella ([ADR-0022](0022-contrato-rest-canonico-umbrella.md), princípio 6) já estabeleceu que padrões abertos são binding. As ADRs irmãs do contrato V1 ([ADR-0023](0023-wire-formato-erro-rfc-9457.md), [ADR-0026](0026-paginacao-cursor-opaco-cifrado.md), [ADR-0028](0028-versionamento-per-resource-content-negotiation.md), [ADR-0029](0029-hateoas-level-1-links.md)) já introduziram extensões e padrões (`code` regex, vendor media types, `legal_reference`, `_links`, cursor opaco) que precisam ser refletidos no spec — o tooling escolhido precisa permitir injetar esses elementos sem reescrita por endpoint.

## Drivers da decisão

- **Suporte nativo do .NET 10.** `Microsoft.AspNetCore.OpenApi` é shipado e mantido pela Microsoft, com OpenAPI 3.1 default. Eliminar dependência de pacote comunitário reduz risco de drift quando novas versões do .NET evoluírem o suporte OpenAPI.
- **Granularidade alinhada à modularidade.** A `uniplus-api` é monolito modular (Selecao, Ingresso, futuros). Cada módulo tem ownership independente e cadência de versionamento per-resource ([ADR-0028](0028-versionamento-per-resource-content-negotiation.md)) — um spec por módulo evita coordenação artificial entre owners.
- **Spec é contrato com terceiros.** O spec exportado e committed (`contracts/openapi.<modulo>.json`) é o que integradores institucionais e codegen consomem. PR que altera comportamento sem regerar e committar o spec quebra esse contrato silenciosamente.
- **Drift code↔spec é o pior dos mundos.** Spec desatualizado é pior que ausência de spec — causa decisões erradas em integradores. CI precisa falhar PR que não regenerou o spec.
- **Reflexão das ADRs irmãs no spec.** Vendor media types per-resource ([ADR-0028](0028-versionamento-per-resource-content-negotiation.md)), `code` regex e `legal_reference` extension ([ADR-0023](0023-wire-formato-erro-rfc-9457.md)), cursor opaco ([ADR-0026](0026-paginacao-cursor-opaco-cifrado.md)), `_links` ([ADR-0029](0029-hateoas-level-1-links.md)) e `Sunset`/`Deprecation` headers ([ADR-0028](0028-versionamento-per-resource-content-negotiation.md)) precisam aparecer no spec sem trabalho manual por endpoint.
- **Codegen `.NET` e Angular.** Clientes para consumo cross-módulo (interno) e para o `uniplus-web` (Angular) saem do mesmo spec — o pipeline precisa ser composable, não monolítico.

## Opções consideradas

- **A. `Microsoft.AspNetCore.OpenApi` (built-in .NET 10) + um spec por módulo + pipeline de pós-processamento** (transformers + Spectral + Redocly bundle + drift check) committando o spec em `contracts/openapi.<modulo>.json`.
- **B. `Swashbuckle.AspNetCore`** — gerador legado, manutenção comunitária pós-.NET 10.
- **C. `NSwag`** — gerador code-first com codegen integrado.
- **D. Contract-first puro com `openapi.yaml` autorado à mão** — código gerado a partir do spec.
- **E. Um único `openapi.json` unificado** para toda a `uniplus-api`.
- **F. Híbrido per-módulo + agregado gerado** — specs por módulo como fonte + agregador build-time que produz spec único para SDKs que queiram cliente unificado.

## Resultado da decisão

**Escolhida:** "A — `Microsoft.AspNetCore.OpenApi` built-in + um spec por módulo + pipeline de pós-processamento", porque é a única opção que combina (i) ownership Microsoft do gerador, (ii) granularidade alinhada ao modular monolith, (iii) reflexão automática das extensões via transformers, e (iv) drift check committado dando ao spec o mesmo rigor de revisão que o código.

### Conciliação code-first vs contract-first

Esta ADR resolve a aparente tensão entre "contract-first" (princípio da ADR-0015) e "code-first" (forma do gerador escolhido):

- **Código é a fonte da verdade do spec gerado** — controllers, atributos, XML doc comments e exemplos em arquivos de fixture produzem o spec.
- **O spec exportado e committed (`contracts/openapi.<modulo>.json`) é o contrato binding** com integradores — codegen, SDKs e portal consomem essa cópia, não o endpoint runtime.
- **CI enforça que spec gerado bate com spec committed** em todo PR. Mudou comportamento? Regen e commit. Não regenerou? PR falha.

Na prática, isso entrega contract-first com 95% do rigor e 30% do custo de manutenção da autoria manual em YAML.

### Tooling stack

- **`Microsoft.AspNetCore.OpenApi` 1.x** (.NET 10) — registrado por módulo: `services.AddOpenApi("selecao")`, `services.AddOpenApi("ingresso")`. Cada módulo expõe o próprio documento em `/openapi/{documentName}.json`.
- **`Microsoft.OpenApi` 1.6+** — para os transformers programáticos (`IOpenApiDocumentTransformer`, `IOpenApiOperationTransformer`, `IOpenApiSchemaTransformer`).
- **Spectral** (Stoplight CLI) — lint do spec committed com ruleset Uni+ versionado em `tools/spectral/.spectral.yaml`.
- **`redocly-cli`** — bundle do spec (resolve `$ref` cross-file e produz key ordering canônico) + validação OpenAPI 3.1 estrita.
- **`Microsoft.OpenApi.Kiota`** — codegen de cliente .NET para consumo cross-módulo interno e SDKs futuros.
- **`@openapitools/openapi-generator-cli`** — codegen de cliente Angular consumido pelo `uniplus-web`.

### Pipeline de pós-processamento (CI a cada PR e a cada merge na main)

1. **Build .NET.** `dotnet build` produz os assemblies dos módulos.
2. **Extração do spec por módulo.** Spawn efêmero hits `/openapi/selecao.json` e `/openapi/ingresso.json` capturando o spec gerado de cada módulo.
3. **Aplicação dos transformers Uni+** (ver "Transformers requeridos" abaixo).
4. **Lint via Spectral** com ruleset Uni+ (ver "Regras Spectral mínimas" abaixo). Falha o PR em violação.
5. **Bundle via `redocly-cli`** — resolve `$ref` cross-file (ex.: `ProblemDetails` em `contracts/shared.openapi.json` referenciado por todos os módulos) e produz key ordering canônico.
6. **Drift check.** Compara spec gerado+enriquecido+bundled com `contracts/openapi.<modulo>.json` committed. Diff → PR falha. Dev regenera e commita.
7. **Codegen .NET.** Kiota gera `Unifesspa.UniPlus.Selecao.Client` e `Unifesspa.UniPlus.Ingresso.Client`.
8. **Codegen Angular.** `openapi-generator-cli` gera `@uniplus/api-selecao-client` e `@uniplus/api-ingresso-client`.
9. **Publicação no portal.** No merge para main, specs são pushados para `uniplus-developers` onde Docusaurus + Redoc renderizam por módulo.

### Transformers requeridos (mínimos)

Três transformers cobrem a reflexão das ADRs irmãs no spec:

1. **`UniPlusDocumentTransformer` (`IOpenApiDocumentTransformer`)** — injeta `info.title`, `info.description`, `info.contact`, `info.license` em pt-BR; popula `servers[]` com URLs por ambiente; injeta `tags` agrupadas por subdomínio Uni+; declara componentes shared (`ProblemDetails`, `Cursor`, `_links`, paginação) via `$ref` para `contracts/shared.openapi.json`.
2. **`UniPlusOperationTransformer` (`IOpenApiOperationTransformer`)** — injeta exemplos de request/response a partir de `contracts/examples/<modulo>/<resource>/<operacao>.json`; popula extensões custom `x-uniplus-norma`, `x-uniplus-legal-reference`, `x-uniplus-rn-numero` a partir de atributos em controllers; declara responses padrão (RFC 9457 ProblemDetails para 4xx/5xx, vendor media types para 2xx); injeta headers padrão (`Sunset`, `Deprecation`, `Link`) em operações cuja versão de recurso esteja em fase deprecated.
3. **`UniPlusSchemaTransformer` (`IOpenApiSchemaTransformer`)** — enriquece descrições de schema a partir de XML doc comments em pt-BR; aplica regex de validação no schema do `code` (`^[a-z]+(\.[a-z_]+)+$`); marca campos opcionais vs requeridos conforme metadados de validação.

### Regras Spectral mínimas

O ruleset Uni+ em `tools/spectral/.spectral.yaml` enforça:

1. **`uniplus-pt-br-descriptions`** — `info.description`, `tags[].description`, `paths.*.*.summary` e `paths.*.*.description` devem existir e estar em pt-BR (heurística: presença de caracteres com diacrítico ou de stopwords como "de", "para", "com" indica pt-BR; ausência total de minúsculas em alfabeto latino estendido falha).
2. **`uniplus-error-code-regex`** — toda response `4xx`/`5xx` referencia `ProblemDetails` shared; o campo `code` no schema tem `pattern: ^[a-z]+(\.[a-z_]+)+$`.
3. **`uniplus-problem-details-ref-reuse`** — `ProblemDetails` é definido apenas em `contracts/shared.openapi.json` e referenciado via `$ref` por todos os módulos; definição inline em endpoint individual falha.
4. **`uniplus-deprecated-needs-sunset`** — toda operação com `deprecated: true` declara header `Sunset` (timestamp HTTP-date) na response e atributo custom `x-uniplus-sunset-iso` no nível da operação.

Regras adicionais entram conforme padrões de erro recorrentes aparecem em PR review; cada nova regra é documentada no próprio arquivo do ruleset.

### Um spec por módulo + shared

- **`contracts/openapi.selecao.json`** — Selecao: `Edital`, `Inscricao`, `Classificacao`, etc.
- **`contracts/openapi.ingresso.json`** — Ingresso: `Chamada`, `Convocacao`, `Matricula`, etc.
- **`contracts/shared.openapi.json`** — types compartilhados (`ProblemDetails`, `Cursor`, `_links`, paginação envelopes) referenciados via `$ref` pelos módulos. O bundle do `redocly-cli` inlined essas definições nas cópias distribuídas.
- Módulos futuros (Auxílio Estudantil, Pesquisa) seguem o mesmo padrão de path.

### Spike gate dia-1 da implementação

Antes de comprometer o pipeline na slice pilot, **spike obrigatório**: validar que `Microsoft.AspNetCore.OpenApi` emite spec OpenAPI 3.1 válido para um endpoint pilot do `EditalController` com **vendor media type `application/vnd.uniplus.edital.v1+json`** declarado em `Produces`/`Consumes`. Se o built-in não declarar o media type corretamente no spec gerado, fallback documentado é autoria programática via `Microsoft.OpenApi` (mesma família Microsoft, mais verboso, controle mais baixo nível). Spike é 1-2 dias; resultado entra no PR pilot, não bloqueia o milestone se planejado cedo.

### Esta ADR não decide

- Caminho exato do projeto que hospeda os transformers (`tools/openapi-pipeline/` separado vs assembly da própria API com classe interna) — escolha de organização da implementação.
- Convenção exata de pastas para `contracts/examples/` — sugestão razoável é `contracts/examples/<modulo>/<resource>/<operacao>-<status>.json`, decisão final na slice pilot.
- Política de cache do pipeline em CI (Nx cache, GitHub Actions cache) — otimização de implementação.
- Distribuição dos pacotes de cliente codegen (NuGet interno, pacote local, npm registry interno) — decisão de infra fora do escopo da ADR.

### Por que B, C, D, E e F foram rejeitadas

- **B (Swashbuckle).** Manutenção comunitária pós-.NET 10; suporte OpenAPI 3.1 incompleto; risco de drift relativo à evolução Microsoft. Migrar de Swashbuckle para built-in depois custaria rework — adotar built-in desde o V1 elimina essa migração.
- **C (NSwag).** Toolchain monolítica (gerador + codegen integrado) sem benefício composto sobre best-of-breed (Microsoft + Kiota + openapi-generator) na escala V1. Suporte de segunda classe relativo ao built-in.
- **D (contract-first puro com YAML).** Hand-authoring de 30-50 endpoints em 2+ módulos é alto custo cognitivo; manter sync com código vira disciplina dispendiosa; não há caminho .NET-nativo que gere skeletons OpenAPI 3.1 com fidelidade necessária para Wolverine + Clean Architecture; Kiota server codegen está em preview.
- **E (spec unificado).** Versionar um único recurso (ex.: `Inscricao.v2`) força tocar o spec único, aumentando coordenação cross-módulo; ownership por módulo se dilui no artefato; deprecation per-módulo difícil de expressar; hierarquia do portal arbitrária.
- **F (híbrido per-módulo + agregado).** Dois artefatos para manter; agregador é infra custom; agregado dá falsa impressão de "uma API" quando a realidade é múltiplos módulos com ciclos diferentes. Sem consumidor real demandando agregado no V1; se aparecer no V2, `redocly-cli bundle` produz na hora.

## Consequências

### Positivas

- **Microsoft owns o gerador.** Sem risco de drift comunitário no surface OpenAPI 3.1.
- **Code é fonte de verdade do spec gerado, spec committed é contrato binding, drift check é guardião.** PR review verifica comportamento e contrato no mesmo lugar.
- **Per-module specs alinham com modular monolith.** Owners controlam contrato do próprio módulo independentemente.
- **Spectral ruleset enforça house style.** Qualidade de spec é gated em CI, não aspiracional.
- **Pipeline composable.** Cada passo (extract, transform, lint, bundle, drift check, codegen) é independentemente scriptável e substituível.
- **Codegen .NET + Angular** sem trancar em toolchain monolítica.

### Negativas

- **Spec quality reflete code quality.** XML doc comments, atributos, exemplos em fixture viram artefatos de primeira classe — precisam do mesmo rigor de revisão que o código.
- **Pipeline é infra CI não-trivial.** ~5-7 passos com tratamento de erro. Setup inicial leva tempo desproporcional ao valor entregue por slice — amortizado nas demais.
- **Coordenação de shared types.** `contracts/shared.openapi.json` precisa permanecer backward-compatível; mudança ali ripples para todos os módulos. Adições requerem PR separado com revisão explícita do impacto cross-módulo.
- **`Microsoft.AspNetCore.OpenApi` é mais novo que Swashbuckle.** Documentação e Stack Overflow mais finos; time vai encontrar edge cases que requerem ler código fonte do pacote.

### Neutras

- A escolha não impede gerar agregado (`redocly-cli bundle`) sob demanda no futuro se um integrador real pedir um cliente unificado.
- Endpoints administrativos internos podem ficar fora do spec público; quando forem expostos, entram no mesmo pipeline.

## Riscos e mitigações

- **Risco:** built-in não suporta vendor media types per-resource corretamente no spec gerado.
  **Mitigação:** spike dia-1 (descrito acima); fallback é `Microsoft.OpenApi` programmatic authoring.
- **Risco:** drift check falha repetidamente em mudanças cosméticas (key ordering, whitespace), virando ruído de CI.
  **Mitigação:** `redocly-cli bundle` normaliza key ordering antes do drift check; whitespace é normalizado na comparação.
- **Risco:** consumidor confuso sobre qual spec consumir para qual operação.
  **Mitigação:** landing page do portal mapeia módulos a specs; cada `info.title`/`info.description` declara claramente o escopo do módulo.
- **Risco:** pipeline lento degrada developer velocity.
  **Mitigação:** pipeline é module-scoped (só roda para módulos tocados pelo PR); cache de codegen via Nx ou Actions cache; benchmark inicial alvo: under 90s total.

## Confirmação

1. **Drift check em CI** — diff entre spec gerado pelo pipeline e `contracts/openapi.<modulo>.json` committed; PR falha em qualquer divergência.
2. **Spectral lint em CI** — ruleset Uni+ rodado em todo PR que toque controllers, contracts ou exemplos; PR falha em violação.
3. **Spike de validação do vendor media type** — entregue como PR pilot com endpoint Edital + spec gerado mostrando `application/vnd.uniplus.edital.v1+json` corretamente declarado.
4. **Smoke E2E (Postman/Newman)** — coleção é gerada a partir do spec committed; cenários verificam que comportamento real bate com contrato declarado.

## Mais informações

- [ADR-0022](0022-contrato-rest-canonico-umbrella.md) — umbrella (princípio 6: padrões abertos).
- [ADR-0015](0015-rest-contract-first-com-openapi.md) — princípio contract-first; esta ADR completa o tooling.
- [ADR-0023](0023-wire-formato-erro-rfc-9457.md) — `ProblemDetails` declarado em `contracts/shared.openapi.json` com extensions reflectidas no spec.
- [ADR-0024](0024-mapeamento-domain-error-http.md) — `code` regex enforçado pelo Spectral.
- [ADR-0025](0025-wire-formato-sucesso-body-direto.md) — body direto refletido nos schemas de response 2xx.
- [ADR-0026](0026-paginacao-cursor-opaco-cifrado.md) — cursor opaco e `Link` header documentados no spec.
- [ADR-0028](0028-versionamento-per-resource-content-negotiation.md) — vendor media types per-resource declarados no spec; headers `Sunset`/`Deprecation` em operações deprecated.
- [ADR-0029](0029-hateoas-level-1-links.md) — `_links` declarado como propriedade opcional dos schemas de recurso.
- [Microsoft — Generate OpenAPI documents in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/aspnetcore-openapi).
- [Microsoft.OpenApi.Kiota for .NET client codegen](https://learn.microsoft.com/en-us/openapi/kiota/dotnet-tutorial).
- [Spectral — OpenAPI linter](https://github.com/stoplightio/spectral).
- [Redocly CLI](https://redocly.com/docs/cli).
- [@openapitools/openapi-generator-cli](https://www.npmjs.com/package/@openapitools/openapi-generator-cli).
