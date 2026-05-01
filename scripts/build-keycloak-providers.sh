#!/usr/bin/env bash
# build-keycloak-providers.sh — Builda os artefatos do repositório
# uniplus-keycloak-providers (SPIs customizados do Keycloak) via Maven em
# container Docker, sem exigir Maven instalado no host.
#
# O resultado fica disponível no diretório target/ de cada módulo do repositório
# irmão e é montado pelo docker-compose do uniplus-api em
# /opt/keycloak/providers/ (ver docker/keycloak/README.md).
#
# Uso:
#   scripts/build-keycloak-providers.sh                # builda tudo (mvn -pl :all)
#   scripts/build-keycloak-providers.sh cpf-matcher    # builda apenas um módulo
#
# Variáveis de ambiente opcionais:
#   PROVIDERS_REPO     (default: ../uniplus-keycloak-providers — relativo ao
#                       uniplus-api; assume convenção repositories/)
#   MAVEN_IMAGE        (default: maven:3.9-eclipse-temurin-21)
#   MAVEN_OPTS         repassado para o Maven dentro do container
#   SKIP_TESTS         se "true", roda com -DskipTests
#
# Requisitos:
#   - Docker no host
#   - Repositório uniplus-keycloak-providers clonado ao lado do uniplus-api/

set -euo pipefail

# ---- Logging --------------------------------------------------------------

log()  { printf '\033[1;36m==> %s\033[0m\n' "$*" >&2; }
ok()   { printf '\033[1;32m    OK %s\033[0m\n' "$*" >&2; }
warn() { printf '\033[1;33m!! %s\033[0m\n' "$*" >&2; }
fail() { printf '\033[1;31m✗ %s\033[0m\n' "$*" >&2; exit 1; }

# ---- Localiza repos -------------------------------------------------------

# Diretório do uniplus-api (raiz do repositório, derivado da localização do script).
API_REPO="$(cd "$(dirname "$0")/.." && pwd)"
PROVIDERS_REPO="${PROVIDERS_REPO:-$API_REPO/../uniplus-keycloak-providers}"

if [ ! -d "$PROVIDERS_REPO" ]; then
    warn "Repositório uniplus-keycloak-providers não encontrado em: $PROVIDERS_REPO"
    warn "Clone-o ao lado do uniplus-api seguindo a convenção repositories/:"
    warn "  cd $(dirname "$API_REPO")"
    fail "  git clone https://github.com/unifesspa-edu-br/uniplus-keycloak-providers.git"
fi

if [ ! -f "$PROVIDERS_REPO/pom.xml" ]; then
    fail "pom.xml ausente em $PROVIDERS_REPO — caminho parece incorreto."
fi

PROVIDERS_REPO_ABS="$(cd "$PROVIDERS_REPO" && pwd)"

# ---- Pré-checks -----------------------------------------------------------

command -v docker >/dev/null 2>&1 || fail "Docker não está instalado ou não está no PATH"
docker info >/dev/null 2>&1       || fail "Docker daemon não responde — está rodando?"

# ---- Build ----------------------------------------------------------------

MAVEN_IMAGE="${MAVEN_IMAGE:-maven:3.9-eclipse-temurin-21}"
MAVEN_OPTS_VALUE="${MAVEN_OPTS:-}"

# Argumentos extras conforme parâmetros do script
MVN_TARGETS=("clean" "package")
if [ "${SKIP_TESTS:-false}" = "true" ]; then
    MVN_TARGETS+=("-DskipTests")
fi

MODULE=""
if [ "$#" -gt 0 ]; then
    MODULE="$1"
    if [ ! -d "$PROVIDERS_REPO_ABS/$MODULE" ]; then
        fail "Módulo '$MODULE' não existe em $PROVIDERS_REPO_ABS"
    fi
    MVN_TARGETS+=("-pl" "$MODULE" "-am")
    log "Buildando módulo $MODULE"
else
    log "Buildando todos os módulos de uniplus-keycloak-providers"
fi

# Cache local do Maven (~/.m2) para evitar re-download entre execuções
M2_CACHE="${HOME}/.m2"
mkdir -p "$M2_CACHE"

# Container roda como UID:GID do host para que os arquivos gerados em target/
# tenham as permissões certas (sem precisar de sudo para limpar).
docker run --rm \
    -u "$(id -u):$(id -g)" \
    -v "$PROVIDERS_REPO_ABS:/workspace" \
    -v "$M2_CACHE:/var/maven/.m2" \
    -e MAVEN_CONFIG=/var/maven/.m2 \
    -e MAVEN_OPTS="$MAVEN_OPTS_VALUE" \
    -w /workspace \
    "$MAVEN_IMAGE" \
    mvn -Duser.home=/var/maven "${MVN_TARGETS[@]}"

# ---- Verifica saída -------------------------------------------------------

# Quando o build é direcionado a um módulo específico, exigimos que o JAR
# tenha sido gerado dentro daquele target/. Sem isso, um build que sucede
# em dependências mas falha no módulo alvo passaria silenciosamente —
# resultando em volume mount de arquivo inexistente no docker-compose.
if [ -n "$MODULE" ]; then
    JAR_PATH=$(find "$PROVIDERS_REPO_ABS/$MODULE/target" -maxdepth 1 -name '*.jar' \
        -not -name '*-sources.jar' -not -name '*-javadoc.jar' -print -quit 2>/dev/null)
    if [ -z "$JAR_PATH" ]; then
        warn "Build concluiu sem erro, mas nenhum JAR foi produzido em $PROVIDERS_REPO_ABS/$MODULE/target/."
        fail "Verifique o packaging do pom.xml e o output do Maven acima."
    fi
    ok "Build concluído. JAR: $JAR_PATH"

    # O docker-compose monta um caminho hardcoded — se a versão no pom mudar,
    # o mount no Keycloak vira arquivo inexistente. Avisa cedo no build.
    if [ "$MODULE" = "cpf-matcher" ]; then
        EXPECTED="$PROVIDERS_REPO_ABS/cpf-matcher/target/cpf-matcher-1.0.0-SNAPSHOT.jar"
        if [ ! -f "$EXPECTED" ]; then
            warn "JAR gerado ($JAR_PATH) não bate com o caminho esperado pelo docker-compose:"
            warn "  $EXPECTED"
            warn "Verifique se a versão do pom.xml foi alterada — o volume mount em"
            warn "docker/docker-compose.yml precisa ser atualizado em sincronia."
        fi
    fi
else
    if ! find "$PROVIDERS_REPO_ABS" -path '*/target/*.jar' -not -name '*-sources.jar' -not -name '*-javadoc.jar' -print -quit | grep -q .; then
        fail "Build concluiu sem erro, mas nenhum JAR foi produzido em target/."
    fi
    ok "Build concluído. JARs disponíveis em $PROVIDERS_REPO_ABS/<modulo>/target/"
fi
