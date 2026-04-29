#!/usr/bin/env bash
# Setup pós-import do realm `unifesspa` para habilitar testes via Direct Access
# Grants (ROPC) sem subir o frontend Angular.
#
# O realm-export.json versionado reflete configuração próxima de produção:
# senhas temporárias, admin-cli com lightweight tokens e sem o scope
# uniplus-profile. Em produção isso é correto — mas trava smoke tests rápidos
# de fluxo OIDC via curl. Este script aplica os patches via Admin API; nada
# é persistido no realm-export.
#
# Os ajustes feitos aqui não sobrevivem a `docker compose down -v` — re-rode
# o script após recriar o volume do Postgres.
#
# Pré-requisitos:
#   - Stack docker no ar: docker compose -f docker/docker-compose.yml up -d
#   - jq, curl
#
# Uso:
#   scripts/setup-keycloak-dev.sh
#
# Variáveis de ambiente opcionais:
#   KC_URL          (default: http://localhost:8080)
#   KC_REALM        (default: unifesspa)
#   KC_ADMIN_USER   (default: admin)
#   KC_ADMIN_PASS   (default: admin)
#   TEST_PASSWORD   (default: Changeme!123)

set -euo pipefail

KC_URL="${KC_URL:-http://localhost:8080}"
KC_REALM="${KC_REALM:-unifesspa}"
KC_ADMIN_USER="${KC_ADMIN_USER:-admin}"
KC_ADMIN_PASS="${KC_ADMIN_PASS:-admin}"
TEST_PASSWORD="${TEST_PASSWORD:-Changeme!123}"
TEST_USERS=("admin" "gestor" "avaliador" "candidato")

log()  { printf '\033[1;36m==> %s\033[0m\n' "$*"; }
ok()   { printf '\033[1;32m    OK %s\033[0m\n' "$*"; }
warn() { printf '\033[1;33m!! %s\033[0m\n' "$*" >&2; }

require() {
    command -v "$1" >/dev/null 2>&1 || { warn "Comando ausente: $1"; exit 1; }
}
require jq
require curl

log "Aguardando Keycloak responder em $KC_URL/realms/$KC_REALM"
RETRIES=30
while ! curl -sf "$KC_URL/realms/$KC_REALM/.well-known/openid-configuration" >/dev/null 2>&1; do
    RETRIES=$((RETRIES - 1))
    if [ "$RETRIES" -le 0 ]; then
        warn "Timeout aguardando Keycloak. A stack está no ar?"
        exit 1
    fi
    printf '.'
    sleep 2
done
echo
ok "Keycloak responde"

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

log "Configurando admin-cli para emitir tokens ROPC com claims completos"
ADMIN_CLI_ID=$(auth "$API/clients?clientId=admin-cli" | jq -r '.[0].id // empty')
[ -n "$ADMIN_CLI_ID" ] || { warn "Client admin-cli não encontrado no realm $KC_REALM"; exit 1; }

# Adicionar uniplus-profile aos default scopes (idempotente — Keycloak retorna 204 se já existe)
UP_SCOPE_ID=$(auth "$API/client-scopes" | jq -r '.[] | select(.name=="uniplus-profile") | .id')
if [ -n "$UP_SCOPE_ID" ] && [ "$UP_SCOPE_ID" != "null" ]; then
    auth_json -X PUT "$API/clients/$ADMIN_CLI_ID/default-client-scopes/$UP_SCOPE_ID" >/dev/null
    ok "scope 'uniplus-profile' presente nos default scopes do admin-cli (cpf, nomeSocial, aud=uniplus)"
else
    warn "Scope uniplus-profile não existe no realm — verifique a importação"
fi

# Desligar lightweight access tokens (admin-cli em Keycloak 26+ vem com true e isso retira sub/email/etc do token)
CURRENT=$(auth "$API/clients/$ADMIN_CLI_ID")
PATCHED=$(echo "$CURRENT" | jq '.attributes["client.use.lightweight.access.token.enabled"] = "false"')
auth_json -X PUT "$API/clients/$ADMIN_CLI_ID" -d "$PATCHED" >/dev/null
ok "lightweight access token desligado em admin-cli"

log "Resetando senha dos usuários de teste como não-temporária (libera ROPC)"
for user in "${TEST_USERS[@]}"; do
    USER_ID=$(auth "$API/users?username=$user&exact=true" | jq -r '.[0].id // empty')
    if [ -z "$USER_ID" ]; then
        warn "Usuário '$user' não encontrado — pulando"
        continue
    fi
    auth_json -X PUT "$API/users/$USER_ID/reset-password" \
        -d "{\"type\":\"password\",\"value\":\"$TEST_PASSWORD\",\"temporary\":false}" >/dev/null
    auth_json -X PUT "$API/users/$USER_ID" -d '{"requiredActions":[]}' >/dev/null
    ok "$user — senha=$TEST_PASSWORD (não-temporária), required_actions limpos"
done

log "Configurando User Federation LDAP (openldap sintético) — se openldap estiver no ar"

# Detecta se há LDAP local acessível para configurar User Federation.
# A federation aqui criada (ldap-local-sintetico) é dev-only — em HML institucional
# o LDAP é configurado de outra forma e este passo é skipado.
#
# Estratégia: probe TCP no host:porta via /dev/tcp do bash (não exige nc).
# - Em dev local: KC_URL=http://localhost:* → testa contra o openldap mapeado em 127.0.0.1:1389
# - Em HML/PROD: KC_URL=https://*.unifesspa.edu.br → skip explícito (LDAP institucional não é nosso openldap sintético)
LDAP_HOST="${LDAP_HOST:-openldap}"
LDAP_PORT="${LDAP_PORT:-389}"
LDAP_PROBE_HOST="${LDAP_PROBE_HOST:-127.0.0.1}"
LDAP_PROBE_PORT="${LDAP_PROBE_PORT:-1389}"
LDAP_REACHABLE=false

if [[ "$KC_URL" == http://localhost:* ]] || [[ "$KC_URL" == http://127.0.0.1:* ]]; then
    if (timeout 2 bash -c "</dev/tcp/$LDAP_PROBE_HOST/$LDAP_PROBE_PORT") 2>/dev/null; then
        LDAP_REACHABLE=true
    fi
fi

if [ "$LDAP_REACHABLE" = "true" ]; then
    LDAP_PROVIDER_NAME="ldap-local-sintetico"
    LDAP_BASE_DN="${LDAP_BASE_DN:-dc=unifesspa,dc=edu,dc=br}"
    LDAP_BIND_DN="${LDAP_BIND_DN:-cn=admin,$LDAP_BASE_DN}"
    LDAP_BIND_CREDENTIAL="${LDAP_BIND_CREDENTIAL:-admin}"
    LDAP_USERS_DN="${LDAP_USERS_DN:-ou=Users,$LDAP_BASE_DN}"

    EXISTING_LDAP_ID=$(auth "$API/components?type=org.keycloak.storage.UserStorageProvider&name=$LDAP_PROVIDER_NAME" \
        | jq -r '.[0].id // empty')

    LDAP_BODY=$(jq -nc \
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
        }')

    if [ -n "$EXISTING_LDAP_ID" ]; then
        # PUT exige body com id e parentId — busca atual e mescla
        CURRENT=$(auth "$API/components/$EXISTING_LDAP_ID")
        MERGED=$(echo "$CURRENT" | jq --argjson new "$LDAP_BODY" '.config = $new.config')
        auth_json -X PUT "$API/components/$EXISTING_LDAP_ID" -d "$MERGED" >/dev/null
        ok "User Federation '$LDAP_PROVIDER_NAME' atualizado"
        LDAP_ID="$EXISTING_LDAP_ID"
    else
        REALM_INFO=$(auth "$API/")
        REALM_INTERNAL_ID=$(echo "$REALM_INFO" | jq -r '.id')
        LDAP_BODY_WITH_PARENT=$(echo "$LDAP_BODY" | jq --arg pid "$REALM_INTERNAL_ID" '. + {parentId: $pid}')
        LOCATION=$(curl -sf -D - -o /dev/null -H "Authorization: Bearer $ADMIN_TOKEN" -H "Content-Type: application/json" \
            -X POST "$API/components" -d "$LDAP_BODY_WITH_PARENT" | grep -i '^location:' | tr -d '\r' | awk '{print $2}')
        LDAP_ID="${LOCATION##*/}"
        ok "User Federation '$LDAP_PROVIDER_NAME' criado (id=$LDAP_ID)"
    fi

    # Mappers do LDAP — todos read.only=true (reproduz cenário institucional)
    upsert_ldap_mapper() {
        local name="$1" provider_id="$2" config_json="$3"

        local existing_id
        existing_id=$(auth "$API/components?parent=$LDAP_ID&name=$name" \
            | jq -r '.[0].id // empty')

        local body
        body=$(jq -nc \
            --arg name "$name" \
            --arg pid "$LDAP_ID" \
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

    # CPF — vem do employeeNumber do LDAP (no LDAP institucional seria brPersonCPF;
    # documentado na issue uniplus-api#217 a equivalência conceitual)
    upsert_ldap_mapper "cpf" "user-attribute-ldap-mapper" '{
        "ldap.attribute": ["employeeNumber"],
        "user.model.attribute": ["cpf"],
        "read.only": ["true"],
        "always.read.value.from.ldap": ["true"],
        "is.mandatory.in.ldap": ["false"],
        "is.binary.attribute": ["false"]
    }'

    upsert_ldap_mapper "username" "user-attribute-ldap-mapper" '{
        "ldap.attribute": ["uid"],
        "user.model.attribute": ["username"],
        "read.only": ["true"],
        "always.read.value.from.ldap": ["true"],
        "is.mandatory.in.ldap": ["true"],
        "is.binary.attribute": ["false"]
    }'

    upsert_ldap_mapper "email" "user-attribute-ldap-mapper" '{
        "ldap.attribute": ["mail"],
        "user.model.attribute": ["email"],
        "read.only": ["true"],
        "always.read.value.from.ldap": ["true"],
        "is.mandatory.in.ldap": ["false"],
        "is.binary.attribute": ["false"]
    }'

    upsert_ldap_mapper "first-name" "user-attribute-ldap-mapper" '{
        "ldap.attribute": ["givenName"],
        "user.model.attribute": ["firstName"],
        "read.only": ["true"],
        "always.read.value.from.ldap": ["true"],
        "is.mandatory.in.ldap": ["false"],
        "is.binary.attribute": ["false"]
    }'

    upsert_ldap_mapper "last-name" "user-attribute-ldap-mapper" '{
        "ldap.attribute": ["sn"],
        "user.model.attribute": ["lastName"],
        "read.only": ["true"],
        "always.read.value.from.ldap": ["true"],
        "is.mandatory.in.ldap": ["false"],
        "is.binary.attribute": ["false"]
    }'

    # Sync inicial (puxa os 10 users do LDAP para o realm)
    SYNC_RESULT=$(curl -sf -X POST -H "Authorization: Bearer $ADMIN_TOKEN" \
        "$API/user-storage/$LDAP_ID/sync?action=triggerFullSync")
    ok "sync inicial: $(echo "$SYNC_RESULT" | jq -c '.')"
else
    warn "openldap não está rodando — User Federation LDAP não foi configurado."
    warn "Suba com: docker compose -f docker/docker-compose.yml up -d openldap"
fi

echo
log "Setup concluído. Teste o fluxo:"
cat <<EOF

  TOKEN=\$(curl -s -X POST '$KC_URL/realms/$KC_REALM/protocol/openid-connect/token' \\
    -H 'Content-Type: application/x-www-form-urlencoded' \\
    -d 'grant_type=password' -d 'client_id=admin-cli' \\
    -d 'username=candidato' -d 'password=$TEST_PASSWORD' | jq -r .access_token)

  curl -s -H "Authorization: Bearer \$TOKEN" http://localhost:5202/api/profile/me | jq

  # Listar users sintéticos do LDAP local:
  ldapsearch -x -H ldap://localhost:1389 \\
    -b ou=Users,dc=unifesspa,dc=edu,dc=br \\
    -D 'cn=admin,dc=unifesspa,dc=edu,dc=br' -w admin \\
    '(objectClass=inetOrgPerson)' uid cn employeeNumber

EOF
