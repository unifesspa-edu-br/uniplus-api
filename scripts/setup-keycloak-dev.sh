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

echo
log "Setup concluído. Teste o fluxo:"
cat <<EOF

  TOKEN=\$(curl -s -X POST '$KC_URL/realms/$KC_REALM/protocol/openid-connect/token' \\
    -H 'Content-Type: application/x-www-form-urlencoded' \\
    -d 'grant_type=password' -d 'client_id=admin-cli' \\
    -d 'username=candidato' -d 'password=$TEST_PASSWORD' | jq -r .access_token)

  curl -s -H "Authorization: Bearer \$TOKEN" http://localhost:5202/api/profile/me | jq

EOF
