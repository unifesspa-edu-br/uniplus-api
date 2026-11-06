#!/usr/bin/env bash
# Diagnóstico do ambiente local, para quando algo não sobe ou se comporta de forma estranha. Não faz parte
# da subida: `docker compose up --build` funciona sem ele.
#
#   scripts/diagnosticar-ambiente.sh             só diagnostica e mostra como corrigir; não altera nada
#   scripts/diagnosticar-ambiente.sh --corrigir  aplica a correção, guardando cópia de segurança (*.bak-<data>)
#
# Termina com 1 se achar problema que impede ou corrompe a subida:
#   - docker/.env ausente, ou sem chave que o docker/.env.example tem (nomeia as chaves);
#   - chave obrigatória com valor vazio;
#   - docker/docker-compose.override.yml que redefine serviço que agora vive no docker-compose.yml
#     (uniplus-api, geo-api, ambiente-init, verificador, clamd-verificador): sobrepõe os valores do base;
#   - o compose recusar a configuração.
# Avisa (sem falhar) de chave do .env que o exemplo não conhece e de porta já ocupada no host (se este
# ambiente já está no ar, as próprias portas aparecem como ocupadas).
#
# O --corrigir só mexe em desenvolvimento local: cria o .env a partir do exemplo, acrescenta ao fim do .env as
# chaves que faltam (mantém os valores que você já tem), preenche com o valor do exemplo as obrigatórias vazias
# e renomeia para *.bak-<data> o override antigo. Não derruba nem sobe contêiner.

set -uo pipefail

RAIZ="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DOCKER="$RAIZ/docker"
ENV_ARQUIVO="$DOCKER/.env"
EXEMPLO="$DOCKER/.env.example"
OVERRIDE="$DOCKER/docker-compose.override.yml"
# Serviços que o override antigo definia por inteiro e que agora vivem no docker-compose.yml. O override de
# exemplo do stack completo não os define (só ajusta variáveis do uniplus-api, ver abaixo).
SERVICOS_DO_BASE=(geo-api ambiente-init verificador clamd-verificador)
# As obrigatórias são as que o compose exige com `${VAR:?...}`: a lista sai dele, não de uma cópia à mão.
mapfile -t OBRIGATORIAS < <(grep -ohE '\$\{[A-Za-z_][A-Za-z0-9_]*:\?' "$DOCKER/docker-compose.yml" "$DOCKER/docker-compose.override.example.yml" | sed -E 's/^\$\{//; s/:\?$//' | sort -u)

CORRIGIR=false
case "${1:-}" in
    "") ;;
    --corrigir) CORRIGIR=true ;;
    *) echo "Uso: scripts/diagnosticar-ambiente.sh [--corrigir]" >&2; exit 2 ;;
esac

DATA="$(date +%Y%m%d-%H%M%S)"
problemas=0
falha() { echo "  FALHA  $1"; problemas=$((problemas + 1)); }
dica()  { echo "         como corrigir: $1"; }
aviso() { echo "  aviso  $1"; }
ok()    { echo "  ok     $1"; }

chaves_de() { grep -E '^[A-Za-z_][A-Za-z0-9_]*=' "$1" | cut -d= -f1 | sort -u; }
valor_de()  { grep -E "^$2=" "$1" | tail -n1 | cut -d= -f2- | sed -E 's/\r$//; s/^"(.*)"$/\1/; s/^'"'"'(.*)'"'"'$/\1/'; }

# Preenchidos pela verificação e consumidos pela correção.
ENV_AUSENTE=false
CHAVES_FALTANDO=""
CHAVES_VAZIAS=""
OVERRIDE_ANTIGO=false

verificar() {
    problemas=0; ENV_AUSENTE=false; CHAVES_FALTANDO=""; CHAVES_VAZIAS=""; OVERRIDE_ANTIGO=false

    echo "== docker/.env"
    if [[ ! -f "$ENV_ARQUIVO" ]]; then
        ENV_AUSENTE=true
        falha "docker/.env não existe."
        dica "cp docker/.env.example docker/.env"
    else
        CHAVES_FALTANDO=$(comm -13 <(chaves_de "$ENV_ARQUIVO") <(chaves_de "$EXEMPLO") | tr '\n' ' ')
        if [[ -n "$CHAVES_FALTANDO" ]]; then
            falha "faltam no docker/.env: $CHAVES_FALTANDO"
            dica "copie essas linhas do docker/.env.example para o docker/.env (ou use --corrigir)"
        else
            ok "todas as chaves do .env.example estão no .env"
        fi
        desconhecidas=$(comm -23 <(chaves_de "$ENV_ARQUIVO") <(chaves_de "$EXEMPLO") | grep -v '^COMPOSE_' || true)
        [[ -z "$desconhecidas" ]] || aviso "chaves do .env que o .env.example não tem: $(echo "$desconhecidas" | tr '\n' ' ')"

        for chave in "${OBRIGATORIAS[@]}"; do
            if grep -qE "^$chave=" "$ENV_ARQUIVO" && [[ -z "$(valor_de "$ENV_ARQUIVO" "$chave")" ]]; then
                CHAVES_VAZIAS="$CHAVES_VAZIAS $chave"
            fi
        done
        if [[ -n "$CHAVES_VAZIAS" ]]; then
            falha "valor vazio no docker/.env:$CHAVES_VAZIAS"
            dica "preencha com o valor do docker/.env.example (ou use --corrigir)"
        else
            ok "chaves obrigatórias preenchidas"
        fi
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
        # O uniplus-api também aparece no override de exemplo, só com variáveis soltas. O override antigo o
        # definia por inteiro: com `build:` ou as connection strings, que agora vêm do docker-compose.yml.
        bloco_api=$(awk '/^services:/{d=1;next} /^[^ #]/{d=0} d && /^  [A-Za-z0-9_-]+:[[:space:]]*$/{em=($1=="uniplus-api:");next} d && em' "$OVERRIDE")
        if grep -qE '^[[:space:]]+(build:|ConnectionStrings__)' <<< "$bloco_api"; then
            colisao="$colisao uniplus-api"
        fi
        if [[ -n "$colisao" ]]; then
            OVERRIDE_ANTIGO=true
            falha "o docker-compose.override.yml redefine serviço que agora vive no docker-compose.yml:$colisao"
            dica "mv docker/docker-compose.override.yml docker/docker-compose.override.yml.bak (ou use --corrigir). Só o stack completo precisa de override: cp docker/docker-compose.override.example.yml docker/docker-compose.override.yml"
        else
            ok "o override não redefine serviço do compose base (o do stack completo é válido)"
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
            dica "corrija o que as mensagens acima nomeiam; se vier de .env ou override antigos, rode com --corrigir"
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
            echo "         para desviar, troque a variável *_PORT no docker/.env (e COMPOSE_PROJECT_NAME num segundo ambiente)"
        else
            ok "nenhuma porta do .env está em uso"
        fi
    fi
}

corrigir() {
    echo
    echo "== corrigindo (cópias de segurança com sufixo .bak-$DATA)"
    if $ENV_AUSENTE; then
        cp "$EXEMPLO" "$ENV_ARQUIVO" && echo "  docker/.env criado a partir do .env.example"
    else
        if [[ -n "$CHAVES_FALTANDO" || -n "$CHAVES_VAZIAS" ]]; then
            cp "$ENV_ARQUIVO" "$ENV_ARQUIVO.bak-$DATA" && echo "  cópia: docker/.env.bak-$DATA"
        fi
        if [[ -n "$CHAVES_FALTANDO" ]]; then
            ultimo_caractere=$(tail -c1 "$ENV_ARQUIVO")
            {
                [[ -z "$ultimo_caractere" ]] || echo
                echo "# Acrescentadas por scripts/diagnosticar-ambiente.sh --corrigir ($DATA)"
                for chave in $CHAVES_FALTANDO; do grep -E "^$chave=" "$EXEMPLO" | tail -n1; done
            } >> "$ENV_ARQUIVO"
            echo "  acrescentadas ao .env: $CHAVES_FALTANDO"
        fi
        for chave in $CHAVES_VAZIAS; do
            padrao=$(grep -E "^$chave=" "$EXEMPLO" | tail -n1)
            sed -i "s|^$chave=.*|${padrao//|/\\|}|" "$ENV_ARQUIVO"
            echo "  preenchida com o valor do exemplo: $chave"
        done
    fi
    if $OVERRIDE_ANTIGO; then
        mv "$OVERRIDE" "$OVERRIDE.bak-$DATA" && echo "  override antigo movido para docker/docker-compose.override.yml.bak-$DATA"
    fi
    echo
    echo "== conferindo de novo"
}

verificar
if $CORRIGIR && [[ "$problemas" -gt 0 ]]; then
    corrigir
    verificar
fi

echo
if [[ "$problemas" -gt 0 ]]; then
    echo "$problemas problema(s) encontrado(s)."
    $CORRIGIR || echo "Para aplicar as correções acima, com cópia de segurança: scripts/diagnosticar-ambiente.sh --corrigir"
    exit 1
fi
echo "Nada de errado nos arquivos do ambiente. Se ainda não sobe, veja: cd docker && docker compose logs <serviço>"
