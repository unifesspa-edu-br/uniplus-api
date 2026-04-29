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
# Uso local:
#   export GOVBR_CLIENT_ID=keycloak-hom.unifesspa.edu.br
#   export GOVBR_CLIENT_SECRET=...   # do .env, NUNCA commitado
#   export GOVBR_ENV=staging
#   scripts/setup-govbr-idp.sh
#
# Uso em homologação institucional:
#   export KC_URL=https://keycloak-hom.unifesspa.edu.br
#   export KC_ADMIN_USER=...
#   export KC_ADMIN_PASS=...
#   export GOVBR_CLIENT_ID=keycloak-hom.unifesspa.edu.br
#   export GOVBR_CLIENT_SECRET=...
#   scripts/setup-govbr-idp.sh
#
# Para REMOVER o IdP (rollback):
#   ADMIN_TOKEN=$(curl -sf -X POST "$KC_URL/realms/master/protocol/openid-connect/token" \
#       -H "Content-Type: application/x-www-form-urlencoded" \
#       -d "grant_type=password&client_id=admin-cli&username=admin&password=admin" \
#       | jq -r .access_token)
#   curl -X DELETE -H "Authorization: Bearer $ADMIN_TOKEN" \
#       "$KC_URL/admin/realms/$KC_REALM/identity-provider/instances/$GOVBR_ALIAS"
#
# LIMITAÇÃO CONHECIDA — flow de first-broker-login:
#   A ADR-029 prevê auto-link por CPF (atributo) no primeiro login. Isso exige
#   uma execution customizada no flow "First Broker Login" (clone do flow padrão
#   substituindo o executor "Detect Existing Broker User" para casar por atributo
#   `cpf` em vez de e-mail). Esse fluxo customizado NÃO é configurado por este
#   script — usa-se o flow padrão do Keycloak (matching por e-mail).
#   Acompanhar follow-up para implementar o flow customizado antes do GO-LIVE.
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
GOVBR_USERINFO_URL="https://${GOVBR_HOST}/userinfo/"
GOVBR_JWKS_URL="https://${GOVBR_HOST}/jwk"
GOVBR_LOGOUT_URL="https://${GOVBR_HOST}/logout"
GOVBR_ISSUER="https://${GOVBR_HOST}"

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
                syncMode: "FORCE",
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
    "syncMode": "INHERIT",
    "claim": "sub",
    "user.attribute": "cpf"
}'

# Nome — given_name e family_name vêm via scope `profile`
upsert_mapper "given-name" "oidc-user-attribute-idp-mapper" '{
    "syncMode": "INHERIT",
    "claim": "given_name",
    "user.attribute": "firstName"
}'

upsert_mapper "family-name" "oidc-user-attribute-idp-mapper" '{
    "syncMode": "INHERIT",
    "claim": "family_name",
    "user.attribute": "lastName"
}'

# Email
upsert_mapper "email" "oidc-user-attribute-idp-mapper" '{
    "syncMode": "INHERIT",
    "claim": "email",
    "user.attribute": "email"
}'

# Nível de confiabilidade — vem em `reliability_info.level` no id_token
# (requer scope govbr_confiabilidades_idtoken; valores: bronze, silver, gold)
upsert_mapper "nivel-confiabilidade" "oidc-user-attribute-idp-mapper" '{
    "syncMode": "INHERIT",
    "claim": "reliability_info.level",
    "user.attribute": "nivelConfiabilidade"
}'

# Role hardcoded — todo login via gov.br ganha a role `candidato`
upsert_mapper "role-candidato" "oidc-hardcoded-role-idp-mapper" '{
    "syncMode": "INHERIT",
    "role": "candidato"
}'

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

  Mappers configurados:
    - cpf                  (sub → atributo cpf)
    - given-name           (given_name → firstName)
    - family-name          (family_name → lastName)
    - email                (email → email)
    - nivel-confiabilidade (reliability_info.level → atributo nivelConfiabilidade)
    - role-candidato       (hardcoded role 'candidato')

  ⚠️  gov.br NÃO aceita 'localhost' como redirect URI registrado.
     Para testar o fluxo end-to-end localmente, use cloudflared/ngrok:
       cloudflared tunnel --url $KC_URL
     Registre a URL pública resultante no gov.br homologação como redirect.
     Em alternativa, configure direto contra o Keycloak HML institucional
     (KC_URL=https://keycloak-hom.unifesspa.edu.br).

EOF
