#!/usr/bin/env bash
# tools/tamanho-de-arquivo/check.sh — guarda do limite de linhas por arquivo
# de código de produção do Uni+.
#
# Regra: nenhum *.cs de src/ passa de 400 linhas físicas (comentários,
# documentação XML e linhas em branco contam; a última linha conta mesmo sem
# quebra no fim, como em `wc -l` corrigido). Ficam fora: Migrations/, obj/,
# bin/, *.g.cs, *.Designer.cs, *ModelSnapshot.cs e todo o diretório tests/.
#
# Arquivos que ainda excedem o limite constam em
# tools/tamanho-de-arquivo/excecoes.txt ("<linhas> <caminho>"). A lista só
# diminui.
#
# Uso:
#   bash tools/tamanho-de-arquivo/check.sh              # verifica
#   bash tools/tamanho-de-arquivo/check.sh --atualizar  # só reduz a lista
#
# Sai com código 1 se houver violação; 2 em erro de uso.

set -euo pipefail

LIMITE=400
RAIZ="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
LISTA_REL="tools/tamanho-de-arquivo/excecoes.txt"
LISTA="$RAIZ/$LISTA_REL"

ATUALIZAR=0
case "${1:-}" in
  "") ;;
  --atualizar) ATUALIZAR=1 ;;
  *)
    echo "ERRO: argumento desconhecido '$1' (use --atualizar ou nenhum)" >&2
    exit 2
    ;;
esac

if [[ ! -d "$RAIZ/src" ]]; then
  echo "ERRO: diretório src/ não encontrado em $RAIZ" >&2
  exit 2
fi
if [[ ! -f "$LISTA" ]]; then
  echo "ERRO: lista de exceções $LISTA_REL não encontrada" >&2
  exit 2
fi

cd "$RAIZ"

# Contagem de linhas físicas por arquivo do escopo.
declare -A ATUAL=()
TOTAL=0
while IFS= read -r LINHA; do
  ARQ="${LINHA%:*}"
  ATUAL["$ARQ"]="${LINHA##*:}"
  TOTAL=$((TOTAL + 1))
done < <(
  find src \
    \( -type d \( -name Migrations -o -name obj -o -name bin \) -prune \) -o \
    \( -type f -name '*.cs' ! -name '*.g.cs' ! -name '*.Designer.cs' ! -name '*ModelSnapshot.cs' -print0 \) \
    | LC_ALL=C sort -z \
    | xargs -0 -r grep -c -H ''
)

# Lista de exceções registrada.
declare -A REGISTRADO=()
CABECALHO=()
FALHAS_LISTA=()
while IFS= read -r LINHA || [[ -n "$LINHA" ]]; do
  if [[ -z "$LINHA" || "$LINHA" == \#* ]]; then
    [[ ${#REGISTRADO[@]} -eq 0 ]] && CABECALHO+=("$LINHA")
    continue
  fi
  if [[ ! "$LINHA" =~ ^([0-9]+)\ (.+)$ ]]; then
    FALHAS_LISTA+=("linha malformada em $LISTA_REL: $LINHA")
    continue
  fi
  N="${BASH_REMATCH[1]}"
  CAMINHO="${BASH_REMATCH[2]}"
  if [[ -n "${REGISTRADO[$CAMINHO]:-}" ]]; then
    FALHAS_LISTA+=("entrada duplicada em $LISTA_REL: $CAMINHO")
    continue
  fi
  REGISTRADO["$CAMINHO"]="$N"
done < "$LISTA"

NOVOS=()
CRESCERAM=()
SAIRAM=()
ENCOLHERAM=()

while IFS= read -r ARQ; do
  N="${ATUAL[$ARQ]}"
  (( N > LIMITE )) || continue
  if [[ -z "${REGISTRADO[$ARQ]:-}" ]]; then
    NOVOS+=("$ARQ ($N linhas)")
  elif (( N > REGISTRADO[$ARQ] )); then
    CRESCERAM+=("$ARQ (registrado ${REGISTRADO[$ARQ]}, agora $N)")
  elif (( N < REGISTRADO[$ARQ] )); then
    ENCOLHERAM+=("$ARQ")
  fi
done < <(printf '%s\n' "${!ATUAL[@]}" | LC_ALL=C sort)

while IFS= read -r ARQ; do
  [[ -n "$ARQ" ]] || continue
  if [[ -z "${ATUAL[$ARQ]:-}" ]]; then
    SAIRAM+=("$ARQ (não existe mais ou saiu do escopo)")
  elif (( ATUAL[$ARQ] <= LIMITE )); then
    SAIRAM+=("$ARQ (agora ${ATUAL[$ARQ]} linhas)")
  fi
done < <(printf '%s\n' "${!REGISTRADO[@]}" | LC_ALL=C sort)

listar() {
  local TITULO="$1"
  shift
  echo "FAIL $TITULO"
  printf '    %s\n' "$@"
  echo
}

if (( ATUALIZAR )); then
  if (( ${#NOVOS[@]} + ${#CRESCERAM[@]} + ${#FALHAS_LISTA[@]} > 0 )); then
    echo "ERRO: --atualizar só reduz a lista; não acrescenta arquivo nem aumenta número." >&2
    (( ${#NOVOS[@]} )) && listar "arquivo fora da lista acima de $LIMITE linhas (divida o arquivo):" "${NOVOS[@]}" >&2
    (( ${#CRESCERAM[@]} )) && listar "arquivo da lista que cresceu (reduza o arquivo):" "${CRESCERAM[@]}" >&2
    (( ${#FALHAS_LISTA[@]} )) && listar "problemas na lista:" "${FALHAS_LISTA[@]}" >&2
    exit 1
  fi
  SAIDA="$(mktemp)"
  trap 'rm -f "$SAIDA"' EXIT
  {
    (( ${#CABECALHO[@]} )) && printf '%s\n' "${CABECALHO[@]}"
    for ARQ in "${!REGISTRADO[@]}"; do
      if [[ -n "${ATUAL[$ARQ]:-}" ]] && (( ATUAL[$ARQ] > LIMITE )); then
        echo "${ATUAL[$ARQ]} $ARQ"
      fi
    done | LC_ALL=C sort -k2
  } > "$SAIDA"
  cat "$SAIDA" > "$LISTA"
  echo "ok   lista atualizada: ${#SAIRAM[@]} saída(s), ${#ENCOLHERAM[@]} redução(ões)"
  exit 0
fi

EXIT_CODE=0
if (( ${#NOVOS[@]} )); then
  EXIT_CODE=1
  listar "arquivo fora da lista passa de $LIMITE linhas (divida o arquivo):" "${NOVOS[@]}"
fi
if (( ${#CRESCERAM[@]} )); then
  EXIT_CODE=1
  listar "arquivo da lista cresceu além do registrado (reduza o arquivo):" "${CRESCERAM[@]}"
fi
if (( ${#SAIRAM[@]} )); then
  EXIT_CODE=1
  listar "arquivo da lista com $LIMITE linhas ou menos: tire-o de $LISTA_REL (ou rode com --atualizar):" "${SAIRAM[@]}"
fi
if (( ${#FALHAS_LISTA[@]} )); then
  EXIT_CODE=1
  listar "problemas na lista de exceções:" "${FALHAS_LISTA[@]}"
fi

if (( EXIT_CODE )); then
  echo "Falhou: limite de $LIMITE linhas por arquivo de produção não respeitado."
  exit 1
fi

NA_LISTA=${#REGISTRADO[@]}
echo "ok   $((TOTAL - NA_LISTA)) arquivo(s) dentro do limite de $LIMITE linhas; $NA_LISTA na lista de exceções ($LISTA_REL)"
if (( ${#ENCOLHERAM[@]} )); then
  echo "aviso ${#ENCOLHERAM[@]} arquivo(s) da lista encolheram; rode 'bash tools/tamanho-de-arquivo/check.sh --atualizar' para registrar"
fi
