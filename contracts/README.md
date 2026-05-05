# Contracts — OpenAPI baselines

Specs OpenAPI 3.1 versionados como **fonte de verdade do contrato V1** da `uniplus-api` (ADR-0030, story #290).

## Arquivos

- `openapi.selecao.json` — spec do módulo Seleção (endpoints `/api/editais`, `/api/auth/me`, `/api/profile/me`).
- `openapi.ingresso.json` — spec do módulo Ingresso (stub atual; endpoints próprios chegam em sprints posteriores).

## Como o spec é gerado

O pipeline `AddUniPlusOpenApi("modulo", config)` (ver `Infrastructure.Core/OpenApi/`) registra três transformers que formatam o documento em runtime:

1. `UniPlusInfoTransformer` — title pt-BR, contact CTIC, license MIT, servers Produção/Homologação, `info.version = 1.0.0`.
2. `UniPlusOperationTransformer` — adiciona header `Idempotency-Key` em endpoints com `[RequiresIdempotencyKey]`; coage respostas 4xx/5xx para `application/problem+json` (RFC 9457, ADR-0023).
3. `UniPlusSchemaTransformer` — aplica pattern `^\d{11}$` + nota PII a propriedades `cpf`.

Endpoint runtime: `GET /openapi/{modulo}.json`.

## Drift check

A integração `OpenApiEndpointTests` em ambos os módulos (`tests/Unifesspa.UniPlus.{Selecao,Ingresso}.IntegrationTests/OpenApiEndpointTests.cs`) compara o spec emitido em runtime com o baseline committed nesta pasta. **Qualquer mudança no contrato faz o teste falhar** — clientes externos (frontend, integradores, `uniplus-developers`) ficam protegidos contra breaking changes acidentais.

### Como regerar o baseline

```bash
UPDATE_OPENAPI_BASELINE=1 dotnet test tests/Unifesspa.UniPlus.Selecao.IntegrationTests --filter "FullyQualifiedName~SpecRuntime"
UPDATE_OPENAPI_BASELINE=1 dotnet test tests/Unifesspa.UniPlus.Ingresso.IntegrationTests --filter "FullyQualifiedName~SpecRuntime"
```

Os arquivos `contracts/openapi.{selecao,ingresso}.json` são reescritos. **Revise o diff** (`git diff contracts/`) e só commit se a mudança for intencional. PRs que mudam controllers sem regerar o baseline falham CI.

> **Nota sobre `NormalizeJson`**: o teste de drift canonicaliza apenas indentação/whitespace (via `JsonSerializer.Serialize` com `WriteIndented = true`); não reordena chaves. Se uma atualização do `Microsoft.OpenApi` reordenar campos no spec emitido (ex.: `schema.type` antes ou depois de `schema.pattern`), o teste falha como drift legítimo até a baseline ser regerada — comportamento correto, não falso positivo.

## Spectral house-style

`tools/spectral/.spectral.yaml` aplica 5 rules Uni+ específicas (pt-BR em info/summary, problem+json em 4xx/5xx, regex `^uniplus.*` em codes, deprecated→x-sunset, Idempotency-Key required em verbs idempotent) sobre os baselines committed. Job dedicado em `.github/workflows/ci.yml#spectral` falha o PR em violação.

## Roadmap (follow-ups)

Itens originalmente listados em #290 que ficam para próximos PRs (mantém este reviewable):

- `contracts/shared.openapi.json` declarando ProblemDetails, Cursor, `_links`, paginação envelopes via `$ref`.
- `contracts/postman/uniplus-api.postman_collection.json` com cenários smoke (criar edital com Idempotency-Key, listar com cursor, 406 vendor MIME inexistente, 422 validation).
- Newman em CI rodando contra a API em container — exige docker-compose com Postgres/Kafka/MinIO/Keycloak.

Esses follow-ups dependem do EditalController real estar exposto em ambiente integrado (HML/dev cluster) e da infra de CI suportar containers — fora do escopo de Milestone A.
