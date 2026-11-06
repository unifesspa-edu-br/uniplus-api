#!/usr/bin/env bash
# Leva o ambiente local do zero a "dois gestores de acessos instalados e acesso funcionando".
#
# O que faz, em ordem:
#   1. confere as ferramentas (docker, jq, curl) e cria docker/.env e docker/docker-compose.override.yml
#      a partir dos exemplos, quando não existem; se existem, acusa a chave que lhes falta;
#   2. sobe a infraestrutura, o kafka-init, o verificador de arquivos (clamd + verificador), o Geo e a API;
#   3. configura o Keycloak (scripts/setup-keycloak-dev.sh): realm, personas fictícias, papel de instalação;
#   4. instala os dois gestores pela API (scripts/dev-instalar-gestores.sh) e confere o acesso.
#
# Idempotente: rodar de novo com o ambiente no ar e os gestores instalados não altera o estado. Banco
# recriado (`docker compose down -v`), roda e reinstala.
#
# Pré-requisitos: Docker (com Compose v2), jq e curl.
#
# Uso:
#   scripts/dev-up.sh                                     # ambiente mínimo para a API e a instalação
#   scripts/dev-up.sh --completo                          # também portal-api, gateway e os 4 frontends
#   scripts/dev-up.sh --deslocamento-de-portas 100        # todas as portas +100 (segundo ambiente)
#   scripts/dev-up.sh --sem-instalacao                    # sobe e configura, sem instalar os gestores
#   scripts/dev-up.sh --so-validar                        # só confere .env, override e compose
#
# Portas: cada serviço publica em ${NOME_PORT:-padrão} (lista em docker/.env.example). Defina a
# variável no shell ou no docker/.env, ou use --deslocamento-de-portas. Para dois ambientes na mesma
# máquina, defina também COMPOSE_PROJECT_NAME diferente (o deslocamento já usa um nome próprio).
#
# DEV ONLY: nada daqui vale para HML ou produção.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
DOCKER_DIR="$REPO_ROOT/docker"

# shellcheck source=lib/dev-up-validacao.sh
source "$SCRIPT_DIR/lib/dev-up-validacao.sh"

ENV_ARQUIVO="$DOCKER_DIR/.env"
ENV_EXEMPLO="$DOCKER_DIR/.env.example"
OVERRIDE_ARQUIVO="$DOCKER_DIR/docker-compose.override.yml"
OVERRIDE_EXEMPLO="$DOCKER_DIR/docker-compose.override.example.yml"

# Serviços do ambiente mínimo: o que a API e a instalação dos gestores exigem. O portal-api, o
# gateway e os frontends ficam para --completo (os frontends precisam do uniplus-web ao lado).
SERVICOS_MINIMOS=(
    postgres redis kafka kafka-init minio minio-init rabbitmq mailpit keycloak apicurio
    geo-api clamd-verificador verificador uniplus-api
)

COMPLETO=false
SEM_INSTALACAO=false
SO_VALIDAR=false
DESLOCAMENTO=""

log()   { echo "[dev-up] $*" >&2; }
falha() { echo "[dev-up] ERRO: $*" >&2; exit 1; }

listar_itens() {
    local item
    while IFS= read -r item; do
        echo "[dev-up]   - $item" >&2
    done <<< "$1"
}

uso() {
    sed -n '2,/^# DEV ONLY/p' "${BASH_SOURCE[0]}" | sed -E 's/^# ?//'
}

ler_argumentos() {
    while [ "$#" -gt 0 ]; do
        case "$1" in
            --completo) COMPLETO=true ;;
            --sem-instalacao) SEM_INSTALACAO=true ;;
            --so-validar) SO_VALIDAR=true ;;
            --deslocamento-de-portas)
                [ "$#" -ge 2 ] || falha "--deslocamento-de-portas pede um número (ex.: 100)."
                DESLOCAMENTO="$2"
                shift
                ;;
            -h|--help) uso; exit 0 ;;
            *) falha "argumento desconhecido: $1 (use --help)." ;;
        esac
        shift
    done
}

conferir_ferramentas() {
    local ferramenta
    for ferramenta in docker jq curl; do
        command -v "$ferramenta" >/dev/null || falha "'$ferramenta' não encontrado no PATH."
    done
    docker compose version >/dev/null 2>&1 || falha "Docker Compose v2 ('docker compose') não encontrado."
    docker info >/dev/null 2>&1 || falha "o daemon do Docker não responde. Inicie o Docker e tente de novo."
}

aplicar_portas() {
    [ -n "$DESLOCAMENTO" ] || return 0
    aplicar_deslocamento_de_portas "$DESLOCAMENTO" \
        || falha "deslocamento inválido ('$DESLOCAMENTO'): use um inteiro que mantenha todas as portas até 65535 e que não leve o KAFKA_PORT a 9093 ou 29092 (listeners internos do broker)."
    export COMPOSE_PROJECT_NAME="${COMPOSE_PROJECT_NAME:-uniplus-desloc-$DESLOCAMENTO}"
    log "Portas deslocadas em +$DESLOCAMENTO; projeto do compose: $COMPOSE_PROJECT_NAME"
}

# Cria os arquivos locais a partir dos exemplos; se já existem, confere se têm as chaves de hoje.
preparar_arquivos_locais() {
    if [ ! -f "$ENV_ARQUIVO" ]; then
        cp "$ENV_EXEMPLO" "$ENV_ARQUIVO"
        log "Criado docker/.env a partir do exemplo (só valores de desenvolvimento)."
    fi
    if [ ! -f "$OVERRIDE_ARQUIVO" ]; then
        cp "$OVERRIDE_EXEMPLO" "$OVERRIDE_ARQUIVO"
        log "Criado docker/docker-compose.override.yml a partir do exemplo."
    fi

    local ausentes
    ausentes=$(chaves_ausentes_do_env "$ENV_EXEMPLO" "$ENV_ARQUIVO")
    if [ -n "$ausentes" ]; then
        {
            echo "[dev-up] ERRO: docker/.env está desatualizado. Falta(m) a(s) chave(s):"
            listar_itens "$ausentes"
            echo "[dev-up] Copie a(s) linha(s) correspondente(s) de docker/.env.example para docker/.env"
            echo "[dev-up] (valores de desenvolvimento, sem segredo real), ou recopie o exemplo:"
            echo "[dev-up]   cp docker/.env docker/.env.bak && cp docker/.env.example docker/.env"
        } >&2
        exit 1
    fi

    ausentes=$(chaves_ausentes_do_override "$OVERRIDE_EXEMPLO" "$OVERRIDE_ARQUIVO")
    if [ -n "$ausentes" ]; then
        {
            echo "[dev-up] ERRO: docker/docker-compose.override.yml está desatualizado. Falta(m) a(s) variável(is) de serviço:"
            listar_itens "$ausentes"
            echo "[dev-up] Copie o(s) bloco(s) correspondente(s) de docker/docker-compose.override.example.yml,"
            echo "[dev-up] ou recopie o exemplo:"
            echo "[dev-up]   cp docker/docker-compose.override.yml docker/docker-compose.override.yml.bak"
            echo "[dev-up]   cp docker/docker-compose.override.example.yml docker/docker-compose.override.yml"
        } >&2
        exit 1
    fi
}

compose() {
    docker compose -f "$DOCKER_DIR/docker-compose.yml" -f "$OVERRIDE_ARQUIVO" "$@"
}

conferir_compose() {
    local saida
    saida=$(compose config -q 2>&1) || {
        echo "$saida" >&2
        falha "o docker compose recusou a configuração (variável obrigatória sem valor?). Veja a mensagem acima."
    }
}

aguardar_http() {
    local nome="$1" url="$2" segundos="$3" fim=$((SECONDS + $3))
    log "Aguardando $nome ($url)"
    while [ "$SECONDS" -lt "$fim" ]; do
        curl -fsS -o /dev/null "$url" 2>/dev/null && return 0
        sleep 3
    done
    falha "$nome não respondeu em ${segundos}s em $url. Veja: docker compose -f docker/docker-compose.yml -f docker/docker-compose.override.yml logs"
}

subir_servicos() {
    log "Subindo os serviços (a primeira vez constrói imagens; o clamd baixa a base de assinaturas)."
    if $COMPLETO; then
        compose up -d --build
    else
        compose up -d --build "${SERVICOS_MINIMOS[@]}"
    fi
}

configurar_keycloak() {
    local kc_url admin senha
    kc_url="http://localhost:$(valor_do_env "$ENV_ARQUIVO" KEYCLOAK_PORT 8080)"
    admin=$(valor_do_env "$ENV_ARQUIVO" KEYCLOAK_ADMIN admin)
    senha=$(valor_do_env "$ENV_ARQUIVO" KEYCLOAK_ADMIN_PASSWORD admin)

    log "Configurando o Keycloak (realm, personas fictícias, papel de instalação)."
    KC_URL="$kc_url" KC_ADMIN_USER="$admin" KC_ADMIN_PASS="$senha" \
        CONFIGURACAO_WEB_ORIGIN="http://localhost:$(valor_do_env "$ENV_ARQUIVO" CONFIGURACAO_WEB_PORT 4203)" \
        bash "$SCRIPT_DIR/setup-keycloak-dev.sh" >/dev/null
}

instalar_gestores() {
    KC_URL="http://localhost:$(valor_do_env "$ENV_ARQUIVO" KEYCLOAK_PORT 8080)" \
        API_URL="http://localhost:$(valor_do_env "$ENV_ARQUIVO" API_PORT 5200)" \
        MAILPIT_URL="http://localhost:$(valor_do_env "$ENV_ARQUIVO" MAILPIT_UI_PORT 8025)" \
        KEYCLOAK_ADMIN="$(valor_do_env "$ENV_ARQUIVO" KEYCLOAK_ADMIN admin)" \
        KEYCLOAK_ADMIN_PASSWORD="$(valor_do_env "$ENV_ARQUIVO" KEYCLOAK_ADMIN_PASSWORD admin)" \
        bash "$SCRIPT_DIR/dev-instalar-gestores.sh"
}

resumo() {
    local api kc mailpit
    api=$(valor_do_env "$ENV_ARQUIVO" API_PORT 5200)
    kc=$(valor_do_env "$ENV_ARQUIVO" KEYCLOAK_PORT 8080)
    mailpit=$(valor_do_env "$ENV_ARQUIVO" MAILPIT_UI_PORT 8025)
    cat >&2 <<EOF

[dev-up] Ambiente pronto.
  API ............ http://localhost:$api
  Keycloak ....... http://localhost:$kc   (realm unifesspa; personas com a senha Uni+Teste26)
  Mailpit ........ http://localhost:$mailpit
  Gestores ....... diogo.souza e leandro.viana (executor da instalação: admin, sem acesso às concessões)
EOF
}

main() {
    ler_argumentos "$@"
    aplicar_portas
    conferir_ferramentas
    preparar_arquivos_locais
    conferir_compose
    if $SO_VALIDAR; then
        log "Arquivos de configuração conferidos."
        exit 0
    fi

    subir_servicos
    aguardar_http "o Keycloak" \
        "http://localhost:$(valor_do_env "$ENV_ARQUIVO" KEYCLOAK_PORT 8080)/realms/unifesspa" 240
    configurar_keycloak
    aguardar_http "a API" "http://localhost:$(valor_do_env "$ENV_ARQUIVO" API_PORT 5200)/health" 300

    if $SEM_INSTALACAO; then
        log "Instalação dos gestores ignorada (--sem-instalacao)."
    else
        instalar_gestores
    fi
    resumo
}

main "$@"
