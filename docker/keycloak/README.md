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

## User Federation LDAP (sintético, dev local)

Para testar Identity Brokering com matching de CPF contra users LDAP-federados, o ambiente local sobe um **OpenLDAP sintético** (`docker/ldap/`) e o `setup-keycloak-dev.sh` registra o User Federation no realm de forma idempotente.

```bash
# Sobe OpenLDAP local + Keycloak
docker compose -f docker/docker-compose.yml up -d openldap keycloak

# Registra User Federation no realm + sync inicial
scripts/setup-keycloak-dev.sh
```

Resultado: 10 users sintéticos importados no realm, 7 com CPF de 11 dígitos + 3 com CPF de 10 dígitos (simulando bug do LDAP institucional).

Detalhes em [`../ldap/README.md`](../ldap/README.md) — origem dos dados, como regenerar, atributo `employeeNumber` no lugar de `brPersonCPF`, credenciais.

> Não confundir com o **LDAP institucional Unifesspa** — este OpenLDAP local existe **apenas** em `docker-compose` para dev local. Não é replicação do institucional, não tem dados reais, e não deve ser referenciado em config de HML/PROD.

## Identity Provider gov.br (ADR-029)

O gov.br é registrado no realm `unifesspa` como Identity Provider externo OIDC, permitindo que candidatos façam login via Login Único com CPF, nome, e-mail e nível de confiabilidade (bronze/prata/ouro) sincronizados automaticamente. A decisão arquitetural está documentada na [ADR-029](https://github.com/unifesspa-edu-br/uniplus-docs/blob/main/docs/adrs/ADR-029-identity-brokering-govbr-ldap-google.md).

A configuração é aplicada via Admin API por um script idempotente — **não** entra no `realm-export.json` (segredos versionados são proibidos):

```bash
scripts/setup-govbr-idp.sh
```

### Variáveis de ambiente

| Variável | Obrigatória | Default | Observação |
|---|---|---|---|
| `GOVBR_CLIENT_ID` | sim | — | Hostname do RP (ex.: `keycloak-hom.unifesspa.edu.br` em HML) |
| `GOVBR_CLIENT_SECRET` | sim | — | Fornecido pelo gov.br — **nunca commitar** |
| `GOVBR_ENV` | não | `staging` | `staging` ou `production` |
| `GOVBR_ALIAS` | não | `govbr` | Compõe o redirect URI |
| `KC_URL` | não | `http://localhost:8080` | Apontar para HML quando configurar institucional |
| `KC_REALM` | não | `unifesspa` | |
| `KC_ADMIN_USER` / `KC_ADMIN_PASS` | não | `admin` / `admin` | |

Configurar via `.env` (use `docker/.env.example` como referência) — o `docker compose` carrega automaticamente. **`.env` é gitignored** — confirmar antes de qualquer commit.

### O que o script faz

1. Aguarda Keycloak responder no realm.
2. Obtém token de admin via ROPC contra `master`.
3. Garante que a role realm `candidato` existe (cria se faltar — útil em HML que tem realm pré-existente).
4. Cria/atualiza Identity Provider OIDC com:
   - `clientAuthMethod: client_secret_basic` (única opção aceita pelo gov.br)
   - `pkceEnabled: true`, `pkceMethod: S256`
   - `syncMode: IMPORT`, `trustEmail: true`, `storeToken: false`
   - `defaultScope: openid email profile govbr_confiabilidades govbr_confiabilidades_idtoken`
   - `issuer` com barra final, `userInfoUrl` sem barra final — alinhado à discovery oficial do gov.br staging em `/.well-known/openid-configuration`
   - Endpoints conforme `GOVBR_ENV` (staging usa `sso.staging.acesso.gov.br`, production usa `sso.acesso.gov.br`)
5. Cria/atualiza 5 mappers:
   - `cpf` — claim `sub` → atributo `cpf`
   - `given-name` — claim `given_name` → `firstName`
   - `family-name` — claim `family_name` → `lastName`
   - `email` — claim `email` → `email`
   - `nivel-confiabilidade` — claim `reliability_info.level` → atributo `nivelConfiabilidade`

> **Nota sobre role `candidato`:** mapper hardcoded de role **não** é configurado por este script. Aplicar role realm via mapper a users existentes do LDAP (read-only) lança exceção e aborta o flow. Atribuição da role precisa ocorrer via fluxo customizado de first-broker-login (clone do flow padrão) ou na camada de aplicação. Decisão pendente — ver ADR-029.

> **Nota sobre `syncMode: IMPORT`:** quando o realm tem User Federation LDAP em modo READ_ONLY (caso do realm HML institucional), `FORCE` causa exceção ao tentar sobrescrever atributos LDAP-managed (cpf, email, firstName). `IMPORT` só popula atributos quando o user é **criado** pelo broker (candidatos novos), preservando os dados do LDAP em users já federados. Trade-off: candidato com dados desatualizados no Keycloak não recebe refresh automático em logins subsequentes.

Idempotente: rodar 2x produz o mesmo estado, sem erros nem duplicação.

### Como configurar contra HML institucional (caminho recomendado)

A validação end-to-end do fluxo gov.br é feita contra o Keycloak HML institucional (`keycloak-hom.unifesspa.edu.br`) — o redirect URI já está registrado no gov.br homologação (item da solicitação enviada em 23/04/2026 e validado em 29/04/2026).

```bash
export KC_URL=https://keycloak-hom.unifesspa.edu.br
export KC_ADMIN_USER=...                   # admin do realm unifesspa
export KC_ADMIN_PASS=...
export GOVBR_CLIENT_ID=keycloak-hom.unifesspa.edu.br
export GOVBR_CLIENT_SECRET=...             # do canal formal gov.br
export GOVBR_ENV=staging
scripts/setup-govbr-idp.sh
```

> ⚠️ O Keycloak HML institucional é **realm compartilhado** com outros sistemas (ex.: `ficha_facil`, `sisplad`) — **NÃO** importar `realm-export.json` lá. O script só toca em IdP gov.br, seus mappers e a role `candidato` (idempotente, defensivo).

Para validar o login, abra `https://keycloak-hom.unifesspa.edu.br/realms/unifesspa/account/` → Sign In → "gov.br". Para criar conta de teste no `https://sso.staging.acesso.gov.br/`, usar:

- **Nome da mãe:** `MAMÃE`
- **Data de nascimento:** `01/01/1980`

### Configuração local (estrutural)

O `setup-govbr-idp.sh` pode ser rodado contra o Keycloak local para validar a configuração estrutural do IdP/mappers (útil ao iterar no script ou no SPI customizado). O **fluxo end-to-end gov.br não funciona contra `localhost`** — gov.br não aceita esse host como redirect URI registrado.

```bash
docker compose -f docker/docker-compose.yml up -d
scripts/setup-keycloak-dev.sh   # roles, usuários de teste, clients, LDAP federation

GOVBR_CLIENT_ID=... GOVBR_CLIENT_SECRET=... scripts/setup-govbr-idp.sh
```

Inspeção da configuração via Admin API:

```bash
TOKEN=$(curl -sf -X POST "http://localhost:8080/realms/master/protocol/openid-connect/token" \
    -H "Content-Type: application/x-www-form-urlencoded" \
    -d "grant_type=password&client_id=admin-cli&username=admin&password=admin" \
    | jq -r .access_token)

curl -sf -H "Authorization: Bearer $TOKEN" \
    "http://localhost:8080/admin/realms/unifesspa/identity-provider/instances/govbr" \
    | jq '{alias, providerId, enabled, syncMode: .config.syncMode, defaultScope: .config.defaultScope}'
```

### Como remover (rollback)

```bash
ADMIN_TOKEN=$(curl -sf -X POST "$KC_URL/realms/master/protocol/openid-connect/token" \
    -H "Content-Type: application/x-www-form-urlencoded" \
    -d "grant_type=password&client_id=admin-cli&username=$KC_ADMIN_USER&password=$KC_ADMIN_PASS" \
    | jq -r .access_token)

curl -X DELETE -H "Authorization: Bearer $ADMIN_TOKEN" \
    "$KC_URL/admin/realms/$KC_REALM/identity-provider/instances/govbr"
```

Os mappers do IdP são removidos em cascata pelo Keycloak. A role `candidato` permanece (não é removida pelo rollback).

### Limitação conhecida — flow de first-broker-login

A ADR-029 prevê **auto-link por CPF** no primeiro login (Opção A). Isso requer uma execution customizada no flow `First Broker Login` (clone do flow padrão substituindo o executor `Detect Existing Broker User` para casar por atributo `cpf` em vez de e-mail). Esse flow customizado **não é configurado** por este script — usa-se o flow padrão (matching por e-mail). Endereçar antes do GO-LIVE em produção.

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
