#!/usr/bin/env bash
# Instala os dois primeiros gestores de acessos do ambiente local pelo mesmo caminho da API
# (ADR-0152): inicia, envia o PDF de designação, confirma o documento, envia o código, lê o código
# no Mailpit e confirma. Não grava concessão no banco.
#
# Idempotente: com gestor já instalado, confere o acesso e termina sem alterar nada. Banco
# recriado, sem gestor, reinstala. Uma tentativa abandonada no meio é cancelada pela API ao iniciar
# a seguinte.
#
# Pré-requisitos: ambiente no ar e Keycloak configurado (o serviço ambiente-init do compose faz os dois), jq e curl.
#
# Uso:
#   scripts/dev-instalar-gestores.sh
#
# Variáveis de ambiente opcionais:
#   KC_URL                  (default: http://localhost:${KEYCLOAK_PORT:-8080})
#   API_URL                 (default: http://localhost:${API_PORT:-5200})
#   MAILPIT_URL             (default: http://localhost:${MAILPIT_UI_PORT:-8025})
#   KC_REALM                (default: unifesspa)
#   EXECUTOR                (default: admin) — quem executa; não pode estar entre os gestores
#   GESTORES                (default: "diogo.souza leandro.viana") — duas personas fictícias do portal
#   CANDIDATO               (default: candidato) — usuário que confirma a recusa de acesso
#   TEST_PASSWORD           (default: Uni+Teste26) — senha das personas e do executor
#   KEYCLOAK_ADMIN / KEYCLOAK_ADMIN_PASSWORD — administrador do realm master (default: admin)
#   ESPERA_VERIFICACAO      (default: 240) — segundos para o verificador liberar o PDF
#   ARMAZENAMENTO_CONNECT_TO — `host:porta:destino:porta` do curl para o envio do PDF. Dentro da rede do
#                           compose a URL assinada aponta para localhost:<porta do host>, que não é o MinIO ali;
#                           o ambiente-init a redireciona para minio:9000 sem trocar o Host, que entra na assinatura.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PDF_DE_DESIGNACAO="$SCRIPT_DIR/edital-de-teste.pdf"

KC_URL="${KC_URL:-http://localhost:${KEYCLOAK_PORT:-8080}}"
API_URL="${API_URL:-http://localhost:${API_PORT:-5200}}"
MAILPIT_URL="${MAILPIT_URL:-http://localhost:${MAILPIT_UI_PORT:-8025}}"
KC_REALM="${KC_REALM:-unifesspa}"
EXECUTOR="${EXECUTOR:-admin}"
GESTORES="${GESTORES:-diogo.souza leandro.viana}"
CANDIDATO="${CANDIDATO:-candidato}"
TEST_PASSWORD="${TEST_PASSWORD:-Uni+Teste26}"
KEYCLOAK_ADMIN="${KEYCLOAK_ADMIN:-admin}"
KEYCLOAK_ADMIN_PASSWORD="${KEYCLOAK_ADMIN_PASSWORD:-admin}"
ESPERA_VERIFICACAO="${ESPERA_VERIFICACAO:-240}"

TMP_DIR="$(mktemp -d)"
trap 'rm -rf "$TMP_DIR"' EXIT

log()  { echo "[gestores] $*" >&2; }
falha() { echo "[gestores] ERRO: $*" >&2; exit 1; }

for ferramenta in jq curl; do
    command -v "$ferramenta" >/dev/null || falha "'$ferramenta' não encontrado no PATH."
done
[ -f "$PDF_DE_DESIGNACAO" ] || falha "PDF fictício de designação ausente: $PDF_DE_DESIGNACAO"

read -r -a LISTA_DE_GESTORES <<< "$GESTORES"
[ "${#LISTA_DE_GESTORES[@]}" -eq 2 ] || falha "GESTORES deve ter exatamente duas personas (recebido: '$GESTORES')."
for gestor in "${LISTA_DE_GESTORES[@]}"; do
    [ "$gestor" != "$EXECUTOR" ] || falha "o executor '$EXECUTOR' não pode estar entre os gestores (instalacao.executor_entre_gestores)."
done

# Token do usuário do realm. --data-urlencode, não -d: a senha tem '+'.
token_do_usuario() {
    local usuario="$1" token
    token=$(curl -fsS -X POST "$KC_URL/realms/$KC_REALM/protocol/openid-connect/token" \
        --data-urlencode grant_type=password --data-urlencode client_id=admin-cli \
        --data-urlencode "username=$usuario" --data-urlencode "password=$TEST_PASSWORD" \
        | jq -r '.access_token // empty') \
        || falha "o Keycloak recusou o login de '$usuario' em $KC_URL. Rode scripts/setup-keycloak-dev.sh (define a senha das personas)."
    [ -n "$token" ] || falha "o Keycloak não devolveu token para '$usuario'."
    printf '%s\n' "$token"
}

token_do_administrador_do_keycloak() {
    curl -fsS -X POST "$KC_URL/realms/master/protocol/openid-connect/token" \
        --data-urlencode grant_type=password --data-urlencode client_id=admin-cli \
        --data-urlencode "username=$KEYCLOAK_ADMIN" --data-urlencode "password=$KEYCLOAK_ADMIN_PASSWORD" \
        | jq -r '.access_token // empty'
}

# Devolve o usuário do realm como JSON ({id, username, email, firstName, lastName}).
usuario_do_realm() {
    local usuario="$1" token resposta
    token=$(token_do_administrador_do_keycloak)
    [ -n "$token" ] || falha "login do administrador do Keycloak recusado em $KC_URL (KEYCLOAK_ADMIN / KEYCLOAK_ADMIN_PASSWORD)."
    resposta=$(curl -fsS -H "Authorization: Bearer $token" \
        "$KC_URL/admin/realms/$KC_REALM/users?username=$usuario&exact=true" | jq -c '.[0] // empty')
    [ -n "$resposta" ] || falha "usuário '$usuario' não existe no realm '$KC_REALM'. Rode scripts/setup-keycloak-dev.sh (cria as personas)."
    printf '%s\n' "$resposta"
}

codigo_http() {
    curl -s -o /dev/null -w '%{http_code}' "$@" || true
}

acesso_as_concessoes() {
    codigo_http -H "Authorization: Bearer $(token_do_usuario "$1")" "$API_URL/api/autorizacao/concessoes"
}

# Chamada que precisa dar certo; o corpo da recusa vai na mensagem de erro. Corpo em $TMP_DIR/corpo.
chamar() {
    local passo="$1" esperado="$2" metodo="$3" url="$4" token="$5"
    shift 5
    local status
    status=$(curl -s -o "$TMP_DIR/corpo" -w '%{http_code}' -X "$metodo" "$url" \
        -H "Authorization: Bearer $token" \
        -H "Idempotency-Key: $(cat /proc/sys/kernel/random/uuid)" "$@" || true)
    if [ "$status" != "$esperado" ]; then
        falha "$passo respondeu $status (esperado $esperado): $(head -c 600 "$TMP_DIR/corpo" 2>/dev/null)"
    fi
}

aguardar_documento_verificado() {
    local id="$1" token="$2" situacao="" fim=$((SECONDS + ESPERA_VERIFICACAO))
    while [ "$SECONDS" -lt "$fim" ]; do
        situacao=$(curl -s -H "Authorization: Bearer $token" "$API_URL/api/autorizacao/instalacao/$id" \
            | jq -r '.situacao // empty' 2>/dev/null || true)
        [ "$situacao" = "documento_verificado" ] && return 0
        sleep 3
    done
    falha "o documento não chegou a 'documento_verificado' em ${ESPERA_VERIFICACAO}s (última situação: '${situacao:-desconhecida}'). Confira o serviço 'verificador': docker compose -f docker/docker-compose.yml logs verificador clamd-verificador (com COMPOSE_PROJECT_NAME igual ao do ambiente, se houver)."
}

# Lê o código mais recente enviado ao e-mail. A caixa do executor é limpa antes do passo 3, então só
# existe a mensagem desta tentativa.
ler_codigo_no_mailpit() {
    local email="$1" id texto codigo fim=$((SECONDS + 60))
    while [ "$SECONDS" -lt "$fim" ]; do
        id=$(curl -s -G "$MAILPIT_URL/api/v1/search" --data-urlencode "query=to:$email" \
            | jq -r '.messages[0].ID // empty' 2>/dev/null || true)
        if [ -n "$id" ]; then
            texto=$(curl -s "$MAILPIT_URL/api/v1/message/$id" | jq -r '.Text // empty')
            codigo=$(grep -oE '[0-9]{8}' <<< "$texto" | head -1 || true)
            if [ -n "$codigo" ]; then
                printf '%s\n' "$codigo"
                return 0
            fi
        fi
        sleep 2
    done
    falha "o código não chegou ao Mailpit ($MAILPIT_URL) para '$email' em 60s."
}

limpar_caixa_do_executor() {
    curl -s -G -X DELETE "$MAILPIT_URL/api/v1/search" --data-urlencode "query=to:$1" >/dev/null || true
}

conferir_acesso() {
    local ok=0 usuario status esperado
    log "Conferindo GET /api/autorizacao/concessoes:"
    for usuario in "${LISTA_DE_GESTORES[@]}" "$EXECUTOR" "$CANDIDATO"; do
        esperado=403
        for gestor in "${LISTA_DE_GESTORES[@]}"; do
            [ "$usuario" = "$gestor" ] && esperado=200
        done
        status=$(acesso_as_concessoes "$usuario")
        if [ "$status" = "$esperado" ]; then
            log "  $usuario: $status (esperado)"
        else
            log "  $usuario: $status (esperado $esperado)"
            ok=1
        fi
    done
    return "$ok"
}

instalar() {
    local token_executor emissor tamanho executor_json gestor_json corpo inicio id url etag codigo
    local -a gestores_json=()

    token_executor=$(token_do_usuario "$EXECUTOR")
    executor_json=$(usuario_do_realm "$EXECUTOR")
    # O emissor dos gestores é o `iss` do token do executor, sem edição.
    emissor=$(jq -rR 'split(".")[1] | gsub("-"; "+") | gsub("_"; "/") | @base64d | fromjson | .iss // empty' <<< "$token_executor")
    [ -n "$emissor" ] || falha "não foi possível ler o emissor (iss) do token do executor."

    for gestor in "${LISTA_DE_GESTORES[@]}"; do
        gestor_json=$(usuario_do_realm "$gestor")
        gestores_json+=("$(jq -c --arg iss "$emissor" \
            '{emissor: $iss, sub: .id, email: .email, nome: ((.firstName // "") + " " + (.lastName // "") | gsub("^ +| +$"; ""))}' <<< "$gestor_json")")
    done
    tamanho=$(stat -c%s "$PDF_DE_DESIGNACAO")
    corpo=$(jq -nc --argjson gestores "$(printf '%s\n' "${gestores_json[@]}" | jq -sc .)" --argjson tamanho "$tamanho" \
        '{gestores: $gestores, numeroDoDocumento: "PORTARIA-LOCAL-0001/2026", tamanhoDoPdf: $tamanho}')

    log "Passo 1 — iniciando a instalação (executor: $EXECUTOR)"
    chamar "o passo 1 (iniciar)" 201 POST "$API_URL/api/autorizacao/instalacao" "$token_executor" \
        -H "Content-Type: application/json" -d "$corpo"
    inicio=$(cat "$TMP_DIR/corpo")
    id=$(jq -r '.id // empty' <<< "$inicio")
    url=$(jq -r '.urlDeEnvio // empty' <<< "$inicio")
    [[ -n "$id" && -n "$url" ]] || falha "o passo 1 não devolveu id e URL de envio: $inicio"

    log "Passo 2 — enviando o PDF de designação e confirmando o documento"
    local -a redirecionamento=()
    [ -z "${ARMAZENAMENTO_CONNECT_TO:-}" ] || redirecionamento=(--connect-to "$ARMAZENAMENTO_CONNECT_TO")
    etag=$(curl -fsS -D - -o /dev/null -X PUT -H "Content-Type: application/pdf" ${redirecionamento[@]+"${redirecionamento[@]}"} --data-binary @"$PDF_DE_DESIGNACAO" "$url" \
        | tr -d '\r' | awk -F': ' 'tolower($1)=="etag"{print $2}') \
        || falha "o envio do PDF à URL assinada falhou (MinIO em porta acessível? Storage__PublicEndpoint do docker-compose.yml)."
    etag="${etag//\"/}"
    [ -n "$etag" ] || falha "o armazenamento não devolveu ETag no envio do PDF."
    chamar "o passo 2 (confirmar o documento)" 200 POST "$API_URL/api/autorizacao/instalacao/$id/documento/confirmacao" "$token_executor" \
        -H "Content-Type: application/json" -d "$(jq -nc --arg etag "$etag" '{etag: $etag}')"
    log "  aguardando o verificador de arquivos liberar o PDF"
    aguardar_documento_verificado "$id" "$token_executor"

    log "Passo 3 — enviando o código ao e-mail do executor"
    limpar_caixa_do_executor "$(jq -r '.email' <<< "$executor_json")"
    chamar "o passo 3 (enviar o código)" 200 POST "$API_URL/api/autorizacao/instalacao/$id/codigo" "$token_executor"
    codigo=$(ler_codigo_no_mailpit "$(jq -r '.email' <<< "$executor_json")")

    log "Passo 4 — confirmando com o código lido no Mailpit"
    chamar "o passo 4 (confirmar)" 204 POST "$API_URL/api/autorizacao/instalacao/confirmacao" "$token_executor" \
        -H "Content-Type: application/json" -d "$(jq -nc --arg codigo "$codigo" '{codigo: $codigo}')"
}

main() {
    if [ "$(acesso_as_concessoes "${LISTA_DE_GESTORES[0]}")" = "200" ]; then
        log "Gestores já instalados; nada a fazer."
    else
        instalar
        log "Gestores instalados: ${LISTA_DE_GESTORES[*]}"
    fi
    conferir_acesso || falha "o acesso não ficou como esperado (gestores 200; executor e candidato 403)."
}

main "$@"
