# Contracts — OpenAPI baselines e códigos de erro

Specs OpenAPI 3.1 versionados como **fonte de verdade do contrato V1** da `uniplus-api` (ADR-0030, story #290), e a lista dos códigos de erro de fio que a API emite.

## Arquivos

- `openapi.selecao.json` — spec do módulo Seleção (endpoints `/api/selecao/processos-seletivos`, `/api/selecao/obrigatoriedades-legais`, `/api/profile/me`).
- `openapi.ingresso.json` — spec do módulo Ingresso (stub atual; endpoints próprios chegam em sprints posteriores).
- `openapi.autorizacao.json` — spec do módulo Autorização (perfis de acesso, sob o prefixo `/api/autorizacao/*`).
- `openapi.organizacao.json` — spec do módulo Organização Institucional (Instituição e Unidades, sob o prefixo de módulo `/api/organizacao/*`, incluindo as variantes administrativas `/api/organizacao/admin/*`). Áreas Organizacionais ficam fora do contrato — em aposentadoria (issue #625), o controller foi removido.
- `openapi.configuracao.json` — spec do módulo Configuração (Campus e Local de Oferta, sob o prefixo de módulo `/api/configuracao/*`, incluindo as variantes administrativas `/api/configuracao/admin/*`). A Cidade é referenciada por código IBGE + display cache (ADR-0090) — não há endpoint de cidade aqui.
- `openapi.publicacoes.json` — spec do módulo Publicações (atos normativos e catálogo de tipos de ato, sob o prefixo `/api/publicacoes/*`, incluindo `/api/publicacoes/admin/*`).
- `openapi.portal.json` — spec da Portal API, o *Backend for Frontend* público do portal do candidato ([ADR-0131](../docs/adrs/0131-portal-como-bff-publico-de-dominio.md)), sob o prefixo `/api/portal/*`. Nasce só com o endpoint de teste; cada endpoint público novo regenera esta baseline.
- `codigos-de-erro.json` — lista dos códigos de erro de fio (`uniplus.<módulo>.<assunto>.<motivo>`) que a API emite, com o status HTTP e o título de cada um. Gerado do código, não escrito à mão; ver [Códigos de erro](#códigos-de-erro).
- Registro de acesso (tópico `autorizacao_registros_de_acesso`): o valor da mensagem é Avro, pelo padrão da [ADR-0051](../docs/adrs/0051-apicurio-schema-registry-avro-wolverine.md), e o esquema é o `LoteDoRegistroDeAcesso.avsc`, embutido em `src/shared/Unifesspa.UniPlus.Authorization/Contracts/Schemas/`, sem cópia nesta pasta: é a mesma fonte que a API registra no Schema Registry na subida, no subject `<tópico>-value`. Versão 1: só entra mudança compatível para trás (campo opcional novo, com default nulo; valor novo nos vocabulários de motivo da negativa, fonte e sensibilidade, que são texto e não mudam o esquema); mudança incompatível cria a `v2` e muda o cabeçalho `uniplus.versao-schema`. O teste de contrato do `Infrastructure.Core.UnitTests` lê o lote serializado de volta pelo esquema e confere que os campos do esquema são as propriedades do registro, nos dois sentidos.
- `selecao/configuracao/v1/*.schema.json` — JSON Schema (draft 2020-12) do documento de configuração do processo seletivo, versão 1, dividido por assunto em arquivos que se referenciam por `$ref` relativo (`documento` é a raiz; `comuns` guarda as definições compartilhadas; `$id` absoluto sob `https://uniplus.unifesspa.edu.br/contracts/selecao/configuracao/v1/`): o formato do rascunho salvo, da importação e da exportação. Descreve só a forma (tipo, formato, enumerações, limites de tamanho, obrigatoriedade estrutural); a regra de negócio fica no domínio, e por isso um rascunho incompleto é válido. Catálogos e unidades entram por código estável, nunca por identificador interno do banco; as referências entre partes do documento usam a `chave` local de cada parte. Os exemplos completos estão em `selecao/configuracao/exemplos/`. Os objetos recusam propriedade desconhecida, então a compatibilidade da `v1` é "documento válido na `v1` continua válido": entram na `v1` a propriedade opcional nova, o valor novo de uma enumeração e o relaxamento de um limite; criam a `v2` (outro arquivo, outro `$id`, outro `versaoFormato`) remover ou renomear propriedade, tornar uma propriedade obrigatória, estreitar tipo, formato, limite ou enumeração, e mudar o significado de um campo. O teste de contrato do `Infrastructure.Core.UnitTests` valida os exemplos e confere que o rascunho incompleto é aceito e que tipo errado, propriedade desconhecida e identificador interno no lugar do código são recusados com o caminho. A exportação traz, ao lado de cada código, o descritivo que o Uni+ já guarda (nomes, siglas, UF) e os valores derivados, todos marcados `readOnly`: a importação os ignora, a referência é o código e o servidor recalcula o que é derivado. Nenhum campo interno entra: id do banco, datas de auditoria e autoria ficam fora, e cada item se identifica pela `chave` local. O documento exportado traz, na raiz e só de leitura, o `id` do processo (uuid; no rascunho, o id do próprio rascunho, que vira o do processo na publicação; ignorado na importação, que gera id novo; não concede pertencimento) e a `versao` exportada (inteiro, ausente no rascunho); os itens não têm id. Traz também `publicadoEm`, o instante da divulgação da versão (na retificação, o da versão retificada), só de leitura e ausente no rascunho. Regra da identidade: o item de lista que o usuário edita e que pode ser alcançado por decisão, fato ou retificação tem `chave` local, porque o rótulo é texto editável e corrigir a grafia numa retificação não pode virar retirada e inclusão; o item identificado por códigos de catálogo (como o produto de uma fase, por `atoCodigo` e `papel`) mantém a identidade natural, e o objeto único por pai fica sem chave.
- `formularios/casos/` — corpus de casos da avaliação do formulário, rodado pela API e pelo interpretador do front (ADR-0139); o formato está no README da pasta.

## Como o spec é gerado

O pipeline `AddUniPlusOpenApi("modulo", config)` (ver `Infrastructure.Core/OpenApi/`) registra os transformers que formatam o documento em runtime:

1. `UniPlusInfoTransformer` — title pt-BR, contact CTIC, license MIT, servers Produção/Homologação, `info.version = 1.0.0`.
2. `UniPlusOperationTransformer` — adiciona header `Idempotency-Key` em endpoints com `[RequiresIdempotencyKey]`; coage respostas 4xx/5xx para `application/problem+json` (RFC 9457, ADR-0023).
3. `UniPlusSchemaTransformer` — aplica pattern `^\d{11}$` + nota PII a propriedades `cpf`.
4. `BearerSecuritySchemeDocumentTransformer` — declara o esquema `bearerAuth` (HTTP Bearer, formato JWT) em `components.securitySchemes`; o `AuthorizationOperationTransformer` referencia esse esquema em cada operação protegida, na mesma passagem em que adiciona o `401`/`403`.
5. `InstanteSchemaTransformer` — publica todo instante (`DateTimeOffset`, anulável, em lista ou em dicionário) como `type: string` + `format: date-time`: o conversor JSON do instante é próprio, e sem ele o gerador omite o `type` e o schema do item.

Endpoint runtime: `GET /openapi/{modulo}.json`.

## Drift check

A integração `OpenApiEndpointTests` nos módulos com baseline (`tests/Unifesspa.UniPlus.{Selecao,Ingresso,OrganizacaoInstitucional,Autorizacao,Configuracao,Publicacoes,Portal}.IntegrationTests/OpenApiEndpointTests.cs`) compara o spec emitido em runtime com o baseline committed nesta pasta. **Qualquer mudança no contrato faz o teste falhar** — clientes externos (frontend, integradores, `uniplus-developers`) ficam protegidos contra breaking changes acidentais.

### Como regerar o baseline

```bash
UPDATE_OPENAPI_BASELINE=1 dotnet test tests/Unifesspa.UniPlus.Selecao.IntegrationTests --filter "FullyQualifiedName~SpecRuntime"
UPDATE_OPENAPI_BASELINE=1 dotnet test tests/Unifesspa.UniPlus.Ingresso.IntegrationTests --filter "FullyQualifiedName~SpecRuntime"
UPDATE_OPENAPI_BASELINE=1 dotnet test tests/Unifesspa.UniPlus.OrganizacaoInstitucional.IntegrationTests --filter "FullyQualifiedName~SpecRuntime"
UPDATE_OPENAPI_BASELINE=1 dotnet test tests/Unifesspa.UniPlus.Autorizacao.IntegrationTests --filter "FullyQualifiedName~SpecRuntime"
UPDATE_OPENAPI_BASELINE=1 dotnet test tests/Unifesspa.UniPlus.Configuracao.IntegrationTests --filter "FullyQualifiedName~SpecRuntime"
UPDATE_OPENAPI_BASELINE=1 dotnet test tests/Unifesspa.UniPlus.Publicacoes.IntegrationTests --filter "FullyQualifiedName~SpecRuntime"
UPDATE_OPENAPI_BASELINE=1 dotnet test tests/Unifesspa.UniPlus.Portal.IntegrationTests --filter "FullyQualifiedName~SpecRuntime"
```

Os arquivos `contracts/openapi.*.json` correspondentes são reescritos. **Revise o diff** (`git diff contracts/`) e só commit se a mudança for intencional. PRs que mudam controllers sem regerar o baseline falham CI.

> **Nota sobre `NormalizeJson`**: o teste de drift canonicaliza apenas indentação/whitespace (via `JsonSerializer.Serialize` com `WriteIndented = true`); não reordena chaves. Se uma atualização do `Microsoft.OpenApi` reordenar campos no spec emitido (ex.: `schema.type` antes ou depois de `schema.pattern`), o teste falha como drift legítimo até a baseline ser regerada — comportamento correto, não falso positivo.

## Códigos de erro

O código de erro vai no corpo `application/problem+json` (ADR-0023), e não é um schema do OpenAPI. `codigos-de-erro.json` o torna um contrato de arquivo: o lint do catálogo de erros do portal (`uniplus-developers`) o lê para conferir que cada código tem página, sem depender de ler o código-fonte da API.

### Formato

```json
{
  "versaoDoFormato": 1,
  "codigos": [
    { "codigo": "uniplus.auth.forbidden", "status": 403, "titulo": "Acesso negado" }
  ]
}
```

- `versaoDoFormato`: inteiro; o consumidor confere antes de interpretar a lista. Muda só se o formato mudar, e não quando um código entra ou sai.
- `codigos`: uma entrada por código de fio, ordenada por `codigo` (ordem ordinal), para o diff entre versões mostrar só o que mudou. Cada entrada tem `codigo`, `status` (HTTP) e `titulo` (pt-BR, estável por código, o mesmo do campo `title` da resposta).

### De onde vem

O arquivo é gerado em memória pelo teste `CodigosDeErroPublicadosTests` (`tests/Unifesspa.UniPlus.ArchTests/SolutionRules/`), que reúne:

1. todas as `IDomainErrorRegistration` dos módulos e do compartilhado (Seleção, Ingresso, Configuração, Organização Institucional, Publicações, Autorização, Discentes, Portal e `Infrastructure.Core`), descobertas por tipo nos assemblies do produto;
2. as definições da fronteira HTTP (`ErrosDaFronteiraHttp`), de quem o `GlobalExceptionMiddleware`, o `AuthenticationProblemDetailsWriter`, o `VendorMediaTypeAttribute` e a recusa de contrato do controller de obrigatoriedades legais leem status, código e título. Um erro novo emitido fora dos registros nasce ali, e não como literal no emissor.

Uma exceção do conteúdo: `uniplus.contract.id_path_body_divergente` vai hoje no campo `type` da resposta 400, e não na extensão `code` que a ADR-0023 exige. Está na lista porque é o identificador estável que o controller emite; o cliente só o lê em `type` até a resposta ganhar o `code`.

O mesmo código declarado em mais de um registro com o mesmo status e título entra uma vez; com status ou título diferentes, a geração falha e aponta o código e os registros.

### Como regerar

```bash
UPDATE_CODIGOS_DE_ERRO_BASELINE=1 dotnet test tests/Unifesspa.UniPlus.ArchTests --filter "FullyQualifiedName~CodigosDeErroPublicados"
```

O teste falha, sem regerar, quando o arquivo diverge do código: código novo sem entrada, entrada sem código correspondente, ou status e título diferentes. A mensagem aponta o código e repete o comando acima. **Revise o diff** (`git diff contracts/codigos-de-erro.json`): código novo pede também a página em `uniplus-developers/docs/erros/`.

## Spectral house-style

`tools/spectral/.spectral.yaml` aplica 5 rules Uni+ específicas (pt-BR em info/summary, problem+json em 4xx/5xx, regex `^uniplus.*` em codes, deprecated→x-sunset, Idempotency-Key required em verbs idempotent) sobre os baselines committed. Job dedicado em `.github/workflows/ci.yml#spectral` falha o PR em violação.

## Roadmap (follow-ups)

Itens originalmente listados em #290 que ficam para próximos PRs (mantém este reviewable):

- ~~`contracts/shared.openapi.json` declarando ProblemDetails, Cursor, `_links`, paginação envelopes via `$ref`.~~ Resolvido por [ADR-0035](../docs/adrs/0035-shared-schemas-cross-module-fitness-test.md): Microsoft.AspNetCore.OpenApi 10 não suporta `$ref` cross-document, e Redocly post-process é desproporcional para 3 schemas duplicados. Em vez disso, `OpenApiSharedSchemasInSyncTests` (`tests/Unifesspa.UniPlus.ArchTests/SolutionRules/`) faz fitness check byte-a-byte dos schemas com mesmo nome em baselines diferentes. Reavaliar quando ≥10 schemas compartilhados, terceiro módulo, ou portal `uniplus-developers` demandar multi-file.
- `contracts/postman/uniplus-api.postman_collection.json` com cenários smoke (criar processo seletivo com Idempotency-Key, listar com cursor, 406 vendor MIME inexistente, 422 validation).
- Newman em CI rodando contra a API em container — exige docker-compose com Postgres/Kafka/MinIO/Keycloak.

Esses follow-ups dependem dos controllers do módulo Seleção estarem expostos em ambiente integrado (HML/dev cluster) e da infra de CI suportar containers — fora do escopo de Milestone A.
