#!/usr/bin/env bash
# Bootstrap dos catálogos administráveis (ADR-0062).
#
# Registra cada linha dos arquivos `seeds/seed-*.json` pelos endpoints admin da API,
# autenticando por client_credentials. Não usa HasData/InsertData: as linhas passam
# pelo mesmo AuditableInterceptor de qualquer escrita, e o CreatedBy é o `sub` do
# service account — a trilha distingue o que veio do bootstrap do que veio da UI.
#
# Idempotente: cada linha faz preflight de leitura antes de escrever, e o request
# relê o recurso criado. Reexecutar é seguro a qualquer momento, inclusive depois
# das 24h de TTL do cache de idempotência (ADR-0027), que protege o retry de
# transporte, não o rerun do seed.
#
# Uso:
#   ENV=dev bash tools/seeds/run.sh
#   ENV=standalone CATALOG="Tipos de ato" bash tools/seeds/run.sh
#
# Fora do dev, o secret do client de bootstrap NÃO é versionado: ele vem do
# uniplus-infra, por variável de ambiente. Os templates de standalone/hml trazem
# apenas o que é público, e o script recusa rodar sem o secret.
#
#   BOOTSTRAP_CLIENT_SECRET=***  ENV=standalone bash tools/seeds/run.sh
#
# Um ambiente completamente externo (ex.: gerado pelo pipeline) pode ser apontado
# diretamente, sem viver neste repositório:
#
#   SEED_ENV_FILE=/run/secrets/prod.postman_environment.json bash tools/seeds/run.sh
set -euo pipefail

# Pinado: `npx newman` sem versão pega o que a rede der no dia, e um seed que muda
# de comportamento sozinho não é bootstrap, é surpresa.
readonly NEWMAN_VERSION="6.2.1"

RAIZ="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
readonly RAIZ
readonly COLECAO="${RAIZ}/tools/seeds/seed-catalogos.postman_collection.json"

ENV="${ENV:-dev}"

# SEED_ENV_FILE tem precedência: permite que o deploy forneça um arquivo de ambiente
# que não vive no repositório.
if [[ -n "${SEED_ENV_FILE:-}" ]]; then
  ARQUIVO_ENV="${SEED_ENV_FILE}"
else
  ARQUIVO_ENV="${RAIZ}/tools/seeds/envs/${ENV}.postman_environment.json"
fi
readonly ARQUIVO_ENV

if [[ ! -f "${ARQUIVO_ENV}" ]]; then
  echo "erro: arquivo de ambiente não encontrado: ${ARQUIVO_ENV}" >&2
  echo "       use ENV=<nome> (templates em tools/seeds/envs/) ou SEED_ENV_FILE=<caminho>" >&2
  echo "       ambientes versionados: $(ls -1 "${RAIZ}/tools/seeds/envs" | sed 's/\.postman_environment\.json//' | tr '\n' ' ')" >&2
  exit 1
fi

# Sobrescritas por variável de ambiente. Fora do dev o secret nunca está no arquivo.
declare -a OVERRIDES=()
[[ -n "${BASE_URL:-}" ]] && OVERRIDES+=(--env-var "base_url=${BASE_URL}")
[[ -n "${KEYCLOAK_TOKEN_URL:-}" ]] && OVERRIDES+=(--env-var "keycloak_token_url=${KEYCLOAK_TOKEN_URL}")
[[ -n "${BOOTSTRAP_CLIENT_ID:-}" ]] && OVERRIDES+=(--env-var "bootstrap_client_id=${BOOTSTRAP_CLIENT_ID}")
[[ -n "${BOOTSTRAP_CLIENT_SECRET:-}" ]] && OVERRIDES+=(--env-var "bootstrap_client_secret=${BOOTSTRAP_CLIENT_SECRET}")

# Um secret vazio produziria um 401 no token endpoint e uma falha obscura no primeiro
# request. Falhar aqui nomeia a causa.
if [[ "${ENV}" != "dev" && -z "${BOOTSTRAP_CLIENT_SECRET:-}" ]]; then
  secret_no_arquivo="$(python3 -c "
import json, sys
with open('${ARQUIVO_ENV}') as f:
    valores = {v['key']: v.get('value', '') for v in json.load(f).get('values', [])}
sys.stdout.write(valores.get('bootstrap_client_secret', ''))
" 2>/dev/null || true)"

  if [[ -z "${secret_no_arquivo}" ]]; then
    echo "erro: BOOTSTRAP_CLIENT_SECRET não informado e ausente em ${ARQUIVO_ENV}" >&2
    echo "       fora do dev o secret vem do uniplus-infra, nunca do repositório" >&2
    exit 1
  fi
fi

# Um catálogo, um arquivo de dados, um folder na coleção.
declare -A CATALOGOS=(
  ["Tipos de ato"]="seeds/seed-tipos-ato.json"
)

executar() {
  local folder="$1"
  local dados="${RAIZ}/${CATALOGOS[$folder]}"

  if [[ ! -f "${dados}" ]]; then
    echo "erro: arquivo de seed ausente: ${dados}" >&2
    exit 1
  fi

  echo "==> ${folder} (${ENV})"
  npx --yes "newman@${NEWMAN_VERSION}" run "${COLECAO}" \
    --environment "${ARQUIVO_ENV}" \
    --folder "${folder}" \
    --iteration-data "${dados}" \
    --reporters cli \
    --reporter-cli-no-banner \
    ${OVERRIDES[@]+"${OVERRIDES[@]}"}
}

if [[ -n "${CATALOG:-}" ]]; then
  if [[ ! -v CATALOGOS["${CATALOG}"] ]]; then
    echo "erro: catálogo '${CATALOG}' desconhecido" >&2
    echo "disponíveis: ${!CATALOGOS[*]}" >&2
    exit 1
  fi
  executar "${CATALOG}"
else
  for folder in "${!CATALOGOS[@]}"; do
    executar "${folder}"
  done
fi

echo "bootstrap concluído (${ENV})"
