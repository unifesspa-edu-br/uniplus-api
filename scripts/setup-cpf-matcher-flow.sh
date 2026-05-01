#!/usr/bin/env bash
# setup-cpf-matcher-flow.sh — Configura o flow first-broker-login customizado
# com o SPI `uniplus-cpf-matcher` em qualquer realm Keycloak (DEV, HML, PRD).
# Aponta opcionalmente IdPs gov.br (real e/ou mock) para o flow custom.
#
# Reutilizável: este script é chamado por
#   - scripts/setup-keycloak-dev.sh        (orquestrador DEV)
#   - operadores em HML/PRD (chamada direta antes de setup-govbr-idp.sh)
#
# O que faz (idempotente; rodar 2x produz o mesmo estado)
#
#   1. Espera o realm responder.
#   2. Obtém token de admin no master realm.
#   3. Clona o flow built-in `first broker login` em `first broker login com cpf`.
#   4. Adiciona execution `uniplus-cpf-matcher` no subflow
#      `User creation or linking` como ALTERNATIVE, posicionado ANTES de
#      `idp-create-user-if-unique`.
#   5. Valida estado final: requirement=ALTERNATIVE, priority < idp-create...,
#      provider carregado pelo runtime.
#
# Apontar IdPs para este flow é responsabilidade dos scripts que CRIAM os
# IdPs — `setup-govbr-idp.sh` (gov.br real) e `setup-govbr-mock.sh` (mock DEV).
# Cada um aceita a env apropriada para definir `firstBrokerLoginFlowAlias`
# no momento da criação/atualização. O guard de downgrade silencioso no
# `setup-govbr-idp.sh` cobre o caso de re-execução com flow divergente.
#
# Variáveis de ambiente
#
#   KC_URL                  default: http://localhost:8080
#   KC_REALM                default: unifesspa
#   KC_ADMIN_USER           default: admin
#   KC_ADMIN_PASS           default: admin
#   FLOW_ALIAS              default: "first broker login com cpf"
#
# Pré-requisitos
#
#   - Keycloak no ar com o realm `$KC_REALM` importado.
#   - Provider `uniplus-cpf-matcher` carregado pelo runtime (em DEV: scripts/
#     build-keycloak-providers.sh + volume mount no compose; em HML/PRD: o
#     JAR precisa estar em /opt/keycloak/providers/ via Helm/CI).
#
# Uso
#
#   # DEV (default)
#   scripts/setup-cpf-matcher-flow.sh
#
#   # HML — direcionar para o realm institucional
#   KC_URL=https://keycloak-hom.unifesspa.edu.br \
#   KC_ADMIN_USER=... KC_ADMIN_PASS=... \
#   scripts/setup-cpf-matcher-flow.sh

set -euo pipefail

# ---- Config ---------------------------------------------------------------

KC_URL="${KC_URL:-http://localhost:8080}"
KC_REALM="${KC_REALM:-unifesspa}"
KC_ADMIN_USER="${KC_ADMIN_USER:-admin}"
KC_ADMIN_PASS="${KC_ADMIN_PASS:-admin}"
FLOW_ALIAS="${FLOW_ALIAS:-first broker login com cpf}"

readonly FLOW_PARENT_BUILTIN="first broker login"
readonly FLOW_SUBFLOW="$FLOW_ALIAS User creation or linking"
readonly CPF_MATCHER_PROVIDER_ID="uniplus-cpf-matcher"

# ---- Logging --------------------------------------------------------------

log()  { printf '\033[1;36m==> %s\033[0m\n' "$*" >&2; }
ok()   { printf '\033[1;32m    OK %s\033[0m\n' "$*" >&2; }
warn() { printf '\033[1;33m!! %s\033[0m\n' "$*" >&2; }
fail() { printf '\033[1;31m✗ %s\033[0m\n' "$*" >&2; exit 1; }

require() {
    command -v "$1" >/dev/null 2>&1 || fail "Comando ausente: $1"
}

# ---- Auth helpers ---------------------------------------------------------

ADMIN_TOKEN=""
API=""

wait_for_keycloak() {
    log "Aguardando Keycloak responder em $KC_URL/realms/$KC_REALM"
    local retries=30
    while ! curl -sf "$KC_URL/realms/$KC_REALM/.well-known/openid-configuration" >/dev/null 2>&1; do
        retries=$((retries - 1))
        [ "$retries" -le 0 ] && fail "Timeout aguardando Keycloak. Stack no ar e realm '$KC_REALM' importado?"
        printf '.'
        sleep 2
    done
    echo
    ok "Keycloak responde"
}

obtain_admin_token() {
    log "Obtendo token de admin no master realm"
    ADMIN_TOKEN=$(curl -sf -X POST "$KC_URL/realms/master/protocol/openid-connect/token" \
        -H "Content-Type: application/x-www-form-urlencoded" \
        -d "grant_type=password&client_id=admin-cli&username=$KC_ADMIN_USER&password=$KC_ADMIN_PASS" \
        | jq -r '.access_token // empty')
    [ -n "$ADMIN_TOKEN" ] || fail "Falha ao obter admin token. Credenciais corretas?"
    API="$KC_URL/admin/realms/$KC_REALM"
    ok "Admin token obtido"
}

auth()      { curl -sf -H "Authorization: Bearer $ADMIN_TOKEN" "$@"; }
auth_json() { auth -H "Content-Type: application/json" "$@"; }

url_encode_alias() {
    jq -rn --arg s "$1" '$s | @uri'
}

# ---- 1. Garante o flow custom existe (clona builtin) ---------------------

ensure_flow_exists() {
    local existing
    existing=$(auth "$API/authentication/flows" \
        | jq -r --arg n "$FLOW_ALIAS" '.[] | select(.alias==$n) | .id // empty')

    if [ -n "$existing" ]; then
        ok "flow '$FLOW_ALIAS' já existe"
        return
    fi

    local parent_encoded
    parent_encoded=$(url_encode_alias "$FLOW_PARENT_BUILTIN")
    auth_json -X POST "$API/authentication/flows/$parent_encoded/copy" \
        -d "$(jq -nc --arg n "$FLOW_ALIAS" '{newName: $n}')" >/dev/null
    ok "flow '$FLOW_ALIAS' criado a partir de '$FLOW_PARENT_BUILTIN'"
}

# ---- 2. Adiciona execution uniplus-cpf-matcher no subflow ----------------

ensure_matcher_execution() {
    local subflow_encoded executions_json existing
    subflow_encoded=$(url_encode_alias "$FLOW_SUBFLOW")
    executions_json=$(auth "$API/authentication/flows/$subflow_encoded/executions")
    existing=$(echo "$executions_json" \
        | jq -r --arg p "$CPF_MATCHER_PROVIDER_ID" '.[] | select(.providerId==$p) | .id // empty' \
        | head -n1)

    if [ -n "$existing" ]; then
        ok "execution '$CPF_MATCHER_PROVIDER_ID' já presente em '$FLOW_SUBFLOW'"
        return
    fi

    auth_json -X POST "$API/authentication/flows/$subflow_encoded/executions/execution" \
        -d "$(jq -nc --arg p "$CPF_MATCHER_PROVIDER_ID" '{provider: $p}')" >/dev/null
    ok "execution '$CPF_MATCHER_PROVIDER_ID' adicionado em '$FLOW_SUBFLOW'"
}

# ---- 3. Posiciona matcher como ALTERNATIVE antes de idp-create-user-...---

ensure_matcher_position() {
    local subflow_encoded executions_json matcher_entry create_entry matcher_id matcher_priority create_priority

    subflow_encoded=$(url_encode_alias "$FLOW_SUBFLOW")
    executions_json=$(auth "$API/authentication/flows/$subflow_encoded/executions")

    matcher_entry=$(echo "$executions_json" | jq -c --arg p "$CPF_MATCHER_PROVIDER_ID" \
        '.[] | select(.providerId==$p)')
    create_entry=$(echo "$executions_json" | jq -c \
        '.[] | select(.providerId=="idp-create-user-if-unique")')

    [ -n "$matcher_entry" ] || fail "execution do matcher sumiu inesperadamente"
    [ -n "$create_entry" ]  || fail "executor idp-create-user-if-unique não encontrado — flow alterado fora deste script?"

    matcher_id=$(echo "$matcher_entry" | jq -r '.id // empty')
    matcher_priority=$(echo "$matcher_entry" | jq -r '.priority // empty')
    create_priority=$(echo "$create_entry"  | jq -r '.priority // empty')

    [ -n "$matcher_id" ]       || fail "campo 'id' ausente na entry do matcher"
    [ -n "$matcher_priority" ] || fail "campo 'priority' ausente na entry do matcher"
    [ -n "$create_priority" ]  || fail "campo 'priority' ausente na entry de idp-create-user-if-unique"

    if [ "$(echo "$matcher_entry" | jq -r '.requirement')" != "ALTERNATIVE" ]; then
        local patched
        patched=$(echo "$matcher_entry" | jq '.requirement = "ALTERNATIVE"')
        auth_json -X PUT "$API/authentication/flows/$subflow_encoded/executions" -d "$patched" >/dev/null
        ok "execution '$CPF_MATCHER_PROVIDER_ID' marcado como ALTERNATIVE"
    fi

    if [ "$matcher_priority" -gt "$create_priority" ]; then
        local i
        for i in $(seq 1 10); do
            auth_json -X POST "$API/authentication/executions/$matcher_id/raise-priority" >/dev/null

            executions_json=$(auth "$API/authentication/flows/$subflow_encoded/executions")
            matcher_priority=$(echo "$executions_json" | jq -r --arg p "$CPF_MATCHER_PROVIDER_ID" \
                '.[] | select(.providerId==$p) | .priority')
            create_priority=$(echo "$executions_json" | jq -r \
                '.[] | select(.providerId=="idp-create-user-if-unique") | .priority')

            [ "$matcher_priority" -lt "$create_priority" ] && break
        done

        if [ "$matcher_priority" -lt "$create_priority" ]; then
            ok "execution '$CPF_MATCHER_PROVIDER_ID' reposicionado antes de idp-create-user-if-unique"
        else
            fail "não foi possível reposicionar o matcher após 10 iterações"
        fi
    fi
}

# ---- 4. Validação estrutural ---------------------------------------------

validate() {
    log "Validando setup"

    local subflow_encoded executions_json matcher_priority create_priority matcher_requirement
    subflow_encoded=$(url_encode_alias "$FLOW_SUBFLOW")
    executions_json=$(auth "$API/authentication/flows/$subflow_encoded/executions")

    matcher_priority=$(echo "$executions_json" | jq -r --arg p "$CPF_MATCHER_PROVIDER_ID" \
        '.[] | select(.providerId==$p) | .priority // empty')
    matcher_requirement=$(echo "$executions_json" | jq -r --arg p "$CPF_MATCHER_PROVIDER_ID" \
        '.[] | select(.providerId==$p) | .requirement // empty')
    create_priority=$(echo "$executions_json" | jq -r \
        '.[] | select(.providerId=="idp-create-user-if-unique") | .priority // empty')

    [ -n "$matcher_priority" ]    || fail "execution do matcher ausente"
    [ "$matcher_requirement" = "ALTERNATIVE" ] || fail "requirement do matcher é '$matcher_requirement', esperado ALTERNATIVE"
    [ "$matcher_priority" -lt "$create_priority" ] || fail "matcher priority=$matcher_priority não é menor que idp-create-user-if-unique=$create_priority"
    ok "flow OK (matcher ALTERNATIVE @priority=$matcher_priority, idp-create-user-if-unique @priority=$create_priority)"

    # Validação de runtime só faz sentido contra localhost (precisa acesso ao container)
    if [[ "$KC_URL" == http://localhost:* ]] && command -v docker >/dev/null 2>&1; then
        if docker logs docker-keycloak-1 2>&1 | grep -q "$CPF_MATCHER_PROVIDER_ID"; then
            ok "provider '$CPF_MATCHER_PROVIDER_ID' carregado pelo runtime"
        else
            warn "não encontrei evidência do provider nos logs do container — JAR montado?"
        fi
    fi
}

# ---- Entry point ---------------------------------------------------------

main() {
    require jq
    require curl

    wait_for_keycloak
    obtain_admin_token

    log "Configurando flow '$FLOW_ALIAS' (matching por CPF via $CPF_MATCHER_PROVIDER_ID)"
    ensure_flow_exists
    ensure_matcher_execution
    ensure_matcher_position
    validate
}

main "$@"
