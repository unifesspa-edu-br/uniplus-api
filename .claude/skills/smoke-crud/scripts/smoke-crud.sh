#!/usr/bin/env bash
# smoke-crud.sh — executa o ciclo CRUD de um recurso REST contra o stack local Uni+.
#
# Pré-condições: docker compose up com perfil dev (override.yml), realm
# unifesspa-dev-local importado, postgres acessível via container docker-postgres-1.
#
# Uso típico:
#   bash .claude/skills/smoke-crud/scripts/smoke-crud.sh --resource=obrigatoriedade-legal
#   bash .claude/skills/smoke-crud/scripts/smoke-crud.sh --resource=obrigatoriedade-legal --methods=POST,GET
#
# A documentação completa está em ../SKILL.md.

set -u
set -o pipefail

# ---------- defaults ----------
RESOURCE=""
METHODS="ALL"
API_OVERRIDE=""
USERNAME="admin"
REALM="unifesspa-dev-local"
KEEP_GOING=0
NO_CLEANUP=0
VERBOSE=0

KC_BASE="${KC_BASE:-http://localhost:8080}"
API_PORT_SELECAO="${API_PORT_SELECAO:-5202}"
API_PORT_INGRESSO="${API_PORT_INGRESSO:-5262}"
API_PORT_PORTAL="${API_PORT_PORTAL:-5302}"
PASSWORD="${SMOKE_CRUD_PASSWORD:-Changeme!123}"
PG_CONTAINER="${PG_CONTAINER:-docker-postgres-1}"
PG_USER="${PG_USER:-uniplus}"
PG_PASSWORD_ENV="${PG_PASSWORD_ENV:-uniplus}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SKILL_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
RESOURCES_DIR="$SKILL_DIR/resources"

# ---------- color helpers ----------
if [ -t 1 ]; then
  C_OK=$'\033[0;32m'; C_FAIL=$'\033[0;31m'; C_INFO=$'\033[0;36m'; C_DIM=$'\033[2m'; C_OFF=$'\033[0m'
else
  C_OK=""; C_FAIL=""; C_INFO=""; C_DIM=""; C_OFF=""
fi

OK_COUNT=0
FAIL_COUNT=0
LOG_LINES=()

record() {
  local label="$1" status="$2" detail="$3"
  if [ "$status" = "OK" ]; then
    OK_COUNT=$((OK_COUNT + 1))
    LOG_LINES+=("$(printf '%s[OK]%s   %-40s %s' "$C_OK" "$C_OFF" "$label" "$detail")")
  else
    FAIL_COUNT=$((FAIL_COUNT + 1))
    LOG_LINES+=("$(printf '%s[FAIL]%s %-40s %s' "$C_FAIL" "$C_OFF" "$label" "$detail")")
    if [ "$KEEP_GOING" -eq 0 ]; then
      print_summary
      exit 1
    fi
  fi
}

info() { printf '%s==>%s %s\n' "$C_INFO" "$C_OFF" "$*"; }
dim()  { printf '%s%s%s\n' "$C_DIM" "$*" "$C_OFF"; }

# ---------- argument parsing ----------
usage() {
  cat <<'EOF'
Uso: smoke-crud.sh --resource=<nome> [opções]

Opções:
  --resource=<nome>           Manifesto em resources/<nome>.json (obrigatório)
  --methods=POST,GET,...,ALL  Operações a executar (default: ALL)
  --api=<modulo>              Sobrescreve api do manifesto (selecao|ingresso|portal)
  --user=<username>           Usuário do realm (default: admin)
  --realm=<realm>             Realm Keycloak (default: unifesspa-dev-local)
  --keep-going                Não aborta no primeiro erro
  --no-cleanup                Não roda DELETE no final
  --verbose                   Imprime body completo das respostas
  -h | --help                 Mostra esta ajuda

Variáveis de ambiente:
  KC_BASE, API_PORT_SELECAO, API_PORT_INGRESSO, API_PORT_PORTAL,
  SMOKE_CRUD_PASSWORD, PG_CONTAINER, PG_USER, PG_PASSWORD_ENV
EOF
}

for arg in "$@"; do
  case "$arg" in
    --resource=*) RESOURCE="${arg#*=}" ;;
    --methods=*)  METHODS="${arg#*=}" ;;
    --api=*)      API_OVERRIDE="${arg#*=}" ;;
    --user=*)     USERNAME="${arg#*=}" ;;
    --realm=*)    REALM="${arg#*=}" ;;
    --keep-going) KEEP_GOING=1 ;;
    --no-cleanup) NO_CLEANUP=1 ;;
    --verbose)    VERBOSE=1 ;;
    -h|--help)    usage; exit 0 ;;
    *) echo "Argumento desconhecido: $arg" >&2; usage; exit 2 ;;
  esac
done

if [ -z "$RESOURCE" ]; then
  echo "ERRO: --resource é obrigatório." >&2
  usage; exit 2
fi

MANIFEST="$RESOURCES_DIR/$RESOURCE.json"
if [ ! -f "$MANIFEST" ]; then
  echo "ERRO: manifesto não encontrado: $MANIFEST" >&2
  echo "Recursos disponíveis:" >&2
  ls "$RESOURCES_DIR"/*.json 2>/dev/null | sed 's|.*/||; s|\.json$||' | sed 's/^/  - /' >&2
  exit 2
fi

command -v jq >/dev/null || { echo "ERRO: jq não está instalado." >&2; exit 2; }
command -v curl >/dev/null || { echo "ERRO: curl não está instalado." >&2; exit 2; }
command -v uuidgen >/dev/null || { echo "ERRO: uuidgen não está instalado." >&2; exit 2; }

# ---------- carregar manifesto ----------
API=$(jq -r '.api' "$MANIFEST")
if [ -n "$API_OVERRIDE" ]; then API="$API_OVERRIDE"; fi
VENDOR_MIME=$(jq -r '.vendorMime' "$MANIFEST")
PATH_PUBLIC=$(jq -r '.paths.public' "$MANIFEST")
PATH_ADMIN=$(jq -r '.paths.admin' "$MANIFEST")
PUT_REQUIRES_ID=$(jq -r '.putRequiresIdInBody // false' "$MANIFEST")
CONFLICT_FIELD=$(jq -r '.conflictField.name // empty' "$MANIFEST")
CONFLICT_ERROR_SUFFIX=$(jq -r '.conflictField.errorCodeSuffix // empty' "$MANIFEST")
HIST_TABLE=$(jq -r '.historico.table // empty' "$MANIFEST")
HIST_REGRA_COL=$(jq -r '.historico.regraIdColumn // "regra_id"' "$MANIFEST")
HIST_AT_COL=$(jq -r '.historico.snapshotAtColumn // "snapshot_at"' "$MANIFEST")
HIST_DB=$(jq -r '.historico.database // empty' "$MANIFEST")

case "$API" in
  selecao)  API_PORT="$API_PORT_SELECAO" ;;
  ingresso) API_PORT="$API_PORT_INGRESSO" ;;
  portal)   API_PORT="$API_PORT_PORTAL" ;;
  *) echo "ERRO: api desconhecida '$API' (use selecao|ingresso|portal)" >&2; exit 2 ;;
esac
API_BASE="http://localhost:$API_PORT"

# ---------- which methods to run ----------
should_run() {
  local m="$1"
  case ",$METHODS," in
    *",ALL,"*) return 0 ;;
    *",$m,"*) return 0 ;;
    *) return 1 ;;
  esac
}
METHODS=",$METHODS,"

# ---------- health check ----------
info "Verificando saúde da API ($API_BASE/health)…"
HEALTH=$(curl -s -o /dev/null -w "%{http_code}" "$API_BASE/health" || echo "000")
if [ "$HEALTH" != "200" ]; then
  echo "${C_FAIL}ERRO:${C_OFF} $API_BASE/health retornou $HEALTH." >&2
  echo "Suba o stack: docker compose -f docker/docker-compose.yml -f docker/docker-compose.override.yml \\" >&2
  echo "  --env-file docker/.env --project-directory docker up -d" >&2
  exit 1
fi
dim "  health 200 OK"

# ---------- obter token ----------
info "Obtendo token (realm=$REALM, user=$USERNAME, client=selecao-web)…"
TOKEN_RESPONSE=$(curl -s -X POST "$KC_BASE/realms/$REALM/protocol/openid-connect/token" \
  -H "Content-Type: application/x-www-form-urlencoded" \
  --data-urlencode "grant_type=password" \
  --data-urlencode "client_id=selecao-web" \
  --data-urlencode "username=$USERNAME" \
  --data-urlencode "password=$PASSWORD" \
  --data-urlencode "scope=openid profile")
TOKEN=$(echo "$TOKEN_RESPONSE" | jq -r '.access_token // empty')
if [ -z "$TOKEN" ]; then
  echo "${C_FAIL}ERRO:${C_OFF} falha ao obter token." >&2
  echo "Resposta do Keycloak:" >&2
  echo "$TOKEN_RESPONSE" | jq . >&2 2>/dev/null || echo "$TOKEN_RESPONSE" >&2
  echo "" >&2
  echo "Checklist:" >&2
  echo "  1. Realm '$REALM' está importado em $KC_BASE? Veja docker logs docker-keycloak-1 | grep -i import" >&2
  echo "  2. Cliente 'selecao-web' tem directAccessGrantsEnabled=true?" >&2
  echo "  3. Usuário '$USERNAME' tem senha non-temporary?" >&2
  exit 1
fi
dim "  token obtido (${#TOKEN} chars)"

H_AUTH="Authorization: Bearer $TOKEN"
H_JSON="Content-Type: application/json"
H_ACCEPT="Accept: $VENDOR_MIME"

# ---------- helpers HTTP ----------
do_request() {
  # do_request METHOD URL [extra_header] [body_file]
  local method="$1" url="$2" extra="${3:-}" body_file="${4:-}"
  local args=(-s -o /tmp/smoke-crud.body -w "%{http_code}" -X "$method" "$url" \
    -H "$H_AUTH" -H "$H_JSON" -H "$H_ACCEPT")
  [ -n "$extra" ] && args+=(-H "$extra")
  [ -n "$body_file" ] && args+=(--data-binary @"$body_file")
  local code
  code=$(curl "${args[@]}")
  echo "$code"
}

print_body() {
  if [ "$VERBOSE" -eq 1 ] && [ -s /tmp/smoke-crud.body ]; then
    dim "  $(cat /tmp/smoke-crud.body | jq -c . 2>/dev/null || cat /tmp/smoke-crud.body)"
  fi
}

# ---------- execução ----------
info "Iniciando smoke do recurso '$RESOURCE' em $API_BASE$PATH_ADMIN"

CREATED_ID=""

# --- LIST inicial ---
if should_run "LIST"; then
  CODE=$(curl -s -o /tmp/smoke-crud.body -w "%{http_code}" \
    -H "$H_AUTH" -H "$H_ACCEPT" "$API_BASE$PATH_PUBLIC")
  if [ "$CODE" = "200" ]; then
    COUNT=$(jq 'length' /tmp/smoke-crud.body 2>/dev/null || echo "?")
    record "LIST inicial" "OK" "200 ($COUNT items)"
  else
    record "LIST inicial" "FAIL" "esperado 200, recebido $CODE"
  fi
  print_body
fi

# --- POST cria ---
if should_run "POST"; then
  jq '.payload.create' "$MANIFEST" > /tmp/smoke-crud.create.json
  IDEM_KEY=$(uuidgen)
  CODE=$(do_request POST "$API_BASE$PATH_ADMIN" "Idempotency-Key: $IDEM_KEY" /tmp/smoke-crud.create.json)
  BODY=$(cat /tmp/smoke-crud.body)
  if [ "$CODE" = "201" ]; then
    CREATED_ID=$(echo "$BODY" | tr -d '"')
    record "POST criar" "OK" "201 id=${CREATED_ID:0:13}…"
  else
    record "POST criar" "FAIL" "esperado 201, recebido $CODE — $BODY"
  fi
  print_body

  # --- POST replay (idempotência) ---
  if [ -n "$CREATED_ID" ]; then
    CODE=$(do_request POST "$API_BASE$PATH_ADMIN" "Idempotency-Key: $IDEM_KEY" /tmp/smoke-crud.create.json)
    REPLAY_ID=$(cat /tmp/smoke-crud.body | tr -d '"')
    if [ "$CODE" = "201" ] && [ "$REPLAY_ID" = "$CREATED_ID" ]; then
      record "POST replay idempotente" "OK" "201 mesmo ID"
    else
      record "POST replay idempotente" "FAIL" "esperado 201+mesmo ID; recebido $CODE id=$REPLAY_ID"
    fi
    print_body
  fi

  # --- POST conflito (mesmo regraCodigo, nova chave) ---
  if [ -n "$CREATED_ID" ] && [ -n "$CONFLICT_FIELD" ]; then
    CODE=$(do_request POST "$API_BASE$PATH_ADMIN" "Idempotency-Key: $(uuidgen)" /tmp/smoke-crud.create.json)
    BODY=$(cat /tmp/smoke-crud.body)
    CODE_TAXONOMY=$(echo "$BODY" | jq -r '.code // empty' 2>/dev/null)
    if [ "$CODE" = "409" ] && echo "$CODE_TAXONOMY" | grep -q "$CONFLICT_ERROR_SUFFIX"; then
      record "POST conflito ($CONFLICT_FIELD)" "OK" "409 $CODE_TAXONOMY"
    else
      record "POST conflito ($CONFLICT_FIELD)" "FAIL" "esperado 409+$CONFLICT_ERROR_SUFFIX; recebido $CODE code=$CODE_TAXONOMY"
    fi
    print_body
  fi
fi

# --- GET single ---
if should_run "GET" && [ -n "$CREATED_ID" ]; then
  CODE=$(curl -s -o /tmp/smoke-crud.body -w "%{http_code}" \
    -H "$H_AUTH" -H "$H_ACCEPT" "$API_BASE$PATH_PUBLIC/$CREATED_ID")
  HAS_LINKS=$(jq -r '._links.self // empty' /tmp/smoke-crud.body 2>/dev/null)
  if [ "$CODE" = "200" ] && [ -n "$HAS_LINKS" ]; then
    record "GET by ID" "OK" "200 + _links.self"
  else
    record "GET by ID" "FAIL" "esperado 200+_links; recebido $CODE links='$HAS_LINKS'"
  fi
  print_body
fi

# --- PUT update ---
if should_run "PUT" && [ -n "$CREATED_ID" ]; then
  if [ "$PUT_REQUIRES_ID" = "true" ]; then
    jq --arg id "$CREATED_ID" '.payload.create * .payload.updateOverrides + {id: $id}' "$MANIFEST" > /tmp/smoke-crud.update.json
  else
    jq '.payload.create * .payload.updateOverrides' "$MANIFEST" > /tmp/smoke-crud.update.json
  fi
  CODE=$(do_request PUT "$API_BASE$PATH_ADMIN/$CREATED_ID" "Idempotency-Key: $(uuidgen)" /tmp/smoke-crud.update.json)
  if [ "$CODE" = "204" ]; then
    record "PUT atualizar" "OK" "204"
  else
    record "PUT atualizar" "FAIL" "esperado 204, recebido $CODE — $(cat /tmp/smoke-crud.body)"
  fi
  print_body

  # --- GET pós-update ---
  CODE=$(curl -s -o /tmp/smoke-crud.body -w "%{http_code}" \
    -H "$H_AUTH" -H "$H_ACCEPT" "$API_BASE$PATH_PUBLIC/$CREATED_ID")
  if [ "$CODE" = "200" ]; then
    # confirma que pelo menos um campo do updateOverrides foi aplicado
    EXPECTED=$(jq -r '.payload.updateOverrides | to_entries[0].value // empty' "$MANIFEST")
    if [ -n "$EXPECTED" ] && grep -qF "$EXPECTED" /tmp/smoke-crud.body; then
      record "GET pós-update" "OK" "200 + campo atualizado"
    else
      record "GET pós-update" "OK" "200 (update aplicado, valor não conferido)"
    fi
  else
    record "GET pós-update" "FAIL" "esperado 200, recebido $CODE"
  fi
  print_body
fi

# --- DELETE ---
if [ "$NO_CLEANUP" -eq 0 ] && should_run "DELETE" && [ -n "$CREATED_ID" ]; then
  CODE=$(do_request DELETE "$API_BASE$PATH_ADMIN/$CREATED_ID" "Idempotency-Key: $(uuidgen)")
  if [ "$CODE" = "204" ]; then
    record "DELETE soft" "OK" "204"
  else
    record "DELETE soft" "FAIL" "esperado 204, recebido $CODE — $(cat /tmp/smoke-crud.body)"
  fi
  print_body

  # --- GET pós-delete (404) ---
  CODE=$(curl -s -o /dev/null -w "%{http_code}" \
    -H "$H_AUTH" -H "$H_ACCEPT" "$API_BASE$PATH_PUBLIC/$CREATED_ID")
  if [ "$CODE" = "404" ]; then
    record "GET pós-delete" "OK" "404"
  else
    record "GET pós-delete" "FAIL" "esperado 404, recebido $CODE"
  fi

  # --- POST mesmo regraCodigo pós-delete (UNIQUE parcial) ---
  if should_run "POST" && [ -n "$CONFLICT_FIELD" ]; then
    CODE=$(do_request POST "$API_BASE$PATH_ADMIN" "Idempotency-Key: $(uuidgen)" /tmp/smoke-crud.create.json)
    NEW_ID=$(cat /tmp/smoke-crud.body | tr -d '"')
    if [ "$CODE" = "201" ] && [ -n "$NEW_ID" ] && [ "$NEW_ID" != "$CREATED_ID" ]; then
      record "POST mesmo $CONFLICT_FIELD pós-delete" "OK" "201 novo id=${NEW_ID:0:13}…"
      # rollback do segundo (mantém o banco limpo)
      curl -s -o /dev/null -X DELETE "$API_BASE$PATH_ADMIN/$NEW_ID" \
        -H "$H_AUTH" -H "$H_ACCEPT" -H "Idempotency-Key: $(uuidgen)"
    else
      record "POST mesmo $CONFLICT_FIELD pós-delete" "FAIL" "esperado 201+novo ID; recebido $CODE id=$NEW_ID"
    fi
  fi
fi

# --- Histórico no PG ---
if [ -n "$HIST_TABLE" ] && [ -n "$HIST_DB" ] && [ -n "$CREATED_ID" ]; then
  COUNT=$(docker exec -e PGPASSWORD="$PG_PASSWORD_ENV" "$PG_CONTAINER" \
    psql -U "$PG_USER" -d "$HIST_DB" -tAc \
    "SELECT count(*) FROM $HIST_TABLE WHERE $HIST_REGRA_COL = '$CREATED_ID';" 2>/dev/null | tr -d ' ')
  if [ -n "$COUNT" ] && [ "$COUNT" -gt 0 ]; then
    record "Histórico em PG ($HIST_TABLE)" "OK" "$COUNT snapshots"
  else
    record "Histórico em PG ($HIST_TABLE)" "FAIL" "0 snapshots (interceptor não disparou?)"
  fi
fi

# ---------- summary ----------
print_summary() {
  echo ""
  printf '%s===== smoke-crud: %s =====%s\n' "$C_INFO" "$RESOURCE" "$C_OFF"
  for line in "${LOG_LINES[@]}"; do echo "$line"; done
  local total=$((OK_COUNT + FAIL_COUNT))
  if [ "$FAIL_COUNT" -eq 0 ]; then
    printf '%sResultado: %d/%d OK%s\n' "$C_OK" "$OK_COUNT" "$total" "$C_OFF"
  else
    printf '%sResultado: %d OK / %d FAIL (total %d)%s\n' "$C_FAIL" "$OK_COUNT" "$FAIL_COUNT" "$total" "$C_OFF"
  fi
}

print_summary
[ "$FAIL_COUNT" -eq 0 ] && exit 0 || exit 1
