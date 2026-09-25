# Coleção Postman — API de Publicações

Testes HTTP do módulo **Publicações**, versionados junto da própria API (convenção:
uma coleção por API). Exercita o ciclo de vida do cadastro de tipos de ato:
autenticação `plataforma-admin`, `Idempotency-Key` no `POST`, vendor MIME por
recurso, ProblemDetails, HATEOAS, janela de vigência semiaberta e exclusão lógica.

## Arquivos

- `publicacoes.postman_collection.json` — a coleção (Postman v2.1).
- `publicacoes.postman_environment.json` — ambiente local (dev): URL da API, URL de
  token do Keycloak, client e credenciais. Valores **dev-only** (os padrões são os do
  realm `unifesspa-dev-local`); todos podem ser sobrescritos por `--env-var`.

## Pré-condições

O módulo Publicações é servido pela **API UniPlus** (monólito modular, serviço
`uniplus-api`, porta `:5200`). As rotas administrativas exigem o papel
`plataforma-admin` (usuário `admin`).

A coleção roda contra a stack local em **um de dois modos**, conforme o realm que
a API valida:

| Modo | Realm validado pela API | Quando usar |
|---|---|---|
| Stack com override padrão | `unifesspa` (o mesmo dos frontends) | a stack já está de pé para desenvolvimento — **não recrie containers** |
| Stack de smoke | `unifesspa-dev-local` | ambiente dedicado a smoke, sem frontends em uso |

**Atenção ao `docker-compose.smoke.yml`:** ele **não** sobe uma stack isolada. É
uma camada sobre o mesmo projeto Compose que só troca o `Auth__Authority` de
`uniplus-api`, `geo-api` e `portal-api`. Rodar o `up` com ele **recria esses
containers** da stack que estiver de pé, com o realm `unifesspa-dev-local`, e os
frontends (que usam `unifesspa`) passam a receber 401. Os dados continuam indo para
o mesmo Postgres. Use o modo de smoke só quando ninguém estiver usando a stack.

```bash
# Modo smoke (ambiente dedicado)
docker compose -f docker/docker-compose.yml -f docker/docker-compose.override.yml \
  -f docker/docker-compose.smoke.yml \
  --env-file docker/.env --project-directory docker up -d uniplus-api
```

### Token

A pasta **Auth** obtém o token por senha (ROPC). No realm `unifesspa`, o único
client que aceita ROPC é o `admin-cli`, e ele só emite token com `aud=uniplus`
(exigido pela API) depois de `scripts/setup-keycloak-dev.sh` — o `realm-export.json`
reflete a configuração de produção e não traz o scope `uniplus-profile` no
`admin-cli` (ver `docker/keycloak/README.md`). Sem o script, as requisições
recebem 401.

Alternativa sem ROPC: preencha a variável `access_token` com um token já emitido
(por exemplo, copiado do navegador depois do login num app do Uni+). Com ela
preenchida, a pasta Auth não chama o Keycloak e a coleção usa esse token.

## Rodar (Newman)

```bash
cd repositories/uniplus-api
P=src/publicacoes/Unifesspa.UniPlus.Publicacoes.API/postman

# Stack com override padrão (realm unifesspa)
npx --yes newman@6.2.1 run "$P/publicacoes.postman_collection.json" -e "$P/publicacoes.postman_environment.json" \
  --env-var keycloak_token_url=http://localhost:8080/realms/unifesspa/protocol/openid-connect/token \
  --env-var client_id=admin-cli --env-var 'password=<senha do admin no realm unifesspa>' \
  --reporters cli --reporter-cli-no-banner

# Stack de smoke (realm unifesspa-dev-local): os valores do environment já servem
npx --yes newman@6.2.1 run "$P/publicacoes.postman_collection.json" -e "$P/publicacoes.postman_environment.json" \
  --reporters cli --reporter-cli-no-banner

# Com token pronto
npx --yes newman@6.2.1 run "$P/publicacoes.postman_collection.json" -e "$P/publicacoes.postman_environment.json" \
  --env-var "access_token=$TOKEN" --reporters cli --reporter-cli-no-banner
```

Ou importe ambos os arquivos no Postman e selecione o ambiente.

## O que é coberto

| Folder | Cobre |
|---|---|
| **Auth** | Password grant contra o Keycloak do realm configurado, ou token pronto via `access_token` |
| **Tipos de ato** | Criação, leitura, atualização e vigência de um tipo de ato de teste, com as recusas de contrato |
| **Limpeza** | Remove o tipo de ato criado pela execução |

A coleção é **auto-contida e re-executável**: remove ao final o tipo de ato que cria.
