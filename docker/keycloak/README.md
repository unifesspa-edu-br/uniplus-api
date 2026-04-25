# Keycloak local

Este diretório contém a configuração do realm `unifesspa` usada no ambiente de desenvolvimento local.

## Por que existe um `realm-export.json` versionado

O arquivo `realm-export.json` é versionado para que qualquer pessoa do time suba o ambiente local com o realm `unifesspa` já configurado — clients OIDC, roles, client scopes, mappers e usuários de teste — sem precisar criar nada manualmente na console do Keycloak.

Isso mantém a configuração de autenticação e autorização do projeto padronizada, reproduzível e compartilhada.

> ⚠️ **Apenas para ambiente local.** Este realm contém credenciais de teste e configurações voltadas para desenvolvimento. Nunca importar este arquivo em homologação ou produção.

## Como subir

```bash
docker compose -f docker/docker-compose.yml up -d keycloak
```

- Console admin: http://localhost:8080
- Credenciais master: `admin` / `admin` (definidas em `docker-compose.yml`)
- OIDC discovery do realm: http://localhost:8080/realms/unifesspa/.well-known/openid-configuration

## Clients configurados

| `clientId`     | Tipo                | Uso                              | Redirect URI              |
| -------------- | ------------------- | -------------------------------- | ------------------------- |
| `selecao-web`  | público + PKCE S256 | App Angular **Seleção**          | `http://localhost:4200/*` |
| `ingresso-web` | público + PKCE S256 | App Angular **Ingresso**         | `http://localhost:4300/*` |
| `portal-web`   | público + PKCE S256 | App Angular **Portal**           | `http://localhost:4100/*` |
| `uniplus-api`  | confidential        | Backend .NET (validação de JWT)  | —                         |

Os 3 clients web são **públicos** (SPA) com **PKCE S256 obrigatório** — sem `client_secret`, conforme OAuth 2.1 / BCP for Browser-Based Apps.

## Client scope `uniplus-profile`

Scope compartilhado pelos clients, com dois mappers que injetam atributos do usuário como claims no ID Token, Access Token e `/userinfo`:

- `cpf` — string (tipo `String`, `multivalued: false`)
- `nomeSocial` — string

Esses claims são consumidos pelo frontend para preencher o perfil do usuário autenticado sem chamada extra ao backend.

## Roles (realm roles)

- `admin`
- `gestor`
- `avaliador`
- `candidato`

Correspondem às personas do sistema.

## Usuários de teste

Os quatro usuários abaixo são criados pela importação do realm.

| `username`           | Email                                   | Papel        |
| -------------------- | --------------------------------------- | ------------ |
| `admin@teste`        | `admin@teste.unifesspa.edu.br`          | `admin`      |
| `gestor@teste`       | `gestor@teste.unifesspa.edu.br`         | `gestor`     |
| `avaliador@teste`    | `avaliador@teste.unifesspa.edu.br`      | `avaliador`  |
| `candidato@teste`    | `candidato@teste.unifesspa.edu.br`      | `candidato`  |

- **Senha inicial (temporária):** `Changeme!123`
- No primeiro login o Keycloak exige trocar a senha (`temporary: true`).
- Cada usuário possui atributos `cpf` (CPF sintético com DV válido) e `nomeSocial`, expostos pelo scope `uniplus-profile`.

### Login por username ou email

A flag `loginWithEmailAllowed: true` do realm permite que o formulário de login aceite qualquer um dos dois identificadores:

- `admin@teste` ou `admin@teste.unifesspa.edu.br`
- (análogo para os demais usuários)

O campo `email` permanece único (`duplicateEmailsAllowed: false`) — requisito para que o email funcione como identificador de login. `registrationEmailAsUsername` fica `false`, mantendo `username` e `email` como campos distintos.

## Smoke tests via Direct Access Grants (ROPC)

Para validar o fluxo OIDC ponta-a-ponta sem subir o frontend Angular — útil em debug rápido, integração contínua local e validação de mudanças no pipeline JWT — usa-se Direct Access Grants (Resource Owner Password Credentials) contra o client `admin-cli`.

O `realm-export.json` reflete configuração de produção: senhas temporárias, `admin-cli` com lightweight access tokens (sem `sub`/`email`/atributos) e sem o scope `uniplus-profile`. Para destravar ROPC, rode o script de setup pós-import:

```bash
scripts/setup-keycloak-dev.sh
```

O script é idempotente e aplica via Admin API (sem alterar `realm-export.json`):

| Patch | Por quê |
|---|---|
| Atribui `uniplus-profile` ao `defaultClientScopes` do `admin-cli` | Sem isso, tokens emitidos via `admin-cli` não carregam `cpf`/`nomeSocial` nem `aud=uniplus` (validação de audience falha nas APIs) |
| Seta `client.use.lightweight.access.token.enabled=false` em `admin-cli` | Em Keycloak 26+, `admin-cli` vem com lightweight tokens — só `iss`/`azp`/`sid`/`scope`. Sem `sub`/`email`/atributos, o pipeline JWT rejeita |
| Reseta senha dos 4 usuários de teste como `temporary=false` e limpa `requiredActions` | Senhas temporárias forçam `UPDATE_PASSWORD` action no primeiro login; ROPC quebra com 401 `invalid_grant` |

Os ajustes vivem só na memória do Keycloak — `docker compose down -v` reverte tudo. Re-rode o script após recriar o volume do Postgres.

### Exemplo de fluxo completo

```bash
# 1. Stack docker no ar + setup pós-import
docker compose -f docker/docker-compose.yml up -d
scripts/setup-keycloak-dev.sh

# 2. Token de candidato
TOKEN=$(curl -s -X POST 'http://localhost:8080/realms/unifesspa/protocol/openid-connect/token' \
  -H 'Content-Type: application/x-www-form-urlencoded' \
  -d 'grant_type=password' -d 'client_id=admin-cli' \
  -d 'username=candidato' -d 'password=Changeme!123' | jq -r .access_token)

# 3. Endpoints autenticados
curl -s -H "Authorization: Bearer $TOKEN" http://localhost:5202/api/auth/me | jq
curl -s -H "Authorization: Bearer $TOKEN" http://localhost:5202/api/profile/me | jq
```

Resposta esperada de `/api/profile/me`:

```json
{
  "userId": "c1787064-fc4b-481a-9cbe-d8715932d4a0",
  "name": "Usuário Candidato",
  "email": "candidato@teste.unifesspa.edu.br",
  "cpf": "24843803480",
  "nomeSocial": "Candidato Teste",
  "roles": ["candidato"],
  "timestamp": "2026-04-25T01:06:24.367+00:00"
}
```

> ⚠️ Em produção, ROPC **não deve ser usado** — frontends Angular usam Authorization Code com PKCE (clients `selecao-web`, `ingresso-web`, `portal-web`). Os patches deste script são exclusivamente de ergonomia para desenvolvimento e CI local.

## Política de senha

O realm aplica a seguinte `passwordPolicy`:

```
length(8) and upperCase(1) and lowerCase(1) and digits(1) and notUsername
```

A senha escolhida na troca do primeiro login precisa atender a essas regras. A mesma política deve valer em produção, para alinhar dev com o ambiente real.

## Como forçar a reimportação

A estratégia de import é `IGNORE_EXISTING` — se o realm `unifesspa` já existir no banco do Keycloak, edições no `realm-export.json` não serão aplicadas em um restart simples.

Para forçar importação limpa, derrubar o ambiente com o volume do Postgres e subir novamente:

```bash
docker compose -f docker/docker-compose.yml down -v
docker compose -f docker/docker-compose.yml up -d keycloak
```

Confira o log esperado:

```
INFO  [ImportUtils] Realm 'unifesspa' imported
INFO  [services] KC-SERVICES0032: Import finished successfully
```

## Como regenerar o `realm-export.json`

Após ajustar clients, roles, scopes ou usuários pela console admin, exportar o realm:

```bash
docker compose -f docker/docker-compose.yml exec keycloak \
  /opt/keycloak/bin/kc.sh export \
  --realm unifesspa \
  --file /tmp/realm-export.json \
  --users realm_file

docker compose -f docker/docker-compose.yml cp \
  keycloak:/tmp/realm-export.json docker/keycloak/realm-export.json
```

Revisar o diff antes de commitar — o Keycloak mascara `client_secret` de clients confidential como `**********` no export. Se isso aparecer, converter o client para bearer-only (se for resource server) ou injetar o secret via variável de ambiente.
