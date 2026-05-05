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

echo "$COUNT ADR(s) validados sem erros (frontmatter MADR 4.0)."

# Markdownlint-cli2 — formatação do markdown (MD032 listas, MD024 siblings,
# MD041 H1 etc.) regida pelo .markdownlint-cli2.jsonc do diretório de ADRs.
# CI roda esta mesma versão; rodar localmente garante paridade dev↔CI.
#
# Pinar via @VERSION evita drift entre ambientes — bump exige edição
# coordenada deste script + .github/workflows/ci.yml. Sem npx no PATH,
# emite aviso em vez de falhar (devs sem Node ainda obtêm validação MADR 4.0
# via primeira parte deste script; o gate completo fica para o CI).
MARKDOWNLINT_VERSION="0.22.1"

if command -v npx >/dev/null 2>&1; then
  echo
  echo "Rodando markdownlint-cli2@${MARKDOWNLINT_VERSION} em $DIR/**/*.md ..."

  # Captura combinada de stdout+stderr para diagnosticar a natureza do
  # exit code != 0 abaixo: violação de lint vs falha de fetch (registry,
  # auth, network). O `tee` espelha a saída no terminal para o operador
  # ver progresso enquanto o script processa o resultado.
  MD_OUTPUT_FILE=$(mktemp)
  trap 'rm -f "$MD_OUTPUT_FILE"' EXIT

  set +e
  npx --yes "markdownlint-cli2@${MARKDOWNLINT_VERSION}" "$DIR/**/*.md" 2>&1 \
    | tee "$MD_OUTPUT_FILE"
  MD_EXIT="${PIPESTATUS[0]}"
  set -e

  if [[ "$MD_EXIT" -eq 0 ]]; then
    echo "markdownlint-cli2: 0 erros."
  elif grep -qE 'markdownlint-cli2 v[0-9]' "$MD_OUTPUT_FILE"; then
    # Banner do binary apareceu → ferramenta rodou; exit != 0 = lint real.
    echo >&2
    echo "ERRO: markdownlint-cli2 reportou violações de formatação." >&2
    echo "Config: $DIR/.markdownlint-cli2.jsonc" >&2
    exit 1
  else
    # Banner ausente → npx falhou ANTES de invocar o binary
    # (registry HTTP error, auth, network, integrity). Não é violação de
    # contrato local; emitir aviso para o operador, sem falhar — o gate de
    # CI ainda protege a main.
    echo >&2
    echo "AVISO: npx não conseguiu rodar markdownlint-cli2@${MARKDOWNLINT_VERSION} (exit $MD_EXIT)." >&2
    echo "       Provável falha de fetch (registry/auth/network), não violação de lint." >&2
    echo "       O gate de CI ainda valida; tente novamente quando a rede normalizar:" >&2
    echo "         npx --yes markdownlint-cli2@${MARKDOWNLINT_VERSION} '$DIR/**/*.md'" >&2
  fi
else
  echo
  echo "AVISO: npx não encontrado — pulando markdownlint-cli2 local." >&2
  echo "       O CI ainda roda este gate; instale Node.js para validação dev↔CI paritária." >&2
fi
