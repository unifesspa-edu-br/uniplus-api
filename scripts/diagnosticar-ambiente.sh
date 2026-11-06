#!/usr/bin/env bash
# Diagnóstico do ambiente local, para quando algo não sobe ou se comporta de forma estranha. Não faz parte
# da subida: `docker compose up --build` funciona sem ele.
#
#   scripts/diagnosticar-ambiente.sh
#
# Só lê e avisa; nunca altera arquivo. Termina com 1 se achar problema que impede ou corrompe a subida:
#   - docker/.env ausente, ou sem chave que o docker/.env.example tem (nomeia as chaves);
#   - chave obrigatória com valor vazio;
#   - docker/docker-compose.override.yml que redefine serviço que agora vive no docker-compose.yml
#     (uniplus-api, geo-api, ambiente-init, verificador, clamd-verificador): sobrepõe os valores do base;
#   - o compose recusar a configuração.
# Avisa (sem falhar) de chave do .env que o exemplo não conhece e de porta já ocupada no host (se este
# ambiente já está no ar, as próprias portas aparecem como ocupadas).

set -uo pipefail

RAIZ="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DOCKER="$RAIZ/docker"
ENV_ARQUIVO="$DOCKER/.env"
EXEMPLO="$DOCKER/.env.example"
OVERRIDE="$DOCKER/docker-compose.override.yml"
SERVICOS_DO_BASE=(uniplus-api geo-api ambiente-init verificador clamd-verificador)

problemas=0
falha() { echo "  FALHA  $1"; problemas=$((problemas + 1)); }
aviso() { echo "  aviso  $1"; }
ok()    { echo "  ok     $1"; }

chaves_de() { grep -E '^[A-Za-z_][A-Za-z0-9_]*=' "$1" | cut -d= -f1 | sort -u; }
valor_de()  { grep -E "^$2=" "$1" | tail -n1 | cut -d= -f2- | sed -E 's/\r$//; s/^"(.*)"$/\1/; s/^'"'"'(.*)'"'"'$/\1/'; }

echo "== docker/.env"
if [[ ! -f "$ENV_ARQUIVO" ]]; then
    falha "docker/.env não existe. Rode: cp docker/.env.example docker/.env"
else
    faltando=$(comm -13 <(chaves_de "$ENV_ARQUIVO") <(chaves_de "$EXEMPLO"))
    if [[ -n "$faltando" ]]; then
        falha "faltam no docker/.env (copie a linha do .env.example): $(echo "$faltando" | tr '\n' ' ')"
    else
        ok "todas as chaves do .env.example estão no .env"
    fi
    desconhecidas=$(comm -23 <(chaves_de "$ENV_ARQUIVO") <(chaves_de "$EXEMPLO") | grep -v '^COMPOSE_' || true)
    [[ -z "$desconhecidas" ]] || aviso "chaves do .env que o .env.example não tem: $(echo "$desconhecidas" | tr '\n' ' ')"

    vazias=""
    for chave in KEYCLOAK_ADMIN KEYCLOAK_ADMIN_PASSWORD MINIO_ROOT_USER MINIO_ROOT_PASSWORD POSTGRES_DB \
        POSTGRES_PASSWORD POSTGRES_USER UNIPLUS_API_SELECAO_CLIENT_SECRET GEO_IMAGE_TAG; do
        if grep -qE "^$chave=" "$ENV_ARQUIVO" && [[ -z "$(valor_de "$ENV_ARQUIVO" "$chave")" ]]; then
            vazias="$vazias $chave"
        fi
    done
    if [[ -n "$vazias" ]]; then falha "valor vazio no docker/.env:$vazias"; else ok "chaves obrigatórias preenchidas"; fi
fi

echo "== override"
if [[ ! -f "$OVERRIDE" ]]; then
    ok "sem docker-compose.override.yml (só necessário para o stack completo)"
else
    definidos=$(awk '/^services:/{d=1;next} /^[^ #]/{d=0} d && /^  [A-Za-z0-9_-]+:[[:space:]]*$/{gsub(/[ :]/,"");print}' "$OVERRIDE")
    colisao=""
    for servico in "${SERVICOS_DO_BASE[@]}"; do
        grep -qx "$servico" <<< "$definidos" && colisao="$colisao $servico"
    done
    if [[ -n "$colisao" ]]; then
        falha "o docker-compose.override.yml redefine serviço que agora vive no docker-compose.yml:$colisao. Apague-o, ou recopie o docker-compose.override.example.yml se precisar do stack completo."
    else
        ok "o override não redefine serviço do compose base"
    fi
fi

echo "== compose"
if ! command -v docker >/dev/null || ! docker compose version >/dev/null 2>&1; then
    aviso "docker compose ausente; etapa ignorada"
elif [[ -f "$ENV_ARQUIVO" ]]; then
    if saida=$(cd "$DOCKER" && docker compose config -q 2>&1); then
        ok "o compose aceita a configuração"
    else
        falha "o compose recusou a configuração: $(echo "$saida" | head -n3)"
    fi
fi

echo "== portas do host"
if [[ -f "$ENV_ARQUIVO" ]] && command -v ss >/dev/null; then
    ocupadas=""
    while IFS='=' read -r chave valor; do
        valor="${valor//[\"\' $'\r']/}"
        [[ "$valor" =~ ^[0-9]+$ ]] || continue
        [[ -n "$(ss -ltnH "sport = :$valor" 2>/dev/null)" ]] && ocupadas="$ocupadas $chave=$valor"
    done < <(grep -E '^[A-Z_]+_PORT=' "$ENV_ARQUIVO")
    if [[ -n "$ocupadas" ]]; then
        aviso "portas já em uso (outro ambiente ou este mesmo, se já está no ar):$ocupadas"
    else
        ok "nenhuma porta do .env está em uso"
    fi
fi

echo
if [[ "$problemas" -gt 0 ]]; then
    echo "$problemas problema(s) encontrado(s)."
    exit 1
fi
echo "Nada de errado nos arquivos do ambiente. Se ainda não sobe, veja: cd docker && docker compose logs <serviço>"
