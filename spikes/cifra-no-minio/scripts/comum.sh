#!/usr/bin/env bash
# Funções compartilhadas pelos scripts da POC. Uso: source "$(dirname "$0")/comum.sh"
set -euo pipefail

POC_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ESTADO="$POC_DIR/.estado"
RESULTADOS="$POC_DIR/resultados"
KES_IMAGEM="quay.io/minio/kes:2025-03-12T09-35-18Z"

dc() {
  docker compose --project-directory "$POC_DIR" -f "$POC_DIR/docker-compose.yml" --env-file "$ESTADO/env" "$@"
}

# Cliente mc de dentro do contêiner do MinIO, com o alias "local" de administrador.
mc_admin() {
  dc exec -T minio sh -c "mc alias set local http://localhost:9000 \"\$MINIO_ROOT_USER\" \"\$MINIO_ROOT_PASSWORD\" >/dev/null && mc $*"
}

vault_cmd() {
  # shellcheck disable=SC2016
  dc exec -T -e VAULT_TOKEN="$(cat "$ESTADO/vault-root-token")" vault vault "$@"
}

# O comando impresso nunca traz segredo local (senhas, chaves, tokens): cada um sai como ***.
mascarar() {
  local linha="$*" segredo
  for segredo in "${SEGREDOS[@]}"; do
    [[ -n "$segredo" ]] && linha=${linha//"$segredo"/***}
  done
  printf '%s' "$linha"
}

# Imprime o comando e executa, para que a saída das provas mostre o que foi rodado.
mostrar() {
  printf '\n$ %s\n' "$(mascarar "$@")"
  "$@"
}

export POC_DIR ESTADO RESULTADOS KES_IMAGEM
export -f dc mc_admin

carregar_env() {
  set -a
  # shellcheck disable=SC1091
  source "$ESTADO/env"
  set +a
  SEGREDOS=("$MINIO_ROOT_PASSWORD" "$KES_API_KEY_MINIO" "$KES_API_KEY_SEM_PERMISSAO" "$TOKEN_WEBHOOK"
    "$SENHA_API" "$SENHA_VERIFICADOR" "$CHAVE_AES_LOCAL" "${KES_VAULT_SECRET_ID:-}"
    "${SENHA_POSTGRES:-}" "${SENHA_RABBITMQ:-}")
  [[ -f "$ESTADO/vault-unseal-key" ]] && SEGREDOS+=("$(cat "$ESTADO/vault-unseal-key")" "$(cat "$ESTADO/vault-root-token")")
  return 0
}
