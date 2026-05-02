#!/usr/bin/env bash
# tools/forbidden-deps/check.sh — guarda contra reintrodução de dependências
# de teste vetadas pelo projeto Uni+.
#
# Uso:
#   bash tools/forbidden-deps/check.sh           # varre o repo a partir de .
#   bash tools/forbidden-deps/check.sh DIR       # varre a partir de DIR
#
# Sai com código != 0 se encontrar pelo menos uma ocorrência ativa.

set -euo pipefail

ROOT="${1:-.}"

if [[ ! -d "$ROOT" ]]; then
  echo "ERRO: diretório $ROOT não encontrado" >&2
  exit 2
fi

# Pacotes proibidos. Cada entrada usa '~' como separador (escolhido por não
# colidir com sintaxe de regex nem com nomes de pacote .NET), com 5 campos:
#   nome ~ regex_para_cs ~ regex_para_csproj_e_props ~ motivo ~ ADR
#
# Os regex são propositalmente específicos (forma efetiva de uso) para evitar
# falso positivo em comentários, strings e nomes parecidos. Para adicionar nova
# proibição, acrescente uma linha aqui e (se relevante) crie/atualize a ADR.
FORBIDDEN=(
  'FluentAssertions~^[[:space:]]*using[[:space:]]+(static[[:space:]]+)?FluentAssertions[[:space:];.]~Include="FluentAssertions"~biblioteca de assertions Xceed comercial paga (v8+); usar AwesomeAssertions~ADR-0021'
)

EXIT_CODE=0
TOTAL_HITS=0

for ENTRY in "${FORBIDDEN[@]}"; do
  IFS='~' read -r NAME CS_PATTERN PROJ_PATTERN REASON ADR <<< "$ENTRY"

  # Busca em duas frentes:
  #   - .cs                : `using` (incluindo `using static`) que importe o namespace.
  #   - .csproj / .props   : referência efetiva ao pacote (Central Package Management ou direta).
  # Exclui docs/ (ADRs e guias citam o nome legitimamente como contexto histórico)
  # e diretórios gerados (bin/, obj/, .git/).
  HITS_CS=$(grep -rnE "$CS_PATTERN" "$ROOT" \
    --include='*.cs' \
    --exclude-dir=docs \
    --exclude-dir=bin \
    --exclude-dir=obj \
    --exclude-dir=.git \
    2>/dev/null || true)

  HITS_PROJ=$(grep -rnE "$PROJ_PATTERN" "$ROOT" \
    --include='*.csproj' \
    --include='*.props' \
    --exclude-dir=docs \
    --exclude-dir=bin \
    --exclude-dir=obj \
    --exclude-dir=.git \
    2>/dev/null || true)

  HITS=$(printf '%s\n%s\n' "$HITS_CS" "$HITS_PROJ" | sed '/^$/d')

  if [[ -n "$HITS" ]]; then
    COUNT=$(echo "$HITS" | wc -l)
    TOTAL_HITS=$((TOTAL_HITS + COUNT))
    EXIT_CODE=1
    echo "FAIL $NAME ($COUNT ocorrência(s))"
    echo "  Motivo: $REASON"
    echo "  Referência: $ADR"
    echo "  Ocorrências:"
    echo "$HITS" | sed 's/^/    /'
    echo
  else
    echo "ok   $NAME — sem ocorrências ativas (referência: $ADR)"
  fi
done

if [[ $EXIT_CODE -ne 0 ]]; then
  echo
  echo "Falhou: $TOTAL_HITS ocorrência(s) de pacote(s) proibido(s) encontrada(s)."
  echo "Remova as referências ou justifique no PR. Veja a ADR citada para o contexto."
fi

exit $EXIT_CODE
