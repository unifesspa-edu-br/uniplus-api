#!/usr/bin/env bash
set -euo pipefail

ROOT="${1:-.}"

if [[ ! -d "$ROOT" ]]; then
  echo "ERRO: diretorio $ROOT nao encontrado" >&2
  exit 2
fi

CHECKS=(
  'project-reference~<ProjectReference[^>]+Unifesspa\.UniPlus\.~ProjectReference para codigo UniPlus'
  'package-reference~<PackageReference[^>]+Unifesspa\.UniPlus\.~PackageReference para pacote UniPlus'
  'using-uniplus~^[[:space:]]*using[[:space:]]+Unifesspa\.UniPlus\.~using para namespace UniPlus'
  'namespace-uniplus~^[[:space:]]*namespace[[:space:]]+Unifesspa\.UniPlus\.~namespace de codigo UniPlus'
  'lockfile-uniplus~"Unifesspa\.UniPlus\.~dependencia UniPlus em packages.lock.json'
)

EXIT_CODE=0
TOTAL_HITS=0

for CHECK in "${CHECKS[@]}"; do
  IFS='~' read -r NAME PATTERN REASON EXTRA <<< "$CHECK"

  if [[ -z "$NAME" || -z "$PATTERN" || -z "$REASON" || -n "$EXTRA" ]]; then
    echo "ERRO: entrada CHECKS malformada:" >&2
    echo "  $CHECK" >&2
    exit 2
  fi

  # -i: IDs de pacote NuGet são normalizados em minúsculas no packages.lock.json
  # (ex.: "unifesspa.uniplus.governance.contracts"); sem case-insensitive uma
  # dependência UniPlus em minúsculas passaria batida pelo gate de independência.
  HITS=$(grep -rniE "$PATTERN" "$ROOT" \
    --include='*.cs' \
    --include='*.csproj' \
    --include='*.props' \
    --include='*.targets' \
    --include='packages.lock.json' \
    --exclude-dir=bin \
    --exclude-dir=obj \
    --exclude-dir=.git \
    --exclude-dir=docs \
    --exclude-dir=openspec \
    2>/dev/null || true)

  if [[ -n "$HITS" ]]; then
    COUNT=$(printf '%s\n' "$HITS" | wc -l)
    TOTAL_HITS=$((TOTAL_HITS + COUNT))
    EXIT_CODE=1
    echo "FAIL $NAME ($COUNT ocorrencia(s))"
    echo "  Motivo: $REASON"
    echo "$HITS" | sed 's/^/    /'
    echo
  else
    echo "ok   $NAME"
  fi
done

if [[ $EXIT_CODE -ne 0 ]]; then
  echo
  echo "Falhou: $TOTAL_HITS ocorrencia(s) UniPlus ativa(s) encontrada(s)."
  echo "O repo Geo dedicado deve assumir codigo proprio e nao depender de Unifesspa.UniPlus.*."
fi

exit $EXIT_CODE
