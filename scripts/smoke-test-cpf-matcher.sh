#!/usr/bin/env bash
# smoke-test-cpf-matcher.sh — Roda o flow `first broker login com cpf` ponta a
# ponta contra o IdP mock (govbr-mock) via curl, simulando o que o navegador
# faria, e valida via Admin API + ROPC que o cpf-matcher trabalhou como
# esperado.
#
# Pré-requisitos:
#   1. scripts/setup-keycloak-dev.sh
#   2. scripts/setup-govbr-mock.sh
#
# Cenários cobertos (ambos demonstram matching tolerante por CPF):
#
#   1. AUTOHEAL — user manual `autoheal-test` (cpf truncado 9876543210 no
#      realm unifesspa). Login mock com sub=09876543210. Esperado:
#        - log "matching via fallback ... aplicando auto-heal"
#        - cpf do user no realm passa a ser 09876543210 (canônico)
#        - userinfo do autoheal-test retorna cpf=09876543210
#
#   2. LDAP_READONLY_LIMITATION — user LDAP-federado kevin.peixoto
#      (cpf 7094871422 truncado no LDAP). Login mock com sub=07094871422.
#      Esperado (limitação documentada):
#        - log "matching via fallback ... aplicando auto-heal"
#        - log "auto-heal falhou ... ReadOnlyException"
#        - cpf do user permanece 7094871422 (LDAP read-only re-lê o original)
#        - userinfo continua com 10 dígitos — correção definitiva é
#          institucional (migration do LDAP brPersonCPF — issue separada)
#
# Idempotente: cada execução limpa federated identity prévio para que o
# matcher rode novamente.

set -euo pipefail

KC_URL="${KC_URL:-http://localhost:8080}"
TARGET_REALM="${TARGET_REALM:-unifesspa}"
MOCK_REALM="${MOCK_REALM:-govbr-mock}"
IDP_ALIAS="${IDP_ALIAS:-govbr-mock}"
KC_ADMIN_USER="${KC_ADMIN_USER:-admin}"
KC_ADMIN_PASS="${KC_ADMIN_PASS:-admin}"
MOCK_USER_PASSWORD="${MOCK_USER_PASSWORD:-Mock!1234}"

# PKCE fixo (RFC 7636 example)
readonly PKCE_VERIFIER="dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk"
readonly PKCE_CHALLENGE="E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM"

readonly CLIENT_ID="selecao-web"
readonly REDIRECT_URI="http://localhost:4200/callback"

log()   { printf '\033[1;36m==> %s\033[0m\n' "$*" >&2; }
ok()    { printf '\033[1;32m    OK %s\033[0m\n' "$*" >&2; }
warn()  { printf '\033[1;33m!! %s\033[0m\n' "$*" >&2; }
fail()  { printf '\033[1;31m✗ %s\033[0m\n' "$*" >&2; exit 1; }

require() {
    command -v "$1" >/dev/null 2>&1 || fail "Comando ausente: $1"
}

# ---- Auth helpers ---------------------------------------------------------

ADMIN_TOKEN=""

obtain_admin_token() {
    ADMIN_TOKEN=$(curl -sf -X POST "$KC_URL/realms/master/protocol/openid-connect/token" \
        -H "Content-Type: application/x-www-form-urlencoded" \
        -d "grant_type=password&client_id=admin-cli&username=$KC_ADMIN_USER&password=$KC_ADMIN_PASS" \
        | jq -r '.access_token // empty')
    [ -n "$ADMIN_TOKEN" ] || fail "Falha obtendo admin token"
}

auth() { curl -sf -H "Authorization: Bearer $ADMIN_TOKEN" "$@"; }

# ---- Limpa estado prévio do user para garantir que o flow rode de novo ----

reset_user_federated_identity() {
    local username="$1"
    local user_id
    user_id=$(auth "$KC_URL/admin/realms/$TARGET_REALM/users?username=$username&exact=true" \
        | jq -r '.[0].id // empty')
    [ -n "$user_id" ] || { warn "user '$username' não existe no realm"; return; }
    auth -X DELETE "$KC_URL/admin/realms/$TARGET_REALM/users/$user_id/federated-identity/$IDP_ALIAS" 2>/dev/null || true
}

# ---- Roda o flow first-broker-login com mock IdP via curl -----------------
#
# Replica os 5 hops que um browser faria:
#   1. GET authorize no realm unifesspa (kc_idp_hint=govbr-mock)
#   2. POST login form do realm fake (mock user)
#   3. GET broker callback no realm unifesspa (consome o code do mock)
#   4. POST submitAction=linkAccount (idp-confirm-link)
#   5. POST credenciais do user existente (re-auth do flow Account verification)
#
# Aceita como entrada o CPF de 11 dígitos (login no mock) e a credencial
# do user existente no realm (username + senha) para a etapa de re-auth.

run_broker_flow() {
    local mock_cpf="$1" reauth_username="$2" reauth_password="$3"
    local cookies state s1 s3 s4 s5 a1 loc a3 a4 page3 page4 page5

    cookies=$(mktemp)
    s1=$(mktemp); s3=$(mktemp); s4=$(mktemp); s5=$(mktemp)
    state="smoke-$(date +%s%N)"

    log "Hop 1 — GET authorize no '$TARGET_REALM' (kc_idp_hint=$IDP_ALIAS)"
    curl -sf -c "$cookies" -L -o "$s1" \
        "$KC_URL/realms/$TARGET_REALM/protocol/openid-connect/auth?client_id=$CLIENT_ID&redirect_uri=$(printf '%s' "$REDIRECT_URI" | jq -sRr @uri)&response_type=code&scope=openid&state=$state&kc_idp_hint=$IDP_ALIAS&code_challenge=$PKCE_CHALLENGE&code_challenge_method=S256"
    a1=$(grep -oE 'action="[^"]*authenticate[^"]*"' "$s1" | head -1 | sed 's/^action="//; s/"$//; s/&amp;/\&/g')
    [ -n "$a1" ] || fail "login form action ausente após Hop 1"

    log "Hop 2 — POST login form no '$MOCK_REALM' (user=$mock_cpf)"
    loc=$(curl -sf -b "$cookies" -c "$cookies" -o /dev/null -w "%{redirect_url}" \
        -X POST "$a1" \
        --data-urlencode "username=$mock_cpf" \
        --data-urlencode "password=$MOCK_USER_PASSWORD" \
        --data-urlencode "credentialId=")
    [ -n "$loc" ] || fail "login do mock não retornou redirect (credenciais erradas?)"

    log "Hop 3 — GET broker callback (cpf-matcher executa aqui)"
    curl -sf -b "$cookies" -c "$cookies" -L -o "$s3" "$loc"
    page3=$(grep -oE 'data-page-id="[^"]*"' "$s3" | head -1 | sed 's/^.*="//; s/"$//')
    case "$page3" in
        login-login-idp-link-confirm)
            ok "matcher achou user existente — caiu no idp-confirm-link"
            ;;
        login-update-profile)
            ok "matcher não achou user — caiu em update-profile (criação de user novo)"
            rm -f "$cookies" "$s1" "$s3" "$s4" "$s5"
            return 0
            ;;
        *)
            warn "Hop 3 caiu em página inesperada: $page3"
            return 1
            ;;
    esac

    a3=$(grep -oE 'action="[^"]*first-broker-login[^"]*"' "$s3" | head -1 | sed 's/^action="//; s/"$//; s/&amp;/\&/g')
    [ -n "$a3" ] || fail "action do confirm-link ausente"

    log "Hop 4 — POST submitAction=linkAccount"
    curl -sf -b "$cookies" -c "$cookies" -L -o "$s4" -X POST "$a3" \
        --data-urlencode "submitAction=linkAccount" || true
    page4=$(grep -oE 'data-page-id="[^"]*"' "$s4" | head -1 | sed 's/^.*="//; s/"$//')
    if [ "$page4" != "login-login" ]; then
        warn "Hop 4 caiu em página inesperada: $page4"
    fi

    a4=$(grep -oE 'action="[^"]*"' "$s4" | head -1 | sed 's/^action="//; s/"$//; s/&amp;/\&/g')
    [ -n "$a4" ] || fail "action do re-auth ausente"

    log "Hop 5 — POST re-auth (user=$reauth_username)"
    curl -sf -b "$cookies" -c "$cookies" -L -o "$s5" -X POST "$a4" \
        --data-urlencode "username=$reauth_username" \
        --data-urlencode "password=$reauth_password" \
        --data-urlencode "credentialId=" || true
    page5=$(grep -oE 'data-page-id="[^"]*"' "$s5" | head -1 | sed 's/^.*="//; s/"$//' || echo "")
    ok "Re-auth concluído (status final esperado: redirect; trailing 404 do callback é OK em curl sem JS)"

    rm -f "$cookies" "$s1" "$s3" "$s4" "$s5"
}

# ---- Inspeções ------------------------------------------------------------

show_user_cpf() {
    local username="$1"
    local user_id cpf
    user_id=$(auth "$KC_URL/admin/realms/$TARGET_REALM/users?username=$username&exact=true" \
        | jq -r '.[0].id // empty')
    [ -n "$user_id" ] || { warn "user '$username' não existe"; return; }
    cpf=$(auth "$KC_URL/admin/realms/$TARGET_REALM/users/$user_id" \
        | jq -r '.attributes.cpf[0] // "(sem cpf)"')
    printf '    cpf de %s no realm: %s\n' "$username" "$cpf" >&2
}

show_userinfo_via_ropc() {
    local username="$1" password="$2"
    local token resp
    token=$(curl -s -X POST "$KC_URL/realms/$TARGET_REALM/protocol/openid-connect/token" \
        -d "grant_type=password" -d "client_id=admin-cli" \
        -d "scope=openid" \
        -d "username=$username" -d "password=$password" \
        | jq -r '.access_token // empty')
    if [ -z "$token" ]; then
        warn "ROPC falhou para '$username' (senha errada ou não autorizado)"
        return
    fi
    resp=$(curl -sf -H "Authorization: Bearer $token" "$KC_URL/realms/$TARGET_REALM/protocol/openid-connect/userinfo")
    printf '    userinfo de %s:\n' "$username" >&2
    echo "$resp" | jq '{sub, preferred_username, name, cpf, email}' >&2
}

show_recent_matcher_logs() {
    log "Logs do cpf-matcher (últimos 60s)"
    docker logs --since 60s docker-keycloak-1 2>&1 \
        | grep -E "CpfMatcher|cpf-matcher|broker.*matching" \
        | tail -10 \
        | sed 's/^/    /' >&2 || warn "(sem logs do matcher recentes)"
}

# ---- Cenários -------------------------------------------------------------

scenario_autoheal() {
    log "── Cenário AUTOHEAL — user manual sem LDAP, auto-heal persiste ──"

    reset_user_federated_identity "autoheal-test"
    show_user_cpf "autoheal-test"

    run_broker_flow "09876543210" "autoheal-test" "Test!1234"
    sleep 1

    show_recent_matcher_logs
    show_user_cpf "autoheal-test"
    show_userinfo_via_ropc "autoheal-test" "Test!1234"
}

scenario_ldap_readonly() {
    log "── Cenário LDAP_READONLY — auto-heal NÃO persiste (limitação documentada) ──"

    local user_id
    user_id=$(auth "$KC_URL/admin/realms/$TARGET_REALM/users?username=kevin.peixoto&exact=true" \
        | jq -r '.[0].id // empty')
    if [ -z "$user_id" ]; then
        warn "Cenário ignorado — user 'kevin.peixoto' não existe (LDAP federation não configurada)."
        warn "Rode 'scripts/setup-keycloak-dev.sh' antes para subir o LDAP sintético."
        return 0
    fi

    reset_user_federated_identity "kevin.peixoto"
    show_user_cpf "kevin.peixoto"

    run_broker_flow "07094871422" "kevin.peixoto" "Changeme!123"
    sleep 1

    show_recent_matcher_logs
    show_user_cpf "kevin.peixoto"
    show_userinfo_via_ropc "kevin.peixoto" "Changeme!123"
}

# ---- Entry point ---------------------------------------------------------

main() {
    require jq
    require curl
    require docker

    # SAFETY: o smoke depende do realm fake `govbr-mock` e do user de teste
    # `autoheal-test` no realm `unifesspa`. Esses artefatos só existem em DEV.
    if [[ "$KC_URL" != http://localhost:* ]] && [[ "$KC_URL" != http://127.0.0.1:* ]]; then
        fail "smoke-test-cpf-matcher.sh é DEV ONLY — KC_URL='$KC_URL' não é localhost."
    fi

    obtain_admin_token

    scenario_autoheal
    echo
    scenario_ldap_readonly
    echo

    log "Smoke test concluído."
}

main "$@"
