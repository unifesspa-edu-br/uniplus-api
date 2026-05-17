---
name: smoke-crud
description: "Smoke test do ciclo CRUD de um recurso REST contra a API local Uni+ (Keycloak unifesspa-dev-local + selecao-api/ingresso-api). Cobre LIST + POST + replay idempotente + 409 conflito + GET com HATEOAS + PUT + DELETE soft + 404 pós-delete + UNIQUE parcial + histórico forense no PG. Recursos descritos via manifesto JSON em resources/."
argument-hint: "--resource=<nome> [--methods=POST,GET,PUT,DELETE,LIST,ALL] [--api=selecao|ingresso|portal] [--user=admin|gestor|...] [--realm=<realm>] [--keep-going] [--no-cleanup] [--verbose]"
---

# Skill: Smoke test de CRUD local contra a API Uni+

Executa o ciclo CRUD completo de um recurso REST contra o stack local (`docker compose -f docker/docker-compose.yml -f docker/docker-compose.override.yml up`), respeitando todas as invariantes do contrato V1:

- OIDC via Keycloak no realm `unifesspa-dev-local` (cliente `selecao-web`, password grant)
- Vendor MIME `application/vnd.uniplus.<resource>.v<N>+json`
- `Idempotency-Key` em POST/PUT/DELETE (ADR-0027)
- ProblemDetails RFC 9457 com taxonomy `uniplus.<modulo>.<recurso>.<erro>` (ADR-0023)
- HATEOAS Level 1 (`_links.self/collection`, ADR-0049)
- Soft delete + UNIQUE parcial (ADR-0058)

Cada recurso testado é descrito num arquivo de manifesto em `resources/<recurso>.json` — adicionar um novo CRUD é só copiar o template e ajustar payload + paths.

## Gatilhos de uso

Invoque quando o usuário pedir:

- "valida o CRUD da api local"
- "smoke test do recurso X"
- "executa fluxo CRUD contra a api local"
- "testa POST/GET/PUT/DELETE de Y localmente"
- `/smoke-crud …` ou similar

## Pré-condições

1. Stack local rodando: `docker compose -f docker/docker-compose.yml -f docker/docker-compose.override.yml --env-file docker/.env --project-directory docker up -d`
2. APIs em healthy: `selecao-api` em `:5202`, `ingresso-api` em `:5262`
3. Keycloak com realm `unifesspa-dev-local` importado (vem do `realm-export-dev-local.json`)
4. PostgreSQL acessível via container `docker-postgres-1`

A skill **não sobe** o stack — se as APIs estão down ela falha cedo e orienta o usuário a subir.

## Argumentos parseados pelo agente

| Flag | Valores | Default | Significado |
|---|---|---|---|
| `--resource=<nome>` | nome de manifesto em `resources/` | **obrigatório** | Identifica qual recurso testar. |
| `--methods=<lista>` | `POST,GET,PUT,DELETE,LIST,ALL` | `ALL` | Lista CSV de operações ou `ALL`. |
| `--api=<modulo>` | `selecao,ingresso,portal,…` | extraído do manifesto | Sobrescreve o módulo do manifesto. |
| `--user=<username>` | `admin,gestor,avaliador,candidato` | `admin` | Usuário do realm `unifesspa-dev-local`. |
| `--realm=<realm>` | qualquer realm KC | `unifesspa-dev-local` | Para apontar para outro realm que tenha `selecao-web` com direct grant. |
| `--keep-going` | flag | off | Não aborta no primeiro erro; reporta todos. |
| `--no-cleanup` | flag | off | Mantém o recurso criado (não roda DELETE). |
| `--verbose` | flag | off | Imprime body completo de cada resposta. |

## Fluxo de execução

1. **Validar pré-condições** — `curl /health` em ambas APIs; se não responder, parar com mensagem orientativa.
2. **Obter token** — password grant em `http://localhost:8080/realms/${realm}/protocol/openid-connect/token` via client `selecao-web` com `--user` + `Changeme!123`. Aborta se token vazio (orientação: confirmar que o realm dev-local foi importado).
3. **Carregar manifesto** — `resources/${resource}.json`.
4. **Executar verbos** conforme `--methods`:
   - `LIST`: `GET /api/<api>/<recurso>` (público com `Authorization` opcional)
   - `POST`: `POST /api/<api>/admin/<recurso>` com body do manifesto + `Idempotency-Key`
   - Replay: re-POST com mesma chave → confirma mesmo ID retornado
   - Conflito: re-POST com chave diferente → confirma 409 `regra_codigo_duplicada`
   - `GET`: `GET /api/<api>/<recurso>/{id}` → confirma `_links.self`
   - `PUT`: `PUT /api/<api>/admin/<recurso>/{id}` com body atualizado + chave nova
   - `DELETE`: `DELETE /api/<api>/admin/<recurso>/{id}` → 204
   - GET pós-DELETE: confirma 404
   - POST mesmo `regraCodigo` pós-DELETE: confirma 201 com **novo ID** (UNIQUE parcial)
   - Histórico no PG: `SELECT … FROM <recurso>_historico WHERE …` se manifesto declarar tabela
5. **Reportar** veredito por step em tabela `[OK]`/`[FAIL]` + códigos HTTP. Final: contagem de OK/FAIL.

## Formato de saída

```
=== smoke-crud: <recurso> ===
[OK]   POST                              201
[OK]   POST replay                       201 (mesmo ID)
[OK]   POST conflito (regraCodigo)       409 uniplus.<modulo>.<recurso>.regra_codigo_duplicada
[OK]   GET by ID                         200 (_links.self presente)
[OK]   PUT                               204
[OK]   GET pós-update                    200 (descricaoHumana alterada)
[OK]   DELETE                            204
[OK]   GET pós-delete                    404
[OK]   POST mesmo regraCodigo após DELETE 201 (novo ID)
[OK]   Histórico no PG                   3 snapshots
============================================
Resultado: 10/10 OK
```

## Adicionar um novo recurso

1. Copiar `resources/obrigatoriedade-legal.json` para `resources/<seu-recurso>.json`.
2. Ajustar `resource`, `api`, `pathPublic`, `pathAdmin`, `historicoTable` (opcional).
3. Editar `payload.create` e `payload.update` para casar com o contract do endpoint.
4. Rodar: `bash .claude/skills/smoke-crud/scripts/smoke-crud.sh --resource=<seu-recurso>`.

Se o endpoint não seguir o vendor MIME convention ou rotas `/admin/...` + `/<recurso>/...`, ajustar o template do manifesto antes de executar.

## Notas

- A skill assume realm `unifesspa-dev-local`. Se o realm canônico (`unifesspa`) for editado para também ter direct grant, pode usar `--realm=unifesspa`.
- Senhas: o script usa `Changeme!123` (valor que veio do `realm-export.json` original). Se o realm for restaurado a outro estado, ajustar via env var `SMOKE_CRUD_PASSWORD`.
- Toda interação fica em logs de cURL — `--verbose` revela payloads, mas não toca dados de produção (o script só fala com `localhost:5202`/`5262`/`8080`).
