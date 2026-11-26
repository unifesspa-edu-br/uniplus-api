#!/usr/bin/env bash
# chamadas-a-api.sh — mensagens e chamada à API comuns aos roteiros de perfis. Lido com `source`; não roda sozinho.
#
# A chamada à API manda o token pelo arquivo de configuração do curl, e não pela linha de comando, onde outros
# usuários da máquina poderiam lê-lo. Exige a variável API_URL.

log()  { printf '\033[1;36m==> %s\033[0m\n' "$*" >&2; }
ok()   { printf '\033[1;32m    OK %s\033[0m\n' "$*" >&2; }
warn() { printf '\033[1;33m!! %s\033[0m\n' "$*" >&2; }
fail() { printf '\033[1;31m✗ %s\033[0m\n' "$*" >&2; exit 1; }

idem() { cat /proc/sys/kernel/random/uuid; }

# Chamada como uma pessoa; falha com o corpo da recusa. $1=passo $2=token $3=método $4=caminho $5=corpo
como() {
    local passo="$1" token="$2" metodo="$3" caminho="$4" corpo="${5:-}" resp status
    local args=(-sS -w '\n%{http_code}' -X "$metodo" "$API_URL$caminho")
    [ "$metodo" = GET ] || args+=(-H "Idempotency-Key: $(idem)")
    [ -z "$corpo" ] || args+=(-H "Content-Type: application/json" -d "$corpo")
    resp=$(curl "${args[@]}" -K <(printf 'header = "Authorization: Bearer %s"\n' "$token"))
    status=$(tail -n1 <<< "$resp")
    case "$status" in
        2??) sed '$d' <<< "$resp" ;;
        *) fail "$passo: $metodo $caminho → $status: $(sed '$d' <<< "$resp" | head -c 600)" ;;
    esac
}
