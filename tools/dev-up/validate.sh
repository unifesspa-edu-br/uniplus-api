#!/usr/bin/env bash
#
# Teste de fumaça do ambiente local (docker/docker-compose.yml + .env.example) e dos scripts que o serviço
# ambiente-init executa, sem subir o ambiente.
#
# O percurso completo (imagens, clamd, Keycloak, instalação dos gestores) leva vários minutos e baixa a base de
# assinaturas do clamd; no CI o custo e a dependência de rede externa não se justificam, e o percurso é validado
# à mão a cada mudança aqui (ver o guia do ambiente local). Esta conferência pega o que quebra em silêncio: o
# .env.example não bastar para o compose, o compose perder a API ou o ambiente-init, chave obrigatória faltando
# sem mensagem e o domínio de desenvolvimento aparecer na configuração da API.

set -uo pipefail

RAIZ="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
TRABALHO="$(mktemp -d)"
trap 'rm -rf "$TRABALHO"' EXIT
ARQUIVO_DE_FALHAS="$TRABALHO/falhas"
: > "$ARQUIVO_DE_FALHAS"

ok()   { echo "  ok   $1"; }
erro() { echo "  FALHA $1"; echo x >> "$ARQUIVO_DE_FALHAS"; }

confere() {
    if [ "$2" = "$3" ]; then ok "$1"; else erro "$1 (esperado: '$2'; obtido: '$3')"; fi
}

echo "== shellcheck"
if command -v shellcheck >/dev/null; then
    if shellcheck -x -P SCRIPTDIR "$RAIZ/scripts/ambiente-init.sh" "$RAIZ/scripts/dev-instalar-gestores.sh" \
        "$RAIZ/scripts/diagnosticar-ambiente.sh" "$RAIZ/scripts/setup-keycloak-dev.sh" "$RAIZ/scripts/setup-govbr-mock.sh"; then
        ok "scripts sem aviso do shellcheck"
    else
        erro "shellcheck acusou aviso"
    fi
else
    echo "  (shellcheck ausente; etapa ignorada)"
fi

echo "== .env.example reúne as variáveis do compose"
COMPOSES=("$RAIZ/docker/docker-compose.yml" "$RAIZ/docker/docker-compose.override.example.yml")
usadas=$(grep -ohE '\$\{[A-Za-z_][A-Za-z0-9_]*' "${COMPOSES[@]}" | sed -E 's/^\$\{//' | sort -u)
definidas=$(grep -E '^[A-Za-z_][A-Za-z0-9_]*=' "$RAIZ/docker/.env.example" | cut -d= -f1 | sort -u)
confere "toda variável que os compose usam está no .env.example" "" "$(comm -23 <(echo "$usadas") <(echo "$definidas"))"

echo "== compose"
if command -v docker >/dev/null && docker compose version >/dev/null 2>&1; then
    cp "$RAIZ/docker/.env.example" "$TRABALHO/env.dev"
    compor() { docker compose --env-file "$TRABALHO/env.dev" -f "$RAIZ/docker/docker-compose.yml" "$@"; }
    if compor config -q 2>"$TRABALHO/erro"; then
        ok "o .env.example basta para o docker-compose.yml"
    else
        erro "docker compose recusou o .env.example: $(head -c 300 "$TRABALHO/erro")"
    fi
    confere "o compose traz a API, o Geo, o verificador e o ambiente-init" "true" \
        "$(compor config --format json | jq -r '.services | has("uniplus-api") and has("geo-api") and has("verificador") and has("clamd-verificador") and has("ambiente-init")')"
    if docker compose --env-file "$TRABALHO/env.dev" -f "$RAIZ/docker/docker-compose.yml" \
        -f "$RAIZ/docker/docker-compose.override.example.yml" config -q 2>"$TRABALHO/erro"; then
        ok "o override de exemplo (stack completo) é válido"
    else
        erro "docker compose recusou o override de exemplo: $(head -c 300 "$TRABALHO/erro")"
    fi
    # .env antigo, sem POSTGRES_USER: o compose recusa e nomeia a chave, antes de subir qualquer serviço.
    grep -v '^POSTGRES_USER=' "$RAIZ/docker/.env.example" > "$TRABALHO/env.antigo"
    saida=$(docker compose --env-file "$TRABALHO/env.antigo" -f "$RAIZ/docker/docker-compose.yml" config -q 2>&1); status=$?
    confere ".env sem chave obrigatória: o compose recusa" "1" "$status"
    case "$saida" in
        *POSTGRES_USER*) ok ".env sem chave obrigatória: a mensagem nomeia a chave" ;;
        *) erro "a mensagem não nomeia POSTGRES_USER: $saida" ;;
    esac
else
    echo "  (docker compose ausente; etapa ignorada)"
fi

echo "== diagnóstico do ambiente"
# O diagnóstico precisa aceitar o override de exemplo do stack completo e recusar o override antigo, que
# definia a API por inteiro.
DIAG="$TRABALHO/diag"
mkdir -p "$DIAG/scripts" "$DIAG/docker"
cp "$RAIZ/scripts/diagnosticar-ambiente.sh" "$DIAG/scripts/"
cp "$RAIZ"/docker/docker-compose.yml "$RAIZ"/docker/docker-compose.override.example.yml "$RAIZ/docker/.env.example" "$DIAG/docker/"
cp "$DIAG/docker/.env.example" "$DIAG/docker/.env"
cp "$DIAG/docker/docker-compose.override.example.yml" "$DIAG/docker/docker-compose.override.yml"
ANTIGO="redefine serviço que agora vive"
"$DIAG/scripts/diagnosticar-ambiente.sh" >"$TRABALHO/diag.log" 2>&1
if grep -q "$ANTIGO" "$TRABALHO/diag.log"; then
    erro "o diagnóstico recusou o override de exemplo"
else
    ok "o diagnóstico aceita o override de exemplo"
fi
printf 'services:\n  uniplus-api:\n    build:\n      context: ..\n' > "$DIAG/docker/docker-compose.override.yml"
"$DIAG/scripts/diagnosticar-ambiente.sh" >"$TRABALHO/diag.log" 2>&1
if grep -q "$ANTIGO" "$TRABALHO/diag.log"; then
    ok "o diagnóstico acusa o override antigo"
else
    erro "o diagnóstico não acusou o override antigo"
fi

echo "== domínio de desenvolvimento fora de HML e produção"
# Os domínios locais só existem no compose de desenvolvimento (docker/, nunca implantado: HML e produção usam
# o chart do uniplus-infra). Nenhuma configuração do código os traz.
vazamentos=$(grep -rlE 'teste\.unifesspa\.edu\.br|uniplus-test\.local' "$RAIZ/src" 2>/dev/null | grep -E 'appsettings' || true)
confere "nenhum appsettings traz o domínio local" "" "$vazamentos"

echo
falhas=$(wc -l < "$ARQUIVO_DE_FALHAS")
if [ "$falhas" -gt 0 ]; then
    echo "$falhas falha(s)."
    exit 1
fi
echo "Todas as conferências passaram."
