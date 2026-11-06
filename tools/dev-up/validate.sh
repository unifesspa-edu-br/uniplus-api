#!/usr/bin/env bash
#
# Teste de fumaça dos scripts de subida do ambiente local (scripts/dev-up.sh e
# scripts/dev-instalar-gestores.sh), sem subir o ambiente.
#
# O percurso completo (imagens, clamd, Keycloak, instalação dos gestores) leva vários minutos e baixa
# a base de assinaturas do clamd; no CI o custo e a dependência de rede externa não se justificam, e o
# percurso é validado à mão a cada mudança nesses scripts (ver o guia do ambiente local). Aqui ficam os
# pontos que quebram em silêncio: conferência do .env e do override contra os exemplos, deslocamento
# das portas, coerência dos compose com a lista de portas e o domínio de desenvolvimento ficar fora do
# que chega a HML e produção.

set -uo pipefail

RAIZ="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
TRABALHO="$(mktemp -d)"
trap 'rm -rf "$TRABALHO"' EXIT

# shellcheck source=scripts/lib/dev-up-validacao.sh
source "$RAIZ/scripts/lib/dev-up-validacao.sh"

falhas=0

ok()    { echo "  ok   $1"; }
erro()  { echo "  FALHA $1"; falhas=$((falhas + 1)); }

verifica() {
    local descricao="$1"
    shift
    if "$@"; then
        ok "$descricao"
    else
        erro "$descricao"
    fi
}

confere() {
    local descricao="$1" esperado="$2" obtido="$3"
    if [ "$esperado" = "$obtido" ]; then
        ok "$descricao"
    else
        erro "$descricao (esperado: '$esperado'; obtido: '$obtido')"
    fi
}

echo "== shellcheck"
if command -v shellcheck >/dev/null; then
    if shellcheck -x -P SCRIPTDIR "$RAIZ/scripts/dev-up.sh" "$RAIZ/scripts/dev-instalar-gestores.sh" \
        "$RAIZ/scripts/setup-keycloak-dev.sh" "$RAIZ/scripts/lib/dev-up-validacao.sh"; then
        ok "scripts sem aviso do shellcheck"
    else
        erro "shellcheck acusou aviso"
    fi
else
    echo "  (shellcheck ausente; etapa ignorada)"
fi

echo "== chaves do .env"
printf 'A=1\nB=2\nC=3\n# D=4\n' > "$TRABALHO/env.exemplo"
printf 'A=1\nB=x\n' > "$TRABALHO/env.real"
confere "aponta só a chave que falta (a comentada não é exigida)" "C" "$(chaves_ausentes_do_env "$TRABALHO/env.exemplo" "$TRABALHO/env.real")"
printf 'A=1\nB=2\nC=3\n' > "$TRABALHO/env.completo"
confere ".env completo não acusa nada" "" "$(chaves_ausentes_do_env "$TRABALHO/env.exemplo" "$TRABALHO/env.completo")"

echo "== chaves do override"
cat > "$TRABALHO/override.exemplo" <<'EOF'
services:
  api:
    environment:
      ASPNETCORE_URLS: http://+:8080
      Geo__BaseUrl: "http://geo-api:8080"
      Autorizacao__Instalacao__DominiosInstitucionais__0: "unifesspa.edu.br"
EOF
cat > "$TRABALHO/override.real" <<'EOF'
services:
  api:
    environment:
      ASPNETCORE_URLS: http://+:8080
      Geo__BaseUrl: "http://geo-api:8080"
EOF
confere "aponta a variável de configuração que falta" \
    "Autorizacao__Instalacao__DominiosInstitucionais__0" \
    "$(chaves_ausentes_do_override "$TRABALHO/override.exemplo" "$TRABALHO/override.real")"
confere "override igual ao exemplo não acusa nada" "" \
    "$(chaves_ausentes_do_override "$TRABALHO/override.exemplo" "$TRABALHO/override.exemplo")"

echo "== valor do .env como o compose lê"
printf 'KEYCLOAK_PORT="18080"\r\nAPI_PORT=15200  # meu ambiente\nSENHA='"'"'x y'"'"'\n' > "$TRABALHO/env.estilos"
confere "aspas duplas e CR" "18080" "$(unset KEYCLOAK_PORT; valor_do_env "$TRABALHO/env.estilos" KEYCLOAK_PORT 8080)"
confere "comentário depois do valor" "15200" "$(unset API_PORT; valor_do_env "$TRABALHO/env.estilos" API_PORT 5200)"
confere "aspas simples" "x y" "$(unset SENHA; valor_do_env "$TRABALHO/env.estilos" SENHA)"

echo "== deslocamento de portas"
(
    unset POSTGRES_PORT KEYCLOAK_PORT API_PORT
    aplicar_deslocamento_de_portas 100
    confere "soma o deslocamento ao padrão" "5532 8180 5300" "$POSTGRES_PORT $KEYCLOAK_PORT $API_PORT"
)
(
    unset KEYCLOAK_PORT
    POSTGRES_PORT=7777
    export POSTGRES_PORT
    aplicar_deslocamento_de_portas 100
    confere "porta definida pela pessoa não é alterada" "7777 8180" "$POSTGRES_PORT $KEYCLOAK_PORT"
)
(
    aplicar_deslocamento_de_portas 60000 2>/dev/null
    confere "recusa deslocamento que passa de 65535" "2" "$?"
)
(
    unset KAFKA_PORT
    aplicar_deslocamento_de_portas 20000 2>/dev/null
    confere "recusa deslocamento que leva o Kafka a um listener interno" "3" "$?"
)
(
    aplicar_deslocamento_de_portas abc 2>/dev/null
    confere "recusa deslocamento que não é número" "1" "$?"
)

echo "== portas dos compose x lista do script"
declarado=$(portas_declaradas_nos_compose "$RAIZ/docker/docker-compose.yml" "$RAIZ/docker/docker-compose.override.example.yml")
esperado=$(printf '%s\n' "${DEV_PORTAS_PADRAO[@]}" | sort -u)
confere "toda porta parametrizada está na lista, com o mesmo padrão, e vice-versa" "$esperado" "$declarado"

echo "== compose com os exemplos"
if command -v docker >/dev/null && docker compose version >/dev/null 2>&1; then
    cp "$RAIZ/docker/.env.example" "$TRABALHO/env.dev"
    compor() {
        docker compose --env-file "$TRABALHO/env.dev" \
            -f "$RAIZ/docker/docker-compose.yml" -f "$RAIZ/docker/docker-compose.override.example.yml" "$@"
    }
    if compor config -q 2>"$TRABALHO/compose.err"; then
        ok "o .env.example e o override de exemplo bastam (config -q)"
    else
        erro "docker compose config recusou os exemplos: $(head -c 400 "$TRABALHO/compose.err")"
    fi
    confere "portas padrão publicadas na API e no Keycloak" "5200 8080" \
        "$(compor config --format json | jq -r '[.services["uniplus-api"].ports[0].published, .services.keycloak.ports[0].published] | join(" ")')"
    confere "o verificador compartilha a rede do clamd" "service:clamd-verificador" \
        "$(compor config --format json | jq -r '.services.verificador.network_mode')"
    confere "kafka-init, clamd e verificador fazem parte do compose" "true" \
        "$(compor config --format json | jq -r '.services | has("kafka-init") and has("clamd-verificador") and has("verificador")')"
    (
        aplicar_deslocamento_de_portas 100
        confere "portas deslocadas chegam ao compose" "5300 8180 9100" \
            "$(compor config --format json | jq -r '[.services["uniplus-api"].ports[0].published, .services.keycloak.ports[0].published, .services.minio.ports[0].published] | join(" ")')"
        confere "issuer do Keycloak acompanha a porta" "http://localhost:8180" \
            "$(compor config --format json | jq -r '.services.keycloak.environment.KC_HOSTNAME')"
    )
else
    echo "  (docker compose ausente; etapa ignorada)"
fi

echo "== dev-up.sh --so-validar em cópia limpa"
if command -v docker >/dev/null && docker compose version >/dev/null 2>&1 && docker info >/dev/null 2>&1; then
    cp -r "$RAIZ/docker" "$RAIZ/scripts" "$TRABALHO/" 2>/dev/null
    rm -f "$TRABALHO/docker/.env" "$TRABALHO/docker/docker-compose.override.yml"

    saida=$("$TRABALHO/scripts/dev-up.sh" --so-validar 2>&1); status=$?
    confere "clone novo: cria os arquivos locais e valida" "0" "$status"
    verifica "docker/.env e override criados a partir dos exemplos" \
        test -f "$TRABALHO/docker/.env" -a -f "$TRABALHO/docker/docker-compose.override.yml"

    # .env antigo: sem a chave do client de integração com o Geo.
    grep -v '^UNIPLUS_API_SELECAO_CLIENT_SECRET=' "$TRABALHO/docker/.env" > "$TRABALHO/env.antigo"
    cp "$TRABALHO/env.antigo" "$TRABALHO/docker/.env"
    saida=$("$TRABALHO/scripts/dev-up.sh" --so-validar 2>&1); status=$?
    confere ".env antigo: falha antes de subir serviço" "1" "$status"
    verifica ".env antigo: a mensagem nomeia a chave ausente" grep -q 'UNIPLUS_API_SELECAO_CLIENT_SECRET' <<< "$saida"
    cp "$RAIZ/docker/.env.example" "$TRABALHO/docker/.env"

    # override antigo: sem os domínios institucionais de desenvolvimento.
    grep -v 'DominiosInstitucionais' "$TRABALHO/docker/docker-compose.override.yml" > "$TRABALHO/override.antigo"
    cp "$TRABALHO/override.antigo" "$TRABALHO/docker/docker-compose.override.yml"
    saida=$("$TRABALHO/scripts/dev-up.sh" --so-validar 2>&1); status=$?
    confere "override antigo: falha antes de subir serviço" "1" "$status"
    verifica "override antigo: a mensagem nomeia a variável ausente" \
        grep -q 'Autorizacao__Instalacao__DominiosInstitucionais__0' <<< "$saida"
else
    echo "  (docker indisponível; etapa ignorada)"
fi

echo "== domínio de desenvolvimento fora de HML e produção"
# Os domínios locais só podem existir no override de exemplo (arquivo de desenvolvimento, não versionado
# na cópia real). Nenhuma configuração do código nem o compose base os traz.
vazamentos=$(grep -rlE 'teste\.unifesspa\.edu\.br|uniplus-test\.local' \
    "$RAIZ/src" "$RAIZ/docker/docker-compose.yml" "$RAIZ/docker/docker-compose.monolito.yml" 2>/dev/null \
    | grep -E 'appsettings|docker-compose' || true)
confere "nenhum appsettings nem compose base traz o domínio local" "" "$vazamentos"
verifica "o override de exemplo traz os domínios de desenvolvimento" \
    grep -q 'DominiosInstitucionais__1: "teste.unifesspa.edu.br"' "$RAIZ/docker/docker-compose.override.example.yml"

echo
if [ "$falhas" -gt 0 ]; then
    echo "$falhas falha(s)."
    exit 1
fi
echo "Todas as conferências passaram."
