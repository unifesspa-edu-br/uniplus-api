#!/usr/bin/env bash
# setup-govbr-idp.sh — Configura o gov.br como Identity Provider OIDC do realm
# `unifesspa` no Keycloak 26.5. Idempotente: rodar 2x produz o mesmo estado
# sem erros nem duplicação.
#
# Implementa a ADR-029 (Identity Brokering — gov.br + LDAP via Keycloak).
#
# Variáveis OBRIGATÓRIAS:
#   GOVBR_CLIENT_ID       Identificador do RP no gov.br (ex.: keycloak-hom.unifesspa.edu.br)
#   GOVBR_CLIENT_SECRET   Segredo fornecido pelo gov.br — NUNCA commitar
#
# Variáveis OPCIONAIS:
#   GOVBR_ENV             staging (default) | production
#   GOVBR_ALIAS           default: govbr  (compõe o redirect URI)
#   KC_URL                default: http://localhost:8080
#   KC_REALM              default: unifesspa
#   KC_ADMIN_USER         default: admin
#   KC_ADMIN_PASS         default: admin
#
# Uso em homologação institucional (caminho recomendado — validado em campo):
#   export KC_URL=https://keycloak-hom.unifesspa.edu.br
#   export KC_ADMIN_USER=...
#   export KC_ADMIN_PASS=...
#   export GOVBR_CLIENT_ID=keycloak-hom.unifesspa.edu.br
#   export GOVBR_CLIENT_SECRET=...   # do canal formal gov.br, NUNCA commitado
#   export GOVBR_ENV=staging
#   scripts/setup-govbr-idp.sh
#
# Uso local (configuração estrutural do realm — fluxo E2E gov.br não roda
# contra localhost porque o gov.br não aceita esse host como redirect):
#   export GOVBR_CLIENT_ID=...
#   export GOVBR_CLIENT_SECRET=...
#   scripts/setup-govbr-idp.sh   # KC_URL=http://localhost:8080 default
#
# Para REMOVER o IdP (rollback):
#   ADMIN_TOKEN=$(curl -sf -X POST "$KC_URL/realms/master/protocol/openid-connect/token" \
#       -H "Content-Type: application/x-www-form-urlencoded" \
#       -d "grant_type=password&client_id=admin-cli&username=admin&password=admin" \
#       | jq -r .access_token)
#   curl -X DELETE -H "Authorization: Bearer $ADMIN_TOKEN" \
#       "$KC_URL/admin/realms/$KC_REALM/identity-provider/instances/$GOVBR_ALIAS"
#
# LIMITAÇÕES CONHECIDAS (validadas em campo, HML 29/04/2026):
#
# 1. Flow de first-broker-login não customizado:
#    A ADR-029 prevê auto-link por CPF (atributo) no primeiro login. Isso exige
#    uma execution customizada no flow "First Broker Login" (clone do flow padrão
#    substituindo o executor "Detect Existing Broker User" para casar por atributo
#    `cpf` em vez de e-mail). Esse fluxo customizado NÃO é configurado por este
#    script — usa-se o flow padrão do Keycloak (matching por e-mail).
#    Servidores que já existem no realm via LDAP terão a fricção de
#    "User already exists. Add to existing account?" no primeiro login via gov.br.
#
# 2. syncMode = IMPORT (não FORCE):
#    Quando o realm tem User Federation LDAP em modo READ_ONLY (caso do realm
#    HML institucional), syncMode FORCE causa exceção ao tentar sobrescrever
#    atributos LDAP-managed (cpf, email, firstName, etc.). IMPORT só popula
#    atributos quando o user é CRIADO pelo broker (candidatos novos), e não
#    força atualização em users já federados pelo LDAP. Trade-off: candidato
#    que tiver dados desatualizados no Keycloak não recebe refresh automático
#    do gov.br em logins subsequentes.
#
# 3. Role `candidato` não atribuída automaticamente:
#    Aplicar role realm via mapper hardcoded a users LDAP read-only causa
#    exceção. Atribuição da role precisa ocorrer via outro mecanismo —
#    ver comentário inline próximo aos mappers.
#
# Pré-requisitos:
#   - Keycloak no ar com realm `unifesspa` importado (rodar setup-keycloak-dev.sh
#     antes em ambiente local)
#   - Comandos: jq, curl

set -euo pipefail

# ---- Variáveis obrigatórias
: "${GOVBR_CLIENT_ID:?GOVBR_CLIENT_ID é obrigatório (ex.: keycloak-hom.unifesspa.edu.br)}"
: "${GOVBR_CLIENT_SECRET:?GOVBR_CLIENT_SECRET é obrigatório (do gov.br — nunca commitar)}"

# ---- Defaults
GOVBR_ENV="${GOVBR_ENV:-staging}"
GOVBR_ALIAS="${GOVBR_ALIAS:-govbr}"
KC_URL="${KC_URL:-http://localhost:8080}"
KC_REALM="${KC_REALM:-unifesspa}"
KC_ADMIN_USER="${KC_ADMIN_USER:-admin}"
KC_ADMIN_PASS="${KC_ADMIN_PASS:-admin}"

# ---- Endpoints gov.br (conforme https://acesso.gov.br/roteiro-tecnico)
case "$GOVBR_ENV" in
    staging)    GOVBR_HOST="sso.staging.acesso.gov.br" ;;
    production) GOVBR_HOST="sso.acesso.gov.br" ;;
    *)          echo "GOVBR_ENV inválido: '$GOVBR_ENV' (use 'staging' ou 'production')" >&2; exit 2 ;;
esac

GOVBR_AUTHORIZATION_URL="https://${GOVBR_HOST}/authorize"
GOVBR_TOKEN_URL="https://${GOVBR_HOST}/token"
GOVBR_USERINFO_URL="https://${GOVBR_HOST}/userinfo"
GOVBR_JWKS_URL="https://${GOVBR_HOST}/jwk"
GOVBR_LOGOUT_URL="https://${GOVBR_HOST}/logout"
# Issuer com barra final é como o gov.br devolve no claim `iss` do id_token
# (validado em 29/04/2026 contra https://sso.staging.acesso.gov.br/.well-known/openid-configuration)
GOVBR_ISSUER="https://${GOVBR_HOST}/"

# ---- Logging
log()  { printf '\033[1;36m==> %s\033[0m\n' "$*"; }
ok()   { printf '\033[1;32m    OK %s\033[0m\n' "$*"; }
warn() { printf '\033[1;33m!! %s\033[0m\n' "$*" >&2; }

require() {
    command -v "$1" >/dev/null 2>&1 || { warn "Comando ausente: $1"; exit 1; }
}
require jq
require curl

# ---- Aguarda Keycloak
log "Aguardando Keycloak responder em $KC_URL/realms/$KC_REALM"
RETRIES=30
while ! curl -sf "$KC_URL/realms/$KC_REALM/.well-known/openid-configuration" >/dev/null 2>&1; do
    RETRIES=$((RETRIES - 1))
    if [ "$RETRIES" -le 0 ]; then
        warn "Timeout aguardando Keycloak. A stack está no ar e o realm '$KC_REALM' importado?"
        exit 1
    fi
    printf '.'
    sleep 2
done
echo
ok "Keycloak responde"

# ---- Token de admin
log "Obtendo token de admin no master realm"
ADMIN_TOKEN=$(curl -sf -X POST "$KC_URL/realms/master/protocol/openid-connect/token" \
    -H "Content-Type: application/x-www-form-urlencoded" \
    -d "grant_type=password" \
    -d "client_id=admin-cli" \
    -d "username=$KC_ADMIN_USER" \
    -d "password=$KC_ADMIN_PASS" \
    | jq -r '.access_token // empty')
[ -n "$ADMIN_TOKEN" ] || { warn "Falha ao obter admin token. Credenciais corretas?"; exit 1; }
ok "Admin token obtido"

API="$KC_URL/admin/realms/$KC_REALM"
auth() { curl -sf -H "Authorization: Bearer $ADMIN_TOKEN" "$@"; }
auth_json() { auth -H "Content-Type: application/json" "$@"; }

# ---- Body do IdP (jq para escapar strings com segurança)
build_idp_body() {
    jq -nc \
        --arg alias "$GOVBR_ALIAS" \
        --arg authUrl "$GOVBR_AUTHORIZATION_URL" \
        --arg tokenUrl "$GOVBR_TOKEN_URL" \
        --arg userInfoUrl "$GOVBR_USERINFO_URL" \
        --arg jwksUrl "$GOVBR_JWKS_URL" \
        --arg logoutUrl "$GOVBR_LOGOUT_URL" \
        --arg issuer "$GOVBR_ISSUER" \
        --arg clientId "$GOVBR_CLIENT_ID" \
        --arg clientSecret "$GOVBR_CLIENT_SECRET" \
        '{
            alias: $alias,
            displayName: "gov.br",
            providerId: "oidc",
            enabled: true,
            trustEmail: true,
            storeToken: false,
            addReadTokenRoleOnCreate: false,
            linkOnly: false,
            firstBrokerLoginFlowAlias: "first broker login",
            config: {
                authorizationUrl: $authUrl,
                tokenUrl: $tokenUrl,
                userInfoUrl: $userInfoUrl,
                jwksUrl: $jwksUrl,
                logoutUrl: $logoutUrl,
                issuer: $issuer,
                clientId: $clientId,
                clientSecret: $clientSecret,
                clientAuthMethod: "client_secret_basic",
                defaultScope: "openid email profile govbr_confiabilidades govbr_confiabilidades_idtoken",
                useJwksUrl: "true",
                validateSignature: "true",
                syncMode: "IMPORT",
                pkceEnabled: "true",
                pkceMethod: "S256",
                backchannelSupported: "false"
            }
        }'
}

# ---- Garante que a role realm `candidato` existe (mapper hardcoded depende dela)
ensure_realm_role() {
    local role_name="$1"
    local http_code
    http_code=$(curl -s -o /dev/null -w '%{http_code}' \
        -H "Authorization: Bearer $ADMIN_TOKEN" \
        "$API/roles/$role_name")

    case "$http_code" in
        200) ok "role realm '$role_name' já existe" ;;
        404)
            auth_json -X POST "$API/roles" \
                -d "$(jq -nc --arg n "$role_name" '{name: $n, description: ("Role aplicada automaticamente a usuários autenticados via gov.br")}')" >/dev/null
            ok "role realm '$role_name' criada"
            ;;
        *)  warn "Resposta inesperada ao consultar role '$role_name': HTTP $http_code"; exit 1 ;;
    esac
}

log "Garantindo pré-requisitos no realm"
ensure_realm_role "candidato"

# ---- Cria ou atualiza IdP (idempotente via GET → POST/PUT)
log "Configurando Identity Provider gov.br ($GOVBR_ENV)"
EXISTS_HTTP=$(curl -s -o /dev/null -w '%{http_code}' \
    -H "Authorization: Bearer $ADMIN_TOKEN" \
    "$API/identity-provider/instances/$GOVBR_ALIAS")

IDP_BODY=$(build_idp_body)

case "$EXISTS_HTTP" in
    200)
        auth_json -X PUT "$API/identity-provider/instances/$GOVBR_ALIAS" -d "$IDP_BODY" >/dev/null
        ok "IdP '$GOVBR_ALIAS' atualizado"
        ;;
    404)
        auth_json -X POST "$API/identity-provider/instances" -d "$IDP_BODY" >/dev/null
        ok "IdP '$GOVBR_ALIAS' criado"
        ;;
    *)
        warn "Resposta inesperada ao consultar IdP: HTTP $EXISTS_HTTP"
        exit 1
        ;;
esac

# ---- Mapper helper (idempotente: GET listagem por nome → POST/PUT)
upsert_mapper() {
    local name="$1" type="$2" config_json="$3"

    local existing_id
    existing_id=$(auth "$API/identity-provider/instances/$GOVBR_ALIAS/mappers" \
        | jq -r --arg n "$name" '.[] | select(.name==$n) | .id // empty' \
        | head -n1)

    local body
    body=$(jq -nc \
        --arg name "$name" \
        --arg type "$type" \
        --arg alias "$GOVBR_ALIAS" \
        --argjson config "$config_json" \
        '{
            name: $name,
            identityProviderAlias: $alias,
            identityProviderMapper: $type,
            config: $config
        }')

    if [ -n "$existing_id" ]; then
        local body_with_id
        body_with_id=$(echo "$body" | jq --arg id "$existing_id" '. + {id: $id}')
        auth_json -X PUT "$API/identity-provider/instances/$GOVBR_ALIAS/mappers/$existing_id" \
            -d "$body_with_id" >/dev/null
        ok "mapper '$name' atualizado"
    else
        auth_json -X POST "$API/identity-provider/instances/$GOVBR_ALIAS/mappers" \
            -d "$body" >/dev/null
        ok "mapper '$name' criado"
    fi
}

# ---- Mappers (5 conforme ADR-029)
log "Configurando mappers do IdP gov.br"

# CPF — gov.br retorna o CPF no claim 'sub' (id_token e access_token)
upsert_mapper "cpf" "oidc-user-attribute-idp-mapper" '{
    "syncMode": "IMPORT",
    "claim": "sub",
    "user.attribute": "cpf"
}'

# Nome — given_name e family_name vêm via scope `profile`
upsert_mapper "given-name" "oidc-user-attribute-idp-mapper" '{
    "syncMode": "IMPORT",
    "claim": "given_name",
    "user.attribute": "firstName"
}'

upsert_mapper "family-name" "oidc-user-attribute-idp-mapper" '{
    "syncMode": "IMPORT",
    "claim": "family_name",
    "user.attribute": "lastName"
}'

# Email
upsert_mapper "email" "oidc-user-attribute-idp-mapper" '{
    "syncMode": "IMPORT",
    "claim": "email",
    "user.attribute": "email"
}'

# Nível de confiabilidade — vem em `reliability_info.level` no id_token
# (requer scope govbr_confiabilidades_idtoken; valores: bronze, silver, gold)
upsert_mapper "nivel-confiabilidade" "oidc-user-attribute-idp-mapper" '{
    "syncMode": "IMPORT",
    "claim": "reliability_info.level",
    "user.attribute": "nivelConfiabilidade"
}'

# NOTA: Mapper hardcoded de role `candidato` foi REMOVIDO desta versão.
#
# Validação em HML (29/04/2026) revelou que aplicar role realm a users já
# federados via LDAP (read-only) lança exceção e aborta o flow de login.
# A role `candidato` permanece criada (ver ensure_realm_role acima) para uso
# por outros mecanismos de autorização. Atribuição da role precisa ocorrer
# em uma das opções abaixo (em deliberação na ADR-029):
#
#   1. Authentication Flow customizado de first-broker-login que aplica role
#      apenas em users novos criados pelo broker (não em users LDAP existentes)
#   2. Atribuição na camada de aplicação (Uni+ API) baseada na origem da
#      identidade federada (federatedIdentityProvider == "govbr")
#   3. Mapper condicional via SPI customizado

# ---- Resumo
echo
log "Configuração concluída"
cat <<EOF

  Realm:      $KC_REALM
  IdP alias:  $GOVBR_ALIAS  ($GOVBR_ENV)
  Client ID:  $GOVBR_CLIENT_ID
  Issuer:     $GOVBR_ISSUER
  Redirect:   $KC_URL/realms/$KC_REALM/broker/$GOVBR_ALIAS/endpoint

  Console admin: $KC_URL/admin/master/console/#/$KC_REALM/identity-providers/$GOVBR_ALIAS/settings

  Mappers configurados (5):
    - cpf                  (sub → atributo cpf)
    - given-name           (given_name → firstName)
    - family-name          (family_name → lastName)
    - email                (email → email)
    - nivel-confiabilidade (reliability_info.level → atributo nivelConfiabilidade)

  Role 'candidato' criada no realm mas NÃO atribuída automaticamente — a
  atribuição depende do Authenticator SPI customizado (uniplus-keycloak-providers).

EOF
