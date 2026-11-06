#!/usr/bin/env bash
# Funções de scripts/dev-up.sh que não tocam em Docker nem em rede: conferência do .env e do
# override contra os exemplos versionados, e deslocamento das portas publicadas. Ficam num arquivo
# próprio para tools/dev-up/validate.sh exercitá-las no CI sem subir o ambiente.
#
# Arquivo para ser lido com `source`: não liga `set -e` nem define estado além das funções abaixo.

# Variáveis de porta aceitas pelos arquivos de compose, com o valor padrão de cada uma. A lista
# precisa bater com os `${VAR:-padrão}` dos compose; tools/dev-up/validate.sh confere isso.
DEV_PORTAS_PADRAO=(
    POSTGRES_PORT:5432
    REDIS_PORT:6379
    KAFKA_PORT:9092
    MINIO_PORT:9000
    MINIO_CONSOLE_PORT:9001
    RABBITMQ_PORT:5672
    RABBITMQ_MANAGEMENT_PORT:15672
    MAILPIT_SMTP_PORT:1025
    MAILPIT_UI_PORT:8025
    LDAP_PORT:1389
    KEYCLOAK_PORT:8080
    APICURIO_PORT:8081
    API_PORT:5200
    GEO_API_PORT:5400
    PORTAL_API_PORT:5302
    OPENAPI_DOCS_PORT:5299
    GATEWAY_PORT:5000
    GATEWAY_DASHBOARD_PORT:5080
    SELECAO_WEB_PORT:4200
    INGRESSO_WEB_PORT:4201
    PORTAL_WEB_PORT:4202
    CONFIGURACAO_WEB_PORT:4203
)

# Chaves de um .env: só linhas `CHAVE=valor`, sem as comentadas.
chaves_do_env() {
    grep -E '^[A-Za-z_][A-Za-z0-9_]*=' "$1" | cut -d= -f1 | sort -u
}

# Chaves que algum compose usa com valor padrão (`${CHAVE:-padrão}`): se faltarem no .env, nada quebra.
chaves_com_padrao_nos_compose() {
    grep -ohE '\$\{[A-Za-z_][A-Za-z0-9_]*:-' "$@" | sed -E 's/^\$\{//; s/:-$//' | sort -u
}

# Chaves do .env de exemplo que o .env real não tem e que o compose exige: as que têm padrão nos arquivos de
# compose informados depois do segundo argumento (portas, credenciais do RabbitMQ...) não contam como falta.
chaves_ausentes_do_env() {
    local exemplo="$1" real="$2"
    shift 2
    if [ "$#" -eq 0 ]; then
        comm -13 <(chaves_do_env "$real") <(chaves_do_env "$exemplo")
    else
        comm -13 <(chaves_do_env "$real") <(comm -23 <(chaves_do_env "$exemplo") <(chaves_com_padrao_nos_compose "$@"))
    fi
}

# Variáveis `${CHAVE...}` que os compose usam, uma por linha.
variaveis_usadas_nos_compose() {
    grep -ohE '\$\{[A-Za-z_][A-Za-z0-9_]*' "$@" | sed -E 's/^\$\{//' | sort -u
}

# Chaves de configuração do .NET (`Secao__Chave`) declaradas como variável de ambiente de um serviço
# do override. As variáveis simples (ASPNETCORE_URLS) não entram: o que envelhece é a configuração.
chaves_do_override() {
    grep -oE '^[[:space:]]+[A-Za-z_][A-Za-z0-9_]*(__[A-Za-z0-9_]+)+:' "$1" | sed -E 's/^[[:space:]]+//; s/:$//' | sort -u
}

# Chaves do override de exemplo que o override real não tem.
chaves_ausentes_do_override() {
    local exemplo="$1" real="$2"
    comm -13 <(chaves_do_override "$real") <(chaves_do_override "$exemplo")
}

# Valor de uma chave do .env, ou o padrão quando a chave não existe. O ambiente do shell vence o
# arquivo, como no docker compose.
valor_do_env() {
    local arquivo="$1" chave="$2" padrao="${3:-}" valor
    if [ -n "${!chave:-}" ]; then
        printf '%s\n' "${!chave}"
        return 0
    fi
    valor=$(grep -E "^${chave}=" "$arquivo" 2>/dev/null | tail -1 | cut -d= -f2- || true)
    # Como o compose lê o .env: sem CR, sem comentário depois de espaço e sem aspas nas pontas.
    valor=$(printf '%s' "$valor" | tr -d '\r' | sed -E 's/[[:space:]]+#.*$//; s/^"(.*)"$/\1/; s/^'"'"'(.*)'"'"'$/\1/')
    printf '%s\n' "${valor:-$padrao}"
}

# Exporta cada variável de porta com o padrão somado ao deslocamento, para um segundo ambiente
# coexistir com o primeiro. A porta que a pessoa já definiu no ambiente do shell não é alterada.
aplicar_deslocamento_de_portas() {
    local deslocamento="$1" par variavel padrao
    if ! [[ "$deslocamento" =~ ^[0-9]+$ ]]; then
        return 1
    fi
    for par in "${DEV_PORTAS_PADRAO[@]}"; do
        variavel="${par%%:*}"
        padrao="${par##*:}"
        if (( padrao + deslocamento > 65535 )); then
            return 2
        fi
        if [ -z "${!variavel:-}" ]; then
            export "$variavel=$((padrao + deslocamento))"
        fi
        # O listener do host do Kafka não pode cair nas portas dos listeners internos do broker.
        if [ "$variavel" = "KAFKA_PORT" ] && { [ "${!variavel}" = "9093" ] || [ "${!variavel}" = "29092" ]; }; then
            return 3
        fi
    done
}

# Pares `VARIAVEL:padrão` de porta que os arquivos de compose usam, um por linha.
portas_declaradas_nos_compose() {
    grep -ohE '\$\{[A-Z_]+_PORT:-[0-9]+\}' "$@" | sed -E 's/^\$\{([A-Z_]+):-([0-9]+)\}$/\1:\2/' | sort -u
}
