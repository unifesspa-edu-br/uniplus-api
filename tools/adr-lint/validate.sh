#!/usr/bin/env bash
# tools/adr-lint/validate.sh — validador MADR 4.0 para ADRs do projeto Uni+.
#
# Uso:
#   bash tools/adr-lint/validate.sh           # valida ./docs/adrs
#   bash tools/adr-lint/validate.sh DIR       # valida DIR
#
# Sai com código != 0 se houver violações.

set -euo pipefail

DIR="${1:-docs/adrs}"
ERRORS=0
COUNT=0

if [[ ! -d "$DIR" ]]; then
  echo "ERRO: diretório $DIR não encontrado" >&2
  exit 2
fi

shopt -s nullglob

# Itera todo arquivo .md do diretório e exclui apenas README.md e arquivos
# começando com "_" (ex.: _template.md). Isso garante que ADRs com nomes
# fora do padrão NNNN-slug.md (ex.: foo.md, adr-001.md) sejam reportados
# como falha em vez de silenciosamente ignorados.
for FILE in "$DIR"/*.md; do
  BASE=$(basename "$FILE")
  case "$BASE" in
    README.md|_*.md) continue ;;
  esac

  COUNT=$((COUNT + 1))
  NUM_FILE="${BASE%%-*}"
  ERR=()

  if [[ ! "$BASE" =~ ^[0-9]{4}-[a-z0-9-]+\.md$ ]]; then
    ERR+=("nome de arquivo fora do padrão NNNN-titulo-em-slug.md")
  fi

  if ! head -1 "$FILE" | grep -q '^---$'; then
    ERR+=("frontmatter YAML ausente (esperado --- na linha 1)")
  elif ! tail -n +2 "$FILE" | head -n 50 | grep -q '^---$'; then
    ERR+=("frontmatter YAML sem delimitador de fechamento (esperado --- após bloco YAML)")
  fi

  FM=$(awk 'BEGIN{c=0} /^---$/{c++; if (c==2) exit; next} c==1' "$FILE" 2>/dev/null || echo "")

  for REQUIRED in "status" "date" "decision-makers"; do
    if ! printf '%s\n' "$FM" | grep -q "^${REQUIRED}:"; then
      ERR+=("frontmatter sem campo obrigatório: $REQUIRED")
    fi
  done

  STATUS=$(printf '%s\n' "$FM" | grep '^status:' | head -1 | sed -E 's/^status:[[:space:]]*"?([^"]*)"?[[:space:]]*$/\1/')
  if [[ -n "$STATUS" ]]; then
    if [[ ! "$STATUS" =~ ^(proposed|accepted|rejected|deprecated)$ ]] && \
       [[ ! "$STATUS" =~ ^superseded\ by\ ADR-[0-9]{4}$ ]]; then
      ERR+=("status inválido: '$STATUS' (esperado proposed|accepted|rejected|deprecated|superseded by ADR-NNNN)")
    fi
  fi

  DATE=$(printf '%s\n' "$FM" | grep '^date:' | head -1 | sed -E 's/^date:[[:space:]]*"?([^"]*)"?[[:space:]]*$/\1/')
  if [[ -n "$DATE" ]] && [[ ! "$DATE" =~ ^[0-9]{4}-[0-9]{2}-[0-9]{2}$ ]]; then
    ERR+=("date inválida: '$DATE' (esperado YYYY-MM-DD)")
  fi

  H1=$(grep -m1 '^# ' "$FILE" || true)
  if [[ ! "$H1" =~ ^\#\ ADR-${NUM_FILE}:\  ]]; then
    ERR+=("H1 não bate: esperado '# ADR-${NUM_FILE}: ...', encontrado '$H1'")
  fi

  COUNT_DECISION=$(grep -c '^## Resultado da decisão' "$FILE" || true)
  COUNT_DECISION=${COUNT_DECISION:-0}
  if [[ "$COUNT_DECISION" -ne 1 ]]; then
    ERR+=("'## Resultado da decisão' deve aparecer exatamente 1 vez (encontrado: $COUNT_DECISION)")
  fi

  for SECTION in "## Contexto e enunciado do problema" "## Opções consideradas" "## Resultado da decisão" "## Consequências"; do
    if ! grep -q "^${SECTION}\$" "$FILE"; then
      ERR+=("seção obrigatória ausente: '$SECTION'")
    fi
  done

  if [[ "${#ERR[@]}" -gt 0 ]]; then
    ERRORS=$((ERRORS + ${#ERR[@]}))
    echo "FAIL $BASE"
    for E in "${ERR[@]}"; do
      echo "  - $E"
    done
  else
    echo "ok   $BASE"
  fi
done

if [[ "$COUNT" -eq 0 ]]; then
  echo "AVISO: nenhum arquivo NNNN-*.md encontrado em $DIR" >&2
  exit 0
fi

echo
if [[ "$ERRORS" -gt 0 ]]; then
  echo "Total de erros: $ERRORS em $COUNT arquivo(s)."
  exit 1
fi

echo "$COUNT ADR(s) validados sem erros."
