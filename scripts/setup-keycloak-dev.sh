#!/usr/bin/env bash
# Setup completo do ambiente DEV do Keycloak. Para HML/PRD use os scripts
# standalone (setup-cpf-matcher-flow.sh + setup-govbr-idp.sh) — este script
# é DEV-only e contém ajustes que NÃO devem rodar em HML/PRD (LDAP sintético,
# admin-cli configurado para ROPC, mock IdP gov.br).
#
# Etapas (cada uma idempotente, encapsulada em função):
#   1. wait_for_keycloak                    — probe do realm endpoint
#   2. obtain_admin_token                   — ROPC contra master realm
#   3. configure_admin_cli_for_ropc         — uniplus-profile + lightweight off
#                                             (libera smoke tests via curl)
#   4. reset_test_user_passwords            — 4 users de teste, senha não-temporária
#   5. configure_ldap_federation_if_available — User Federation contra openldap
#                                             sintético
#   6. setup-cpf-matcher-flow.sh            — clona flow built-in,
#                                             insere uniplus-cpf-matcher,
#                                             aponta IdPs gov.br + govbr-mock
#                                             [delegado para script standalone]
#   7. setup-govbr-mock.sh                  — IdP mock para validar gov.br E2E
#                                             em ambiente local (sub=cpf)
#                                             [SKIP_GOVBR_MOCK=true desabilita]
#   8. print_smoke_test_hint                — exemplo de uso pós-setup
#
# Em DEV o "Login gov.br" efetivo é o realm fake `govbr-mock` no mesmo
# Keycloak. Em HML/PRD o IdP `govbr` aponta para o gov.br staging/produção
# real, configurado via setup-govbr-idp.sh.
#
# Os ajustes deste script não sobrevivem a `docker compose down -v` — re-rode
# após recriar o volume do Postgres.
#
# Pré-requisitos:
#   - Stack docker no ar: docker compose -f docker/docker-compose.yml up -d
#   - JAR cpf-matcher buildado: scripts/build-keycloak-providers.sh cpf-matcher
#   - jq, curl
#
# Uso:
#   scripts/setup-keycloak-dev.sh                     # DEV completo (default)
#   SKIP_GOVBR_MOCK=true scripts/setup-keycloak-dev.sh  # sem mock IdP
#
# Variáveis de ambiente opcionais:
#   KC_URL                  (default: http://localhost:8080)
#   KC_REALM                (default: unifesspa)
#   KC_ADMIN_USER           (default: admin)
#   KC_ADMIN_PASS           (default: admin)
#   TEST_PASSWORD           (default: Changeme!123)
#
#   LDAP_HOST               (default: openldap)        — usado pelo Keycloak (rede docker)
#   LDAP_PORT               (default: 389)             — porta interna do container
#   LDAP_PROBE_HOST         (default: 127.0.0.1)       — usado pelo probe TCP do host
#   LDAP_PROBE_PORT         (default: 1389)            — porta mapeada no host
#   LDAP_BASE_DN            (default: dc=unifesspa,dc=edu,dc=br)
#   LDAP_BIND_DN            (default: cn=admin,$LDAP_BASE_DN)
#   LDAP_BIND_CREDENTIAL    (default: admin)
#   LDAP_USERS_DN           (default: ou=Users,$LDAP_BASE_DN)

set -euo pipefail

# ---- Constantes e variáveis globais ----------------------------------------

KC_URL="${KC_URL:-http://localhost:8080}"
KC_REALM="${KC_REALM:-unifesspa}"
KC_ADMIN_USER="${KC_ADMIN_USER:-admin}"
KC_ADMIN_PASS="${KC_ADMIN_PASS:-admin}"
TEST_PASSWORD="${TEST_PASSWORD:-Changeme!123}"

readonly TEST_USERS=("admin" "gestor" "avaliador" "candidato")

LDAP_HOST="${LDAP_HOST:-openldap}"
LDAP_PORT="${LDAP_PORT:-389}"
LDAP_PROBE_HOST="${LDAP_PROBE_HOST:-127.0.0.1}"
LDAP_PROBE_PORT="${LDAP_PROBE_PORT:-1389}"
LDAP_BASE_DN="${LDAP_BASE_DN:-dc=unifesspa,dc=edu,dc=br}"
LDAP_BIND_DN="${LDAP_BIND_DN:-cn=admin,$LDAP_BASE_DN}"
LDAP_BIND_CREDENTIAL="${LDAP_BIND_CREDENTIAL:-admin}"
LDAP_USERS_DN="${LDAP_USERS_DN:-ou=Users,$LDAP_BASE_DN}"

readonly LDAP_PROVIDER_NAME="ldap-local-sintetico"

# Diretório onde vivem os scripts auxiliares chamados como subprocesso
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# Definidos durante a execução
ADMIN_TOKEN=""
API=""

# ---- Logging ---------------------------------------------------------------

log()  { printf '\033[1;36m==> %s\033[0m\n' "$*" >&2; }
ok()   { printf '\033[1;32m    OK %s\033[0m\n' "$*" >&2; }
warn() { printf '\033[1;33m!! %s\033[0m\n' "$*" >&2; }
fail() { printf '\033[1;31m✗ %s\033[0m\n' "$*" >&2; exit 1; }

require() {
    command -v "$1" >/dev/null 2>&1 || fail "Comando ausente: $1"
}

# Helpers de chamada autenticada — assumem ADMIN_TOKEN e API setados.
auth()      { curl -sf -H "Authorization: Bearer $ADMIN_TOKEN" "$@"; }
auth_json() { auth -H "Content-Type: application/json" "$@"; }

# ---- Etapa 1 — Aguarda Keycloak responder ----------------------------------

wait_for_keycloak() {
    log "Aguardando Keycloak responder em $KC_URL/realms/$KC_REALM"
    local retries=30
    while ! curl -sf "$KC_URL/realms/$KC_REALM/.well-known/openid-configuration" >/dev/null 2>&1; do
        retries=$((retries - 1))
        if [ "$retries" -le 0 ]; then
            warn "Timeout aguardando Keycloak. A stack está no ar?"
            exit 1
        fi
        printf '.'
        sleep 2
    done
    echo
    ok "Keycloak responde"
}

# ---- Etapa 2 — Token de admin no master realm ------------------------------

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

    API="$KC_URL/admin/realms/$KC_REALM"
    ok "Admin token obtido"
}

# ---- Etapa 3 — admin-cli configurado para ROPC com claims completos --------

configure_admin_cli_for_ropc() {
    log "Configurando admin-cli para emitir tokens ROPC com claims completos"

    local admin_cli_id
    admin_cli_id=$(auth "$API/clients?clientId=admin-cli" | jq -r '.[0].id // empty')
    [ -n "$admin_cli_id" ] || { warn "Client admin-cli não encontrado no realm $KC_REALM"; exit 1; }

    # Adiciona uniplus-profile aos default scopes (Keycloak retorna 204 se já existe)
    local up_scope_id
    up_scope_id=$(auth "$API/client-scopes" | jq -r '.[] | select(.name=="uniplus-profile") | .id')
    if [ -n "$up_scope_id" ] && [ "$up_scope_id" != "null" ]; then
        auth_json -X PUT "$API/clients/$admin_cli_id/default-client-scopes/$up_scope_id" >/dev/null
        ok "scope 'uniplus-profile' presente nos default scopes do admin-cli (cpf, nomeSocial, aud=uniplus)"
    else
        warn "Scope uniplus-profile não existe no realm — verifique a importação"
    fi

    # Desliga lightweight access tokens (admin-cli em Keycloak 26+ vem com true,
    # o que retira sub/email/atributos do access token e quebra o pipeline JWT)
    local current patched
    current=$(auth "$API/clients/$admin_cli_id")
    patched=$(echo "$current" | jq '.attributes["client.use.lightweight.access.token.enabled"] = "false"')
    auth_json -X PUT "$API/clients/$admin_cli_id" -d "$patched" >/dev/null
    ok "lightweight access token desligado em admin-cli"
}

# ---- Etapa 4 — Senhas dos usuários de teste --------------------------------

reset_test_user_passwords() {
    log "Resetando senha dos usuários de teste como não-temporária (libera ROPC)"
    local user user_id
    for user in "${TEST_USERS[@]}"; do
        user_id=$(auth "$API/users?username=$user&exact=true" | jq -r '.[0].id // empty')
        if [ -z "$user_id" ]; then
            warn "Usuário '$user' não encontrado — pulando"
            continue
        fi
        auth_json -X PUT "$API/users/$user_id/reset-password" \
            -d "{\"type\":\"password\",\"value\":\"$TEST_PASSWORD\",\"temporary\":false}" >/dev/null
        auth_json -X PUT "$API/users/$user_id" -d '{"requiredActions":[]}' >/dev/null
        ok "$user — senha=$TEST_PASSWORD (não-temporária), required_actions limpos"
    done
}

# ---- Etapa 5 — User Federation LDAP (dev local somente) --------------------
#
# A federation 'ldap-local-sintetico' é dev-only — em HML institucional o
# LDAP é configurado de outra forma e este passo é skipado.

is_local_ldap_reachable() {
    # Só faz sentido em dev local; em HML, o LDAP institucional não é o
    # nosso openldap sintético.
    [[ "$KC_URL" == http://localhost:* ]] || [[ "$KC_URL" == http://127.0.0.1:* ]] || return 1

    # Probe TCP via /dev/tcp do bash (não exige nc).
    (timeout 2 bash -c "</dev/tcp/$LDAP_PROBE_HOST/$LDAP_PROBE_PORT") 2>/dev/null
}

build_ldap_provider_body() {
    jq -nc \
        --arg name "$LDAP_PROVIDER_NAME" \
        --arg connectionUrl "ldap://$LDAP_HOST:$LDAP_PORT" \
        --arg bindDn "$LDAP_BIND_DN" \
        --arg bindCredential "$LDAP_BIND_CREDENTIAL" \
        --arg usersDn "$LDAP_USERS_DN" \
        '{
            name: $name,
            providerId: "ldap",
            providerType: "org.keycloak.storage.UserStorageProvider",
            config: {
                enabled: ["true"],
                priority: ["1"],
                vendor: ["other"],
                editMode: ["READ_ONLY"],
                syncRegistrations: ["false"],
                importEnabled: ["true"],
                authType: ["simple"],
                connectionUrl: [$connectionUrl],
                bindDn: [$bindDn],
                bindCredential: [$bindCredential],
                usersDn: [$usersDn],
                userObjectClasses: ["inetOrgPerson, organizationalPerson, person"],
                rdnLDAPAttribute: ["uid"],
                uuidLDAPAttribute: ["entryUUID"],
                usernameLDAPAttribute: ["uid"],
                searchScope: ["1"],
                useTruststoreSpi: ["ldapsOnly"],
                connectionPooling: ["true"],
                pagination: ["true"],
                changedSyncPeriod: ["-1"],
                fullSyncPeriod: ["-1"],
                cachePolicy: ["DEFAULT"],
                batchSizeForSync: ["1000"]
            }
        }'
}

upsert_ldap_provider() {
    local body existing_id current merged location ldap_id realm_internal_id body_with_parent
    body=$(build_ldap_provider_body)

    existing_id=$(auth "$API/components?type=org.keycloak.storage.UserStorageProvider&name=$LDAP_PROVIDER_NAME" \
        | jq -r '.[0].id // empty')

    if [ -n "$existing_id" ]; then
        current=$(auth "$API/components/$existing_id")
        merged=$(echo "$current" | jq --argjson new "$body" '.config = $new.config')
        auth_json -X PUT "$API/components/$existing_id" -d "$merged" >/dev/null
        ok "User Federation '$LDAP_PROVIDER_NAME' atualizado"
        printf '%s' "$existing_id"
        return
    fi

    realm_internal_id=$(auth "$API/" | jq -r '.id')
    body_with_parent=$(echo "$body" | jq --arg pid "$realm_internal_id" '. + {parentId: $pid}')
    location=$(curl -sf -D - -o /dev/null -H "Authorization: Bearer $ADMIN_TOKEN" \
        -H "Content-Type: application/json" \
        -X POST "$API/components" -d "$body_with_parent" \
        | grep -i '^location:' | tr -d '\r' | awk '{print $2}')
    ldap_id="${location##*/}"
    ok "User Federation '$LDAP_PROVIDER_NAME' criado (id=$ldap_id)"
    printf '%s' "$ldap_id"
}

# upsert_ldap_mapper <ldap_provider_id> <name> <provider_id> <config_json>
upsert_ldap_mapper() {
    local ldap_id="$1" name="$2" provider_id="$3" config_json="$4"

    local existing_id body
    existing_id=$(auth "$API/components?parent=$ldap_id&name=$name" \
        | jq -r '.[0].id // empty')

    body=$(jq -nc \
        --arg name "$name" \
        --arg pid "$ldap_id" \
        --arg providerId "$provider_id" \
        --argjson config "$config_json" \
        '{
            name: $name,
            providerId: $providerId,
            providerType: "org.keycloak.storage.ldap.mappers.LDAPStorageMapper",
            parentId: $pid,
            config: $config
        }')

    if [ -n "$existing_id" ]; then
        local body_with_id
        body_with_id=$(echo "$body" | jq --arg id "$existing_id" '. + {id: $id}')
        auth_json -X PUT "$API/components/$existing_id" -d "$body_with_id" >/dev/null
        ok "ldap mapper '$name' atualizado"
    else
        auth_json -X POST "$API/components" -d "$body" >/dev/null
        ok "ldap mapper '$name' criado"
    fi
}

# Configura todos os 5 mappers padrão. Todos com read.only=true para reproduzir
# o cenário do LDAP institucional (editMode READ_ONLY).
#
# CPF vem do `employeeNumber` do LDAP — equivalente conceitual a `brPersonCPF`
# do LDAP institucional (ver docker/ldap/README.md e issue uniplus-api#217).
configure_ldap_mappers() {
    local ldap_id="$1"

    upsert_ldap_mapper "$ldap_id" "cpf" "user-attribute-ldap-mapper" '{
        "ldap.attribute": ["employeeNumber"],
        "user.model.attribute": ["cpf"],
        "read.only": ["true"],
        "always.read.value.from.ldap": ["true"],
        "is.mandatory.in.ldap": ["false"],
        "is.binary.attribute": ["false"]
    }'

    upsert_ldap_mapper "$ldap_id" "username" "user-attribute-ldap-mapper" '{
        "ldap.attribute": ["uid"],
        "user.model.attribute": ["username"],
        "read.only": ["true"],
        "always.read.value.from.ldap": ["true"],
        "is.mandatory.in.ldap": ["true"],
        "is.binary.attribute": ["false"]
    }'

    upsert_ldap_mapper "$ldap_id" "email" "user-attribute-ldap-mapper" '{
        "ldap.attribute": ["mail"],
        "user.model.attribute": ["email"],
        "read.only": ["true"],
        "always.read.value.from.ldap": ["true"],
        "is.mandatory.in.ldap": ["false"],
        "is.binary.attribute": ["false"]
    }'

    upsert_ldap_mapper "$ldap_id" "first-name" "user-attribute-ldap-mapper" '{
        "ldap.attribute": ["givenName"],
        "user.model.attribute": ["firstName"],
        "read.only": ["true"],
        "always.read.value.from.ldap": ["true"],
        "is.mandatory.in.ldap": ["false"],
        "is.binary.attribute": ["false"]
    }'

    upsert_ldap_mapper "$ldap_id" "last-name" "user-attribute-ldap-mapper" '{
        "ldap.attribute": ["sn"],
        "user.model.attribute": ["lastName"],
        "read.only": ["true"],
        "always.read.value.from.ldap": ["true"],
        "is.mandatory.in.ldap": ["false"],
        "is.binary.attribute": ["false"]
    }'
}

trigger_initial_sync() {
    local ldap_id="$1"
    local result
    result=$(curl -sf -X POST -H "Authorization: Bearer $ADMIN_TOKEN" \
        "$API/user-storage/$ldap_id/sync?action=triggerFullSync")
    ok "sync inicial: $(echo "$result" | jq -c '.')"
}

configure_ldap_federation_if_available() {
    log "Configurando User Federation LDAP (openldap sintético) — se openldap estiver no ar"

    if ! is_local_ldap_reachable; then
        warn "openldap local não acessível — User Federation LDAP não foi configurado."
        warn "Em dev: docker compose -f docker/docker-compose.yml up -d openldap"
        warn "Em HML/PROD: este passo é intencionalmente skipado."
        return
    fi

    local ldap_id
    ldap_id=$(upsert_ldap_provider)
    configure_ldap_mappers "$ldap_id"
    trigger_initial_sync "$ldap_id"
}

# ---- Etapa 6 — Flow first-broker-login com matching por CPF ----------------
#
# Delegado para scripts/setup-cpf-matcher-flow.sh — script standalone
# reutilizável em DEV/HML/PRD. Repassamos as credenciais e o realm via env.

configure_first_broker_login_with_cpf() {
    KC_URL="$KC_URL" KC_REALM="$KC_REALM" \
    KC_ADMIN_USER="$KC_ADMIN_USER" KC_ADMIN_PASS="$KC_ADMIN_PASS" \
        "$SCRIPT_DIR/setup-cpf-matcher-flow.sh"
}

# ---- Etapa 7 — IdP mock gov.br (DEV only) ----------------------------------
#
# Em DEV o "Login gov.br" efetivo é o realm fake `govbr-mock`, criado pelo
# script setup-govbr-mock.sh — passa as mesmas credenciais admin via env. O
# script é safe-guarded contra execução fora de localhost.
#
# SKIP_GOVBR_MOCK=true desativa esta etapa (útil quando o dev está integrando
# contra um IdP gov.br staging diretamente).

configure_govbr_mock_idp() {
    if [ "${SKIP_GOVBR_MOCK:-false}" = "true" ]; then
        log "Pulando setup do mock IdP (SKIP_GOVBR_MOCK=true)"
        return
    fi
    # TARGET_REALM precisa espelhar KC_REALM, senão o mock cria o IdP no
    # default 'unifesspa' enquanto as etapas anteriores configuraram outro
    # realm — desincronia silenciosa em runs com KC_REALM customizado.
    KC_URL="$KC_URL" TARGET_REALM="$KC_REALM" \
    KC_ADMIN_USER="$KC_ADMIN_USER" KC_ADMIN_PASS="$KC_ADMIN_PASS" \
        "$SCRIPT_DIR/setup-govbr-mock.sh"
}

# ---- Etapa 8 — Hint de smoke test ------------------------------------------

print_smoke_test_hint() {
    echo
    log "Setup DEV concluído. Caminhos de validação:"
    cat <<EOF >&2

  ── Login direto via ROPC (sem broker) ──
  TOKEN=\$(curl -s -X POST '$KC_URL/realms/$KC_REALM/protocol/openid-connect/token' \\
    -H 'Content-Type: application/x-www-form-urlencoded' \\
    -d 'grant_type=password' -d 'client_id=admin-cli' \\
    -d 'username=candidato' -d 'password=$TEST_PASSWORD' | jq -r .access_token)

  curl -s -H "Authorization: Bearer \$TOKEN" http://localhost:5202/api/profile/me | jq

  ── Login via gov.br MOCK (E2E do cpf-matcher) ──
  Browser:   http://localhost:8080/realms/$KC_REALM/account → "Entrar com gov.br (MOCK)"
             user: 09876543210  senha: Mock!1234
  Smoke:     scripts/smoke-test-cpf-matcher.sh

  ── Inspeções rápidas ──
  ldapsearch -x -H ldap://$LDAP_PROBE_HOST:$LDAP_PROBE_PORT \\
    -b $LDAP_USERS_DN \\
    -D '$LDAP_BIND_DN' -w '$LDAP_BIND_CREDENTIAL' \\
    '(objectClass=inetOrgPerson)' uid cn employeeNumber

EOF
}

# ---- Entry point -----------------------------------------------------------

main() {
    require jq
    require curl

    # SAFETY: este script é DEV ONLY. Aplica patches que NÃO devem rodar em
    # HML/PRD: reescreve `defaultClientScopes` e `lightweight access token` do
    # admin-cli (compartilhado com ficha_facil/sisplad em HML), sobrescreve
    # senhas dos 4 users de teste, configura LDAP federation contra OpenLDAP
    # sintético. Para HML/PRD use setup-cpf-matcher-flow.sh + setup-govbr-idp.sh.
    if [[ "$KC_URL" != http://localhost:* ]] && [[ "$KC_URL" != http://127.0.0.1:* ]]; then
        warn "setup-keycloak-dev.sh é DEV ONLY — KC_URL='$KC_URL' não é localhost."
        warn "Patches deste script (admin-cli, senhas de teste, LDAP) afetam realms"
        warn "compartilhados em HML/PRD. Para HML/PRD use:"
        warn "  scripts/setup-cpf-matcher-flow.sh"
        warn "  scripts/setup-govbr-idp.sh"
        warn "Defina ALLOW_NON_LOCALHOST_DEV=true para forçar (ciente do risco)."
        [ "${ALLOW_NON_LOCALHOST_DEV:-false}" = "true" ] || exit 1
    fi

    wait_for_keycloak
    obtain_admin_token
    configure_admin_cli_for_ropc
    reset_test_user_passwords
    configure_ldap_federation_if_available
    configure_first_broker_login_with_cpf
    configure_govbr_mock_idp
    print_smoke_test_hint
}

main "$@"
