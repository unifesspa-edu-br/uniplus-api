#!/usr/bin/env bash
#
# Confere que a base local pronta (docker/base-local) continua coerente com o código, sem subir o ambiente e sem restaurar banco:
#   1. a migration do código é a mesma gravada no dump do banco `uniplus`; se a migration for gerada de novo e o dump não, a API
#      sobe sobre um banco que tem a migration antiga, tenta aplicar a nova sobre tabelas existentes e o `docker compose up` quebra;
#   2. cada variável que o restaurador exige (valores gravados nos dumps) existe no docker/.env.example com o mesmo valor padrão.
# A lista de variáveis é lida do próprio restaurador, sem segunda cópia aqui.
#
# O dump é lido com `pg_restore -f -` (só imprime o conteúdo, não restaura): usa o do PATH se for da versão 18 ou superior, que é a
# do dump, e senão o da imagem postgres:18-alpine. Variáveis para teste: DUMP, RESTAURADOR, ENV_EXAMPLE e DIRETORIO_DAS_MIGRATIONS.

set -uo pipefail

RAIZ="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
DUMP="${DUMP:-$RAIZ/docker/base-local/dumps/uniplus.dump}"
RESTAURADOR="${RESTAURADOR:-$RAIZ/docker/base-local/restaura-base-local.sh}"
ENV_EXAMPLE="${ENV_EXAMPLE:-$RAIZ/docker/.env.example}"
DIRETORIO_DAS_MIGRATIONS="${DIRETORIO_DAS_MIGRATIONS:-$RAIZ/src/host/Unifesspa.UniPlus.Monolito.Persistencia/Migrations}"
IMAGEM_DO_PG="${IMAGEM_DO_PG:-postgres:18-alpine}"
falhas=0

ok()   { echo "  ok   $1"; }
erro() { echo "  FALHA $1"; falhas=$((falhas + 1)); }

# Escreve o conteúdo do dump em $1 e devolve o código de saída do pg_restore: leitura incompleta não pode passar por válida.
conteudo_do_dump() {
    local versao
    versao=$(pg_restore --version 2>/dev/null | grep -oE '[0-9]+' | head -1)
    if [ "${versao:-0}" -ge 18 ]; then
        pg_restore -f - "$DUMP" > "$1"
    else
        docker run --rm -i "$IMAGEM_DO_PG" pg_restore -f - < "$DUMP" > "$1"
    fi
}

migrations_do_codigo() {
    # O identificador que o EF registra é o do atributo [Migration("..."), não o nome do arquivo.
    find "$DIRETORIO_DAS_MIGRATIONS" -maxdepth 1 -name '*.Designer.cs' -exec grep -hoE '\[Migration\("[^"]+"\)\]' {} + \
        | sed -E 's/^\[Migration\("([^"]+)"\)\]$/\1/' | sort
}

migrations_do_dump() {
    awk '
        /^COPY plataforma\."__EFMigrationsHistory" / { dentro = 1; next }
        dentro && /^\\\.$/ { dentro = 0 }
        dentro { split($0, campos, "\t"); print campos[1] }' "$1" | sort
}

echo "== migration do código x dump"
do_codigo=$(migrations_do_codigo)
conteudo="$(mktemp)"; trap 'rm -f "$conteudo"' EXIT
if ! conteudo_do_dump "$conteudo"; then
    erro "não consegui ler o dump $DUMP com o pg_restore (dump corrompido, versão incompatível ou Docker indisponível)."
    do_dump=""
else
    do_dump=$(migrations_do_dump "$conteudo")
fi
if [ -z "$do_codigo" ] || [ -z "$do_dump" ]; then
    erro "não consegui ler as migrations (código: '${do_codigo:-nenhuma}'; dump: '${do_dump:-nenhuma}'). Confira o caminho das migrations e o dump $DUMP."
elif [ "$do_codigo" = "$do_dump" ]; then
    ok "o código e o dump têm a mesma migration ($(echo "$do_codigo" | tr '\n' ' '))"
else
    erro "a migration do código ($(echo "$do_codigo" | tr '\n' ' ')) é diferente da gravada no dump ($(echo "$do_dump" | tr '\n' ' ')). Gere os dumps de novo (docker/base-local/dumps) com a migration atual, como descrito em docs/setup-ambiente-local.md."
fi

echo "== variáveis exigidas pelo restaurador x .env.example"
# O array é avaliado pelo próprio Bash (aspas, comentários e quebras de linha como no restaurador), não lido por expressão regular.
bloco=$(awk '/^valores_exigidos=\(/{d=1} d{print} d&&/\)[[:space:]]*$/{exit}' "$RESTAURADOR")
exigidas=$(bash -c "$bloco"$'\n''printf "%s\n" "${valores_exigidos[@]}"' 2>/dev/null)
if [ -z "$exigidas" ]; then
    erro "não achei a lista valores_exigidos em $RESTAURADOR"
fi
while IFS= read -r exigido; do
    [ -n "$exigido" ] || continue
    nome="${exigido%%=*}"; esperado="${exigido#*=}"
    obtido=$(grep -E "^$nome=" "$ENV_EXAMPLE" | head -1 | cut -d= -f2-)
    if [ -z "$obtido" ] && ! grep -qE "^$nome=" "$ENV_EXAMPLE"; then
        erro "a variável $nome, exigida pelo restaurador, não existe no docker/.env.example"
    elif [ "$obtido" != "$esperado" ]; then
        erro "$nome: o restaurador exige '$esperado' e o docker/.env.example tem '$obtido'. Atualize a lista do restaurador ou gere os dumps de novo."
    else
        ok "$nome=$esperado"
    fi
done <<< "$exigidas"

if [ "$falhas" -gt 0 ]; then echo "$falhas falha(s) na base local pronta."; exit 1; fi
echo "Base local pronta coerente com o código."
