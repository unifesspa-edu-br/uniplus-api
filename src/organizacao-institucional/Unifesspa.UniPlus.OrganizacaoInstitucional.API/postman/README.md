# Coleção Postman — API de Organização

Testes HTTP do módulo **OrganizacaoInstitucional**, versionados junto da própria
API (convenção: uma coleção por API, não compartilhada). Exercita o ciclo de
vida completo da **Instituição** singleton (issue #585) e um smoke de **Unidade**.

## Arquivos

- `organizacao.postman_collection.json` — a coleção (Postman v2.1).
- `organizacao.postman_environment.json` — ambiente local (dev): URLs, client e
  credenciais do realm `unifesspa-dev-local`. Valores **dev-only**, idênticos ao
  `docker/keycloak/realm-export-dev-local.json`.

## Pré-condições

Com a topologia de 3 APIs, o módulo Organização é servido pela **API UniPlus**
(o monólito modular, serviço `uniplus-api`), que declara dependência de Postgres,
Redis, Kafka, MinIO, Apicurio e **Keycloak** — subir a API já traz tudo que a
coleção precisa (o token sai do Keycloak):

```bash
docker compose -f docker/docker-compose.yml -f docker/docker-compose.override.yml \
  --env-file docker/.env --project-directory docker up -d uniplus-api
```

A API UniPlus fica em `:5200` (serve o módulo Organização sob `/api/organizacao/*`)
— é o `base_url` do environment. O Keycloak importa o realm `unifesspa-dev-local`
com o usuário `admin` (role `plataforma-admin`).

## Rodar (Newman)

```bash
cd repositories/uniplus-api
P=src/organizacao-institucional/Unifesspa.UniPlus.OrganizacaoInstitucional.API/postman
npx newman run "$P/organizacao.postman_collection.json" -e "$P/organizacao.postman_environment.json"
```

Ou importe ambos os arquivos no Postman e selecione o ambiente.

## O que é coberto

| Cenário | Verbo / rota | Esperado |
|---|---|---|
| Token plataforma-admin | `POST` token (Keycloak) | 200 + `access_token` |
| Limpeza (estado conhecido) | `GET /api/organizacao/instituicao` (+ `DELETE` se existir) | 200/404 → limpo |
| Criar sem auth | `POST /api/organizacao/admin/instituicao` | 401 |
| Criar sem Idempotency-Key | `POST /api/organizacao/admin/instituicao` | 400 |
| Criar sem campo obrigatório | `POST /api/organizacao/admin/instituicao` | 400/422 |
| Criar com unidade raiz inexistente | `POST /api/organizacao/admin/instituicao` | 422 `unidade_raiz_nao_encontrada` |
| Criar Instituição | `POST /api/organizacao/admin/instituicao` | 201 + Guid v7 |
| Obter (vendor MIME + HATEOAS) | `GET /api/organizacao/instituicao` | 200 + `_links.self` |
| Singleton: 2ª criação | `POST /api/organizacao/admin/instituicao` | 409 `ja_existe` |
| Atualizar | `PUT /api/organizacao/admin/instituicao/{id}` | 204 |
| Obter após atualização | `GET /api/organizacao/instituicao` | 200 + `situacao` Credenciada |
| Remover (soft-delete) | `DELETE /api/organizacao/admin/instituicao/{id}` | 204 |
| Obter pós-remoção (slot liberado) | `GET /api/organizacao/instituicao` | 404 |
| Smoke Unidade | `GET /api/organizacao/unidades` | 200 + vendor MIME + array |

A coleção é **auto-contida e re-executável**: limpa a Instituição no início e
remove a que cria ao final, deixando o estado limpo.
