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
