#!/usr/bin/env bash
#
# Exercita a lógica embutida em .github/workflows/bump-infra-tag.yml sem
# precisar publicar uma release.
#
# Os dois trechos sob teste — o editor Python do values.yaml e a seleção da
# maior versão publicada — são EXTRAÍDOS do workflow, não reescritos aqui:
# uma cópia divergiria em silêncio e o teste passaria a validar a si mesmo.
#
# Cobre o que uniplus-api#1302 acrescentou (convergência monotônica: nunca
# rebaixar o values.yaml) e o que já existia antes (guards de drift
# estrutural), para que uma edição futura no workflow não os desfaça.

set -uo pipefail

RAIZ="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
WORKFLOW="$RAIZ/.github/workflows/bump-infra-tag.yml"
FIXTURE="$RAIZ/tools/bump-infra-tag/values-fixture.yaml"
TRABALHO="$(mktemp -d)"
trap 'rm -rf "$TRABALHO"' EXIT

falhas=0

# --- extração dos trechos sob teste -----------------------------------------

python3 - "$WORKFLOW" "$TRABALHO" <<'PYEOF'
import pathlib
import re
import sys

workflow = pathlib.Path(sys.argv[1]).read_text(encoding="utf-8")
destino = pathlib.Path(sys.argv[2])

editor = re.search(r"python3 - <<'PYEOF'\n(.*?)\n\s*PYEOF", workflow, re.S)
if editor is None:
    sys.exit("Bloco python do editor de tag não encontrado no workflow.")
linhas = [
    linha[10:] if linha.startswith(" " * 10) else linha
    for linha in editor.group(1).split("\n")
]
(destino / "editor.py").write_text("\n".join(linhas) + "\n", encoding="utf-8")

selecao = re.search(r"^\s*(TAG=\$\(printf.*?\|\| true)$", workflow, re.S | re.M)
if selecao is None:
    sys.exit("Trecho de seleção da maior versão não encontrado no workflow.")
trecho = "\n".join(
    linha[10:] if linha.startswith(" " * 10) else linha
    for linha in selecao.group(1).split("\n")
)
(destino / "selecao.sh").write_text(
    "set -uo pipefail\n" + trecho + '\nprintf "%s" "$TAG"\n', encoding="utf-8"
)
PYEOF

if [ ! -f "$TRABALHO/editor.py" ] || [ ! -f "$TRABALHO/selecao.sh" ]; then
  echo "✗ Falha ao extrair os trechos do workflow — o teste não validaria nada."
  exit 1
fi

# --- editor do values.yaml ---------------------------------------------------

# edita $1=tag inicial, $2=NOVA_TAG, $3=nome do caso, $4=tag final esperada
# (ou ERRO), $5=mutação opcional aplicada à fixture antes de rodar.
edita() {
  local tag_inicial="$1" nova_tag="$2" nome="$3" esperado="$4" mutacao="${5:-}"
  local caso; caso="$(mktemp -d -p "$TRABALHO")"
  local alvo="$caso/environments/hml-standalone-single/values.yaml"

  mkdir -p "$(dirname "$alvo")"
  sed "s|^    tag: v0\.9\.1$|    tag: $tag_inicial|" "$FIXTURE" > "$alvo"
  if [ -n "$mutacao" ]; then
    sed -i "$mutacao" "$alvo"
  fi

  local saida rc obtido
  saida="$(cd "$caso" && NOVA_TAG="$nova_tag" python3 "$TRABALHO/editor.py" 2>&1)"
  rc=$?

  if [ $rc -ne 0 ]; then
    obtido="ERRO"
  else
    obtido="$(awk '/^uniplusApiHost:/{dentro=1} /^[^ #]/&&!/^uniplusApiHost:/{dentro=0} dentro&&/^    tag: /{print $2; exit}' "$alvo")"
  fi

  if [ "$obtido" = "$esperado" ]; then
    printf '  ✓ %-44s %s\n' "$nome" "$obtido"
  else
    printf '  ✗ %-44s esperado %s, obtido %s\n      %s\n' \
      "$nome" "$esperado" "$obtido" "$saida"
    falhas=$((falhas + 1))
  fi
}

echo "Editor de uniplusApiHost.image.tag:"
edita v0.9.0  v0.9.1  "avança para a versão publicada"        v0.9.1
edita v0.9.9  v0.10.0 "minor supera patch"                    v0.10.0
edita v0.9.1  v0.9.1  "já em dia, não reescreve"              v0.9.1
edita v0.9.1  v0.8.0  "rerun antigo não rebaixa"              v0.9.1
edita v0.10.0 v0.9.1  "values à frente permanece"             v0.10.0
edita v1.2.3  v01.2.3 "zero à esquerda não é versão nova"     v1.2.3
edita v0.9.0  v0.9.1  "drift: duas linhas de tag no bloco"    ERRO \
  '0,/^    tag: v0.9.0$/s//    tag: v0.9.0\n    tag: v0.9.0/'
edita v0.9.0  v0.9.1  "drift: tag fora de image:"             ERRO \
  '0,/^  image:$/s//  imagem:/'
edita v0.9.0  v0.9.1  "drift: bloco uniplusApiHost ausente"   ERRO \
  's/^uniplusApiHost:$/uniplusApiHostX:/'

# O bloco vizinho tem a mesma forma `image:` / `tag:` e não pode ser tocado.
caso="$(mktemp -d -p "$TRABALHO")"
mkdir -p "$caso/environments/hml-standalone-single"
cp "$FIXTURE" "$caso/environments/hml-standalone-single/values.yaml"
(cd "$caso" && NOVA_TAG=v0.9.2 python3 "$TRABALHO/editor.py" > /dev/null 2>&1)
vizinho="$(grep -c '^    tag: v0\.4\.2$' "$caso/environments/hml-standalone-single/values.yaml")"
if [ "$vizinho" = "1" ]; then
  printf '  ✓ %-44s %s\n' "não toca a tag do bloco vizinho" "v0.4.2"
else
  printf '  ✗ %-44s a tag de uniplusGeoApi foi alterada\n' "não toca a tag do bloco vizinho"
  falhas=$((falhas + 1))
fi

# --- seleção da maior versão publicada ---------------------------------------

# seleciona $1=nome, $2=esperado, $3=TAG_DO_RUN, $4..=linhas de RUNS
seleciona() {
  local nome="$1" esperado="$2" tag_do_run="$3"; shift 3
  local runs; runs="$(printf '%s\n' "$@")"
  local obtido
  obtido="$(RUNS="$runs" TAG_DO_RUN="$tag_do_run" bash "$TRABALHO/selecao.sh")"

  if [ "$obtido" = "$esperado" ]; then
    printf '  ✓ %-44s %s\n' "$nome" "${obtido:-<vazio>}"
  else
    printf '  ✗ %-44s esperado %s, obtido %s\n' \
      "$nome" "${esperado:-<vazio>}" "${obtido:-<vazio>}"
    falhas=$((falhas + 1))
  fi
}

echo
echo "Seleção da maior versão publicada:"
seleciona "patch de dois dígitos"            v0.3.10 v0.3.9  v0.3.9 v0.3.10 v0.3.8
seleciona "minor supera patch"               v0.10.0 v0.9.9  v0.9.9 v0.10.0
seleciona "ignora a ordem cronológica"       v0.9.1  v0.7.0  v0.9.1 v0.8.0 v0.7.0
seleciona "descarta pré-release"             v0.9.1  v0.9.1  v0.9.1 v1.0.0-rc1 v2.0.0-beta
seleciona "descarta nome de branch e lixo"   v0.9.1  v0.9.1  v0.9.1 main 'v1.0; rm -rf /'
seleciona "tag do disparo entra no conjunto" v0.9.2  v0.9.2  v0.9.1 v0.9.0
seleciona "sem candidata devolve vazio"      ""      main    dependabot/foo ''

echo
if [ "$falhas" -eq 0 ]; then
  echo "✓ Todos os casos passaram."
else
  echo "✗ $falhas caso(s) falharam."
  exit 1
fi
