#!/usr/bin/env bash
# setup-govbr-mock.sh — Configura um realm Keycloak fake (`govbr-mock`) que
# atua como IdP OIDC simulando o gov.br staging localmente, e registra esse
# realm como Identity Provider `govbr-mock` no realm `unifesspa`.
#
# Por que existe
#
#   O fluxo gov.br não roda contra `localhost` (gov.br não aceita esse host
#   como redirect URI registrado). Para validar o cpf-matcher SPI ponta a ponta
#   em ambiente de desenvolvimento — em particular o cenário de matching com
#   fallback de 10 dígitos (CPF iniciado em zero, bug histórico do LDAP
#   institucional) — este script sobe um IdP local que devolve `sub=<cpf>`
#   no id_token, exatamente como o gov.br faz.
#
#   ⚠️ APENAS DEV LOCAL. Não usar em HML/PROD. O realm fake tem credenciais
#   triviais e mappers que sobrescrevem o claim `sub` — comportamento
#   intencionalmente inseguro para ergonomia de teste.
#
# Idempotência
#
#   Rodar duas vezes produz o mesmo estado, sem erros nem duplicação. Usa
#   GET → POST/PUT em todos os recursos.
#
# Pré-requisitos
#
#   - Keycloak local no ar (docker compose up -d keycloak)
#   - Realm `unifesspa` importado
#   - Flow `first broker login com cpf` configurado pelo setup-keycloak-dev.sh
#   - Provider uniplus-cpf-matcher carregado (ver scripts/build-keycloak-providers.sh)
#
# Uso
#
#   scripts/setup-govbr-mock.sh
#
#   # Login programático para gerar token de mock user (usado pelo smoke test
#   # E2E — ver docker/keycloak/README.md):
#   curl -X POST http://localhost:8080/realms/govbr-mock/protocol/openid-connect/token \
#       -d grant_type=password -d client_id=unifesspa-rp \
#       -d client_secret=mock-secret-not-real -d scope=openid \
#       -d username=07094871422 -d password=Mock!1234

set -euo pipefail

# ---- Config ----------------------------------------------------------------

KC_URL="${KC_URL:-http://localhost:8080}"
TARGET_REALM="${TARGET_REALM:-unifesspa}"
MOCK_REALM="${MOCK_REALM:-govbr-mock}"
MOCK_CLIENT_ID="${MOCK_CLIENT_ID:-unifesspa-rp}"
MOCK_CLIENT_SECRET="${MOCK_CLIENT_SECRET:-mock-secret-not-real}"
MOCK_USER_PASSWORD="${MOCK_USER_PASSWORD:-Mock!1234}"
KC_ADMIN_USER="${KC_ADMIN_USER:-admin}"
KC_ADMIN_PASS="${KC_ADMIN_PASS:-admin}"
IDP_ALIAS="${IDP_ALIAS:-govbr-mock}"
IDP_FIRST_BROKER_LOGIN_FLOW="${IDP_FIRST_BROKER_LOGIN_FLOW:-first broker login com cpf}"

# Users a criar no realm fake. Cada linha é "cpf|first_name|last_name|email".
# Os 3 CPFs com zero à esquerda batem por fallback com users LDAP cujos
# employeeNumber estão truncados em 10 dígitos (kevin.peixoto, emanuelly.fernandes,
# fernando.melo). Os 2 últimos exercem o caminho de matching canônico (11 dígitos).
readonly MOCK_USERS=(
    "07094871422|Kevin Test|Mock|kevin.test@govbr.mock"
    "03776203781|Emanuelly Test|Mock|emanuelly.test@govbr.mock"
    "03425754904|Fernando Test|Mock|fernando.test@govbr.mock"
    "76323164930|Lara Test|Mock|lara.test@govbr.mock"
    "12345678901|Novo Test|Mock|novo.test@govbr.mock"
    # CPF dedicado à demo de auto-heal completo (user manual no realm unifesspa,
    # não conflita com nenhum user LDAP — ver ensure_target_realm_test_user).
    "09876543210|Autoheal Test|Mock|autoheal.test@govbr.mock"
)

# User manual no realm `unifesspa` (NÃO federado) usado para demonstrar o
# auto-heal funcionando ponta a ponta. Cpf=10 dígitos no realm; ao logar via
# mock com sub=11 dígitos correspondente, o cpf-matcher acha via fallback,
# faz auto-heal escrevendo 11 dígitos canônicos. Funciona porque o user é
# manual (sem LDAP federation), portanto setSingleAttribute persiste.
readonly TARGET_TEST_USERNAME="autoheal-test"
readonly TARGET_TEST_CPF_TRUNCATED="9876543210"
readonly TARGET_TEST_EMAIL="autoheal-test@uniplus.local"
readonly TARGET_TEST_PASSWORD="Test!1234"

# ---- Logging ---------------------------------------------------------------

log()  { printf '\033[1;36m==> %s\033[0m\n' "$*" >&2; }
ok()   { printf '\033[1;32m    OK %s\033[0m\n' "$*" >&2; }
warn() { printf '\033[1;33m!! %s\033[0m\n' "$*" >&2; }
fail() { printf '\033[1;31m✗ %s\033[0m\n' "$*" >&2; exit 1; }

require() {
    command -v "$1" >/dev/null 2>&1 || fail "Comando ausente: $1"
}

# ---- Auth helpers ----------------------------------------------------------

ADMIN_TOKEN=""

obtain_admin_token() {
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
}

auth()      { curl -sf -H "Authorization: Bearer $ADMIN_TOKEN" "$@"; }
auth_json() { auth -H "Content-Type: application/json" "$@"; }

# ---- 1. Realm fake `govbr-mock` -------------------------------------------

ensure_mock_realm() {
    local http_code
    http_code=$(curl -s -o /dev/null -w '%{http_code}' \
        -H "Authorization: Bearer $ADMIN_TOKEN" \
        "$KC_URL/admin/realms/$MOCK_REALM")

    if [ "$http_code" = "200" ]; then
        ok "realm '$MOCK_REALM' já existe"
        return
    fi

    auth_json -X POST "$KC_URL/admin/realms" -d "$(jq -nc \
        --arg r "$MOCK_REALM" \
        '{realm: $r, enabled: true, displayName: "gov.br (mock — DEV ONLY)"}')" >/dev/null
    ok "realm '$MOCK_REALM' criado"
}

# Realm 'govbr-mock' precisa permitir attribute 'cpf' no user — por padrão
# Keycloak 26+ rejeita atributos não declarados no User Profile.
ensure_mock_realm_unmanaged_attributes() {
    local profile patched
    profile=$(auth "$KC_URL/admin/realms/$MOCK_REALM/users/profile")
    if [ "$(echo "$profile" | jq -r '.unmanagedAttributePolicy // "null"')" = "ENABLED" ]; then
        ok "unmanagedAttributePolicy do '$MOCK_REALM' já está ENABLED"
        return
    fi
    patched=$(echo "$profile" | jq '. + {unmanagedAttributePolicy: "ENABLED"}')
    auth_json -X PUT "$KC_URL/admin/realms/$MOCK_REALM/users/profile" -d "$patched" >/dev/null
    ok "unmanagedAttributePolicy do '$MOCK_REALM' setado como ENABLED"
}

# ---- 2. Cliente OIDC `unifesspa-rp` no realm fake -------------------------

# O clientId é fixo; o secret é trivial intencionalmente (DEV ONLY). O
# redirect URI casa com o broker callback do realm `unifesspa`.
ensure_mock_client() {
    local existing_id
    existing_id=$(auth "$KC_URL/admin/realms/$MOCK_REALM/clients?clientId=$MOCK_CLIENT_ID" \
        | jq -r '.[0].id // empty')

    local body
    body=$(jq -nc \
        --arg cid "$MOCK_CLIENT_ID" \
        --arg secret "$MOCK_CLIENT_SECRET" \
        --arg redirect "$KC_URL/realms/$TARGET_REALM/broker/$IDP_ALIAS/endpoint" \
        '{
            clientId: $cid,
            secret: $secret,
            enabled: true,
            protocol: "openid-connect",
            publicClient: false,
            standardFlowEnabled: true,
            directAccessGrantsEnabled: true,
            redirectUris: [$redirect],
            attributes: {
                "client.use.lightweight.access.token.enabled": "false"
            }
        }')

    if [ -n "$existing_id" ]; then
        local body_with_id
        body_with_id=$(echo "$body" | jq --arg id "$existing_id" '. + {id: $id}')
        auth_json -X PUT "$KC_URL/admin/realms/$MOCK_REALM/clients/$existing_id" -d "$body_with_id" >/dev/null
        ok "client '$MOCK_CLIENT_ID' atualizado"
    else
        auth_json -X POST "$KC_URL/admin/realms/$MOCK_REALM/clients" -d "$body" >/dev/null
        ok "client '$MOCK_CLIENT_ID' criado"
    fi
}

# ---- 3. Mapper que sobrescreve o claim `sub` com o atributo `cpf` ---------

# O cpf-matcher consome `brokerContext.getId()`, que vem do claim `sub` do
# id_token recebido pelo realm `unifesspa`. Para que o sub seja o CPF (e não
# o UUID interno do user), adicionamos um oidc-usermodel-attribute-mapper que
# remapeia o sub. Funciona — validado empiricamente no Keycloak 26.5.
ensure_sub_override_mapper() {
    local client_id mapper_name existing_id body

    client_id=$(auth "$KC_URL/admin/realms/$MOCK_REALM/clients?clientId=$MOCK_CLIENT_ID" \
        | jq -r '.[0].id')
    mapper_name="sub-from-cpf-attribute"

    existing_id=$(auth "$KC_URL/admin/realms/$MOCK_REALM/clients/$client_id/protocol-mappers/models" \
        | jq -r --arg n "$mapper_name" '.[] | select(.name==$n) | .id // empty' \
        | head -n1)

    body=$(jq -nc --arg name "$mapper_name" \
        '{
            name: $name,
            protocol: "openid-connect",
            protocolMapper: "oidc-usermodel-attribute-mapper",
            config: {
                "user.attribute": "cpf",
                "claim.name": "sub",
                "jsonType.label": "String",
                "id.token.claim": "true",
                "access.token.claim": "true",
                "userinfo.token.claim": "true"
            }
        }')

    if [ -n "$existing_id" ]; then
        local body_with_id
        body_with_id=$(echo "$body" | jq --arg id "$existing_id" '. + {id: $id}')
        auth_json -X PUT "$KC_URL/admin/realms/$MOCK_REALM/clients/$client_id/protocol-mappers/models/$existing_id" \
            -d "$body_with_id" >/dev/null
        ok "mapper '$mapper_name' atualizado"
    else
        auth_json -X POST "$KC_URL/admin/realms/$MOCK_REALM/clients/$client_id/protocol-mappers/models" \
            -d "$body" >/dev/null
        ok "mapper '$mapper_name' criado"
    fi
}

# ---- 4. Mock users no realm fake -----------------------------------------

# Cria/atualiza um user. Cada user tem username=cpf e atributo cpf=cpf, com
# senha não-temporária para destravar testes via direct-grants se necessário.
upsert_mock_user() {
    local cpf="$1" first="$2" last="$3" email="$4"

    local existing_id body
    existing_id=$(auth "$KC_URL/admin/realms/$MOCK_REALM/users?username=$cpf&exact=true" \
        | jq -r '.[0].id // empty')

    body=$(jq -nc \
        --arg u "$cpf" \
        --arg c "$cpf" \
        --arg f "$first" \
        --arg l "$last" \
        --arg e "$email" \
        '{
            username: $u,
            enabled: true,
            emailVerified: true,
            email: $e,
            firstName: $f,
            lastName: $l,
            attributes: { cpf: [$c] }
        }')

    if [ -n "$existing_id" ]; then
        auth_json -X PUT "$KC_URL/admin/realms/$MOCK_REALM/users/$existing_id" -d "$body" >/dev/null
    else
        auth_json -X POST "$KC_URL/admin/realms/$MOCK_REALM/users" -d "$body" >/dev/null
        existing_id=$(auth "$KC_URL/admin/realms/$MOCK_REALM/users?username=$cpf&exact=true" \
            | jq -r '.[0].id')
    fi

    # Reset de senha (idempotente — sempre sobrescreve)
    auth_json -X PUT "$KC_URL/admin/realms/$MOCK_REALM/users/$existing_id/reset-password" \
        -d "$(jq -nc --arg p "$MOCK_USER_PASSWORD" '{type:"password", value:$p, temporary:false}')" >/dev/null

    # Limpa requiredActions (algumas validations do User Profile geram
    # VERIFY_PROFILE em dev — não queremos esse atrito no mock)
    auth_json -X PUT "$KC_URL/admin/realms/$MOCK_REALM/users/$existing_id" \
        -d '{"requiredActions":[]}' >/dev/null

    ok "mock user '$cpf' ($first $last) configurado"
}

ensure_mock_users() {
    local row
    for row in "${MOCK_USERS[@]}"; do
        IFS='|' read -r cpf first last email <<< "$row"
        upsert_mock_user "$cpf" "$first" "$last" "$email"
    done
}

# ---- 5. IdP `govbr-mock` no realm `unifesspa` -----------------------------

ensure_idp_in_target_realm() {
    local http body

    http=$(curl -s -o /dev/null -w '%{http_code}' \
        -H "Authorization: Bearer $ADMIN_TOKEN" \
        "$KC_URL/admin/realms/$TARGET_REALM/identity-provider/instances/$IDP_ALIAS")

    body=$(jq -nc \
        --arg alias "$IDP_ALIAS" \
        --arg flow "$IDP_FIRST_BROKER_LOGIN_FLOW" \
        --arg authUrl "$KC_URL/realms/$MOCK_REALM/protocol/openid-connect/auth" \
        --arg tokenUrl "$KC_URL/realms/$MOCK_REALM/protocol/openid-connect/token" \
        --arg userInfoUrl "$KC_URL/realms/$MOCK_REALM/protocol/openid-connect/userinfo" \
        --arg jwksUrl "$KC_URL/realms/$MOCK_REALM/protocol/openid-connect/certs" \
        --arg logoutUrl "$KC_URL/realms/$MOCK_REALM/protocol/openid-connect/logout" \
        --arg issuer "$KC_URL/realms/$MOCK_REALM" \
        --arg clientId "$MOCK_CLIENT_ID" \
        --arg clientSecret "$MOCK_CLIENT_SECRET" \
        '{
            alias: $alias,
            displayName: "Entrar com gov.br (MOCK)",
            providerId: "oidc",
            enabled: true,
            trustEmail: true,
            storeToken: false,
            addReadTokenRoleOnCreate: false,
            linkOnly: false,
            firstBrokerLoginFlowAlias: $flow,
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
                defaultScope: "openid profile email",
                useJwksUrl: "true",
                validateSignature: "true",
                syncMode: "IMPORT",
                pkceEnabled: "true",
                pkceMethod: "S256",
                backchannelSupported: "false"
            }
        }')

    case "$http" in
        200)
            auth_json -X PUT "$KC_URL/admin/realms/$TARGET_REALM/identity-provider/instances/$IDP_ALIAS" -d "$body" >/dev/null
            ok "IdP '$IDP_ALIAS' atualizado em '$TARGET_REALM'"
            ;;
        404)
            auth_json -X POST "$KC_URL/admin/realms/$TARGET_REALM/identity-provider/instances" -d "$body" >/dev/null
            ok "IdP '$IDP_ALIAS' criado em '$TARGET_REALM'"
            ;;
        *)
            warn "Resposta inesperada ao consultar IdP: HTTP $http"
            exit 1
            ;;
    esac
}

# Espelha os mappers do IdP gov.br real, para que o broker grave os mesmos
# atributos (cpf, given_name, family_name, email) no user criado no realm
# `unifesspa`. Imprescindível para o cpf-matcher: ele usa o atributo `cpf`
# do user existente para fazer matching, mas só para users LDAP-federados;
# para users criados via mock (12345678901), o auto-create vai usar o sub.
upsert_idp_mapper() {
    local name="$1" type="$2" config_json="$3"

    local existing_id body
    existing_id=$(auth "$KC_URL/admin/realms/$TARGET_REALM/identity-provider/instances/$IDP_ALIAS/mappers" \
        | jq -r --arg n "$name" '.[] | select(.name==$n) | .id // empty' \
        | head -n1)

    body=$(jq -nc \
        --arg name "$name" \
        --arg type "$type" \
        --arg alias "$IDP_ALIAS" \
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
        auth_json -X PUT "$KC_URL/admin/realms/$TARGET_REALM/identity-provider/instances/$IDP_ALIAS/mappers/$existing_id" \
            -d "$body_with_id" >/dev/null
        ok "IdP mapper '$name' atualizado"
    else
        auth_json -X POST "$KC_URL/admin/realms/$TARGET_REALM/identity-provider/instances/$IDP_ALIAS/mappers" \
            -d "$body" >/dev/null
        ok "IdP mapper '$name' criado"
    fi
}

# Cria/atualiza user manual no realm `unifesspa` para demo de auto-heal.
# Como NÃO é LDAP-federado, o auto-heal do cpf-matcher persiste o atributo —
# diferente dos users LDAP READ_ONLY, onde o auto-heal falha por design.
ensure_target_realm_test_user() {
    local existing_id body
    existing_id=$(auth "$KC_URL/admin/realms/$TARGET_REALM/users?username=$TARGET_TEST_USERNAME&exact=true" \
        | jq -r '.[0].id // empty')

    body=$(jq -nc \
        --arg u "$TARGET_TEST_USERNAME" \
        --arg c "$TARGET_TEST_CPF_TRUNCATED" \
        --arg e "$TARGET_TEST_EMAIL" \
        '{
            username: $u,
            enabled: true,
            emailVerified: true,
            email: $e,
            firstName: "Autoheal",
            lastName: "Test",
            attributes: { cpf: [$c] }
        }')

    if [ -n "$existing_id" ]; then
        auth_json -X PUT "$KC_URL/admin/realms/$TARGET_REALM/users/$existing_id" -d "$body" >/dev/null
        ok "user '$TARGET_TEST_USERNAME' (manual, não-LDAP) atualizado em '$TARGET_REALM'"
    else
        auth_json -X POST "$KC_URL/admin/realms/$TARGET_REALM/users" -d "$body" >/dev/null
        existing_id=$(auth "$KC_URL/admin/realms/$TARGET_REALM/users?username=$TARGET_TEST_USERNAME&exact=true" \
            | jq -r '.[0].id')
        ok "user '$TARGET_TEST_USERNAME' (manual, não-LDAP) criado em '$TARGET_REALM' com cpf=$TARGET_TEST_CPF_TRUNCATED"
    fi

    # Senha + limpa requiredActions (libera ROPC pós-flow para inspecionar userinfo)
    auth_json -X PUT "$KC_URL/admin/realms/$TARGET_REALM/users/$existing_id/reset-password" \
        -d "$(jq -nc --arg p "$TARGET_TEST_PASSWORD" '{type:"password", value:$p, temporary:false}')" >/dev/null
    auth_json -X PUT "$KC_URL/admin/realms/$TARGET_REALM/users/$existing_id" \
        -d '{"requiredActions":[]}' >/dev/null

    # Garante que NÃO existe federated identity remanescente (idempotência —
    # caso o flow tenha sido executado antes, removemos para que o matcher
    # rode de novo no próximo login)
    auth -X DELETE "$KC_URL/admin/realms/$TARGET_REALM/users/$existing_id/federated-identity/$IDP_ALIAS" 2>/dev/null || true
}

ensure_idp_mappers() {
    upsert_idp_mapper "cpf" "oidc-user-attribute-idp-mapper" '{
        "syncMode": "IMPORT",
        "claim": "sub",
        "user.attribute": "cpf"
    }'
    upsert_idp_mapper "given-name" "oidc-user-attribute-idp-mapper" '{
        "syncMode": "IMPORT",
        "claim": "given_name",
        "user.attribute": "firstName"
    }'
    upsert_idp_mapper "family-name" "oidc-user-attribute-idp-mapper" '{
        "syncMode": "IMPORT",
        "claim": "family_name",
        "user.attribute": "lastName"
    }'
    upsert_idp_mapper "email" "oidc-user-attribute-idp-mapper" '{
        "syncMode": "IMPORT",
        "claim": "email",
        "user.attribute": "email"
    }'
}

# ---- Resumo ---------------------------------------------------------------

print_summary() {
    log "Mock IdP gov.br configurado"
    cat >&2 <<EOF

  Realm fake:    $MOCK_REALM
  Client:        $MOCK_CLIENT_ID  (secret: $MOCK_CLIENT_SECRET)
  Senha mocks:   $MOCK_USER_PASSWORD

  Mock users (username = CPF de 11 dígitos):
EOF
    local row
    for row in "${MOCK_USERS[@]}"; do
        IFS='|' read -r cpf first last _email <<< "$row"
        printf '    - %s  (%s %s)\n' "$cpf" "$first" "$last" >&2
    done

    cat >&2 <<EOF

  IdP no realm '$TARGET_REALM': $IDP_ALIAS  (firstBrokerLoginFlow=$IDP_FIRST_BROKER_LOGIN_FLOW)

  Console admin do realm fake:
    $KC_URL/admin/master/console/#/$MOCK_REALM/

  Como validar via browser:
    1. Abrir incognito em $KC_URL/realms/$TARGET_REALM/account/
    2. Sign In → "Entrar com gov.br (MOCK)"
    3. No realm fake, logar com username=07094871422 / senha=$MOCK_USER_PASSWORD
    4. Após login, voltar pro Account Console e verificar atributo cpf

  Como validar via curl:
    scripts/smoke-test-cpf-matcher.sh

EOF
}

# ---- Entry point ----------------------------------------------------------

main() {
    require jq
    require curl

    # SAFETY: este script é DEV ONLY. Cria um realm fake com mappers que
    # sobrescrevem o claim `sub` — comportamento intencionalmente inseguro.
    # Travamos para localhost / 127.0.0.1; rodar contra HML/PRD quebra a
    # premissa do realm institucional.
    if [[ "$KC_URL" != http://localhost:* ]] && [[ "$KC_URL" != http://127.0.0.1:* ]]; then
        warn "setup-govbr-mock.sh é DEV ONLY — KC_URL='$KC_URL' não é localhost."
        warn "Em HML/PRD use o gov.br staging/produção real via setup-govbr-idp.sh."
        warn "Defina ALLOW_NON_LOCALHOST_MOCK=true para forçar (ciente do risco)."
        [ "${ALLOW_NON_LOCALHOST_MOCK:-false}" = "true" ] || exit 1
    fi

    obtain_admin_token
    log "Configurando realm fake '$MOCK_REALM'"
    ensure_mock_realm
    ensure_mock_realm_unmanaged_attributes
    ensure_mock_client
    ensure_sub_override_mapper
    ensure_mock_users
    log "Configurando IdP '$IDP_ALIAS' no realm '$TARGET_REALM'"
    ensure_idp_in_target_realm
    ensure_idp_mappers
    log "Provisionando user manual de teste (demo de auto-heal sem LDAP)"
    ensure_target_realm_test_user
    print_summary
}

main "$@"
