#!/usr/bin/env bash
# Ponto de entrada do serviço `ambiente-init` do docker-compose: deixa o ambiente local pronto depois que
# a API e o Keycloak sobem — configura o Keycloak (realm, personas fictícias, papel de instalação) e
# instala os dois primeiros gestores de acessos pela API (ADR-0152).
#
# Idempotente: com tudo configurado e os gestores instalados, não altera nada e termina com sucesso.
# Banco recriado, reconfigura e reinstala. Roda dentro da rede do compose. DEV ONLY.
#
# Variáveis (definidas pelo serviço em docker/docker-compose.yml): KC_URL, API_URL, MAILPIT_URL, KEYCLOAK_ADMIN,
# KEYCLOAK_ADMIN_PASSWORD, UNIPLUS_API_SELECAO_CLIENT_SECRET, CONFIGURACAO_WEB_ORIGIN,
# ARMAZENAMENTO_CONNECT_TO e INSTALAR_GESTORES (false pula a instalação).

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

log()   { echo "[ambiente-init] $*" >&2; }
falha() { echo "[ambiente-init] ERRO: $*" >&2; exit 1; }

aguardar_http() {
    local nome="$1" url="$2" segundos="$3" fim=$((SECONDS + $3))
    log "Aguardando $nome ($url)"
    while [ "$SECONDS" -lt "$fim" ]; do
        curl -fsS -o /dev/null "$url" 2>/dev/null && return 0
        sleep 3
    done
    falha "$nome não respondeu em ${segundos}s em $url."
}

main() {
    : "${KC_URL:?KC_URL não definido}" "${API_URL:?API_URL não definido}" "${MAILPIT_URL:?MAILPIT_URL não definido}"

    aguardar_http "o Keycloak" "$KC_URL/realms/unifesspa" 240
    log "Configurando o Keycloak (realm, personas fictícias, papel de instalação)"
    KC_ADMIN_USER="${KEYCLOAK_ADMIN:-admin}" KC_ADMIN_PASS="${KEYCLOAK_ADMIN_PASSWORD:-admin}" \
        bash "$SCRIPT_DIR/setup-keycloak-dev.sh" >/dev/null

    aguardar_http "a API" "$API_URL/health" 300
    if [ "${INSTALAR_GESTORES:-true}" = "true" ]; then
        bash "$SCRIPT_DIR/dev-instalar-gestores.sh"
    else
        log "Instalação dos gestores ignorada (INSTALAR_GESTORES=false)."
    fi
    log "Ambiente pronto."
}

main "$@"
