#!/usr/bin/env bash
# Bootstrap dos catálogos administráveis (ADR-0062).
#
# Registra cada linha dos arquivos `seeds/seed-*.json` pelos endpoints admin da API,
# autenticando por client_credentials. Não usa HasData/InsertData: as linhas passam
# pelo mesmo AuditableInterceptor de qualquer escrita, e o CreatedBy é o `sub` do
# service account — a trilha distingue o que veio do bootstrap do que veio da UI.
#
# Idempotente: cada linha faz preflight de leitura antes de escrever. Reexecutar é
# seguro a qualquer momento, inclusive depois das 24h de TTL do cache de
# idempotência (ADR-0027), que protege o retry de transporte, não o rerun do seed.
#
# Uso:
#   ENV=dev bash tools/seeds/run.sh
#   ENV=standalone CATALOG="Tipos de ato" bash tools/seeds/run.sh
set -euo pipefail

# Pinado: `npx newman` sem versão pega o que a rede der no dia, e um seed que muda
# de comportamento sozinho não é bootstrap, é surpresa.
readonly NEWMAN_VERSION="6.2.1"

readonly RAIZ="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
readonly COLECAO="${RAIZ}/tools/seeds/seed-catalogos.postman_collection.json"

ENV="${ENV:-dev}"
readonly ARQUIVO_ENV="${RAIZ}/tools/seeds/envs/${ENV}.postman_environment.json"

if [[ ! -f "${ARQUIVO_ENV}" ]]; then
  echo "erro: ambiente '${ENV}' não encontrado em tools/seeds/envs/" >&2
  echo "disponíveis: $(ls -1 "${RAIZ}/tools/seeds/envs" | sed 's/\.postman_environment\.json//' | tr '\n' ' ')" >&2
  exit 1
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
    "$@"
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
