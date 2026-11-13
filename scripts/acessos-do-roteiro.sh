#!/usr/bin/env bash
# acessos-do-roteiro.sh — concede ao executor dos roteiros de processo de teste
# (ADMIN_USER) só as permissões que eles usam, pelo mesmo caminho da API que
# uma pessoa seguiria (ADR-0151). Lido com `source` por
# roteiro-processo-teste-inscricao.sh; não roda sozinho.
#
# Numa base nova, o papel de instalação do Keycloak e os dois gestores de
# acessos instalados pelo ambiente-init não dão ao executor nenhuma permissão de
# operação: a autorização vem só das concessões gravadas. Sem elas, o primeiro
# POST do roteiro (tipos de ato) responde 403.
#
# Para cada um dos dois perfis abaixo, quando o executor ainda não o tem:
#   1. o primeiro gestor cria o perfil, envia o PDF fictício que o institui e
#      confirma o envio;
#   2. depois do verificador de arquivos, o segundo gestor aprova a versão (quem
#      cria a versão não a decide);
#   3. o executor pede a concessão para si;
#   4. um gestor que não seja o executor aprova o pedido.
# As permissões são todas de classificação interna: nem a versão nem o pedido
# passam pela proteção de dados, e o pedido não exige o termo de
# responsabilidade.
#
# O perfil institucional cobre os cadastros (Publicações, Organização,
# Configuração); o da unidade cobre a configuração e a publicação do processo e
# só se concede depois que a unidade existe.
#
# Variáveis de ambiente opcionais, além das do roteiro:
#   GESTORES          (default: "diogo.souza leandro.viana") — os gestores
#                     instalados pelo ambiente-init (dev-instalar-gestores.sh)
#   SENHA_GESTORES    (default: $ADMIN_PASS)
#   ESPERA_VERIFICACAO (default: 240) — segundos para o verificador liberar o PDF
#   ARMAZENAMENTO_CONNECT_TO — como em dev-instalar-gestores.sh

GESTORES="${GESTORES:-diogo.souza leandro.viana}"
SENHA_GESTORES="${SENHA_GESTORES:-$ADMIN_PASS}"
ESPERA_VERIFICACAO="${ESPERA_VERIFICACAO:-240}"
PDF_DO_PERFIL="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/edital-de-teste.pdf"

PERFIL_INSTITUCIONAL="Roteiro de teste — cadastros institucionais"
PERMISSOES_INSTITUCIONAIS='[
    "publicacoes:tipos-ato:manter",
    "organizacao:instituicao:manter", "organizacao:unidades:manter",
    "configuracao:campi:manter", "configuracao:locais-oferta:manter",
    "configuracao:cursos:manter", "configuracao:ofertas-curso:manter",
    "configuracao:referencias-reserva-demografica:manter",
    "configuracao:condicoes-atendimento:manter", "configuracao:recursos-acessibilidade:manter",
    "configuracao:tipos-deficiencia:manter", "configuracao:base-legal-bonus-regional:manter",
    "configuracao:calendarios-dias-uteis:manter", "configuracao:pesos-area-enem:manter"
]'
PERFIL_DA_UNIDADE="Roteiro de teste — processo seletivo da unidade"
PERMISSOES_DA_UNIDADE='["selecao:processos:configurar", "selecao:processos:publicar"]'

# Explica o 403 de uma rota do roteiro: a concessão que falta e onde ela nasce.
explicar_recusa_de_acesso() {
    warn "O usuário '$ADMIN_USER' não tem concessão aplicável à rota $1."
    warn "As concessões do roteiro são os perfis '$PERFIL_INSTITUCIONAL' (na instituição) e '$PERFIL_DA_UNIDADE' (na unidade do processo), pedidos e aprovados no início do roteiro."
    warn "Confira os perfis do executor em GET $API_URL/api/profile/me e se os gestores ($GESTORES) estão instalados (serviço ambiente-init)."
}

token_de() {
    local token
    token=$(curl -sS -X POST "$KC_URL/realms/$KC_REALM/protocol/openid-connect/token" \
        --data-urlencode grant_type=password --data-urlencode client_id=admin-cli \
        --data-urlencode "username=$1" --data-urlencode "password=$2" | jq -r '.access_token // empty')
    [ -n "$token" ] || fail "o Keycloak recusou o login de '$1' — rode scripts/setup-keycloak-dev.sh (ou o serviço ambiente-init)."
    printf '%s\n' "$token"
}

# Chamada como uma pessoa; falha com o corpo da recusa. $1=passo $2=token $3=método $4=caminho $5=corpo
como() {
    local passo="$1" token="$2" metodo="$3" caminho="$4" corpo="${5:-}" resp status
    local args=(-sS -w '\n%{http_code}' -X "$metodo" "$API_URL$caminho" -H "Authorization: Bearer $token")
    [ "$metodo" = GET ] || args+=(-H "Idempotency-Key: $(idem)")
    [ -z "$corpo" ] || args+=(-H "Content-Type: application/json" -d "$corpo")
    resp=$(curl "${args[@]}")
    status=$(tail -n1 <<< "$resp")
    case "$status" in
        2??) sed '$d' <<< "$resp" ;;
        *) fail "$passo: $metodo $caminho → $status: $(sed '$d' <<< "$resp" | head -c 600)" ;;
    esac
}

# O executor já tem o perfil no escopo? $1=nome $2=unidade (vazio: instituição)
executor_tem_perfil() {
    como "a leitura dos perfis do executor" "$TOKEN" GET "/api/profile/me" \
        | jq -e --arg n "$1" --arg u "$2" \
            'any(.perfis[]; .nome == $n and (if $u == "" then .escopoTipo == "instituicao" else .unidadeId == $u end))' >/dev/null
}

aguardar_documento_do_perfil() {
    local perfil="$1" token="$2" situacao="" fim=$((SECONDS + ESPERA_VERIFICACAO))
    while [ "$SECONDS" -lt "$fim" ]; do
        situacao=$(como "a leitura do perfil" "$token" GET "/api/autorizacao/perfis/$perfil" | jq -r '.versaoPendente.situacaoDoDocumento // empty')
        [ "$situacao" = "verificado" ] && return 0
        sleep 3
    done
    fail "o documento do perfil $perfil não foi verificado em ${ESPERA_VERIFICACAO}s (situação: '${situacao:-desconhecida}'). Confira o serviço 'verificador'."
}

enviar_documento_do_perfil() {
    local envio="$1" token="$2" url etag
    local -a redirecionamento=()
    [ -z "${ARMAZENAMENTO_CONNECT_TO:-}" ] || redirecionamento=(--connect-to "$ARMAZENAMENTO_CONNECT_TO")
    url=$(jq -r '.urlDeEnvio' <<< "$envio")
    etag=$(curl -fsS -D - -o /dev/null -X PUT -H "Content-Type: application/pdf" ${redirecionamento[@]+"${redirecionamento[@]}"} \
        --data-binary @"$PDF_DO_PERFIL" "$url" | tr -d '\r' | awk -F': ' 'tolower($1)=="etag"{print $2}') \
        || fail "o envio do PDF do perfil à URL assinada falhou (MinIO acessível?)."
    como "a confirmação do documento do perfil" "$token" POST \
        "/api/autorizacao/perfis/$(jq -r '.perfilId' <<< "$envio")/versoes/$(jq -r '.versaoId' <<< "$envio")/documento/confirmacao" \
        "$(jq -nc --arg etag "${etag//\"/}" '{etag: $etag}')" >/dev/null
}

# O segundo gestor aprova a versão depois do verificador. $1=perfil $2=versão $3=nome
aprovar_versao_do_perfil() {
    aguardar_documento_do_perfil "$1" "$(token_de "${LISTA_DE_GESTORES[0]}" "$SENHA_GESTORES")"
    como "a aprovação da versão do perfil '$3' por ${LISTA_DE_GESTORES[1]}" "$(token_de "${LISTA_DE_GESTORES[1]}" "$SENHA_GESTORES")" POST \
        "/api/autorizacao/perfis/$1/versoes/$2/aprovacao" '{"etapa": "gestao_de_acessos"}' >/dev/null
}

# Retoma a versão pendente que uma execução interrompida deixou com o documento já enviado. $1=perfil $2=nome
retomar_versao_pendente() {
    local pendente
    pendente=$(como "a leitura do perfil '$2'" "$(token_de "${LISTA_DE_GESTORES[0]}" "$SENHA_GESTORES")" GET "/api/autorizacao/perfis/$1" \
        | jq -c '.versaoPendente // empty')
    case "$(jq -r '.situacaoDoDocumento // empty' <<< "$pendente")" in
        em_verificacao | verificado) aprovar_versao_do_perfil "$1" "$(jq -r '.versaoId' <<< "$pendente")" "$2" ;;
        *) fail "o perfil '$2' existe sem versão em vigor e sem documento enviado. Cancele a versão pendente na tela de perfis ou recrie a base." ;;
    esac
}

# Cria o perfil e o põe em vigor; devolve o id. $1=nome $2=permissões (JSON)
criar_perfil_do_roteiro() {
    local nome="$1" permissoes="$2" criador envio perfil
    criador=$(token_de "${LISTA_DE_GESTORES[0]}" "$SENHA_GESTORES")
    envio=$(como "a criação do perfil '$nome' por ${LISTA_DE_GESTORES[0]}" "$criador" POST "/api/autorizacao/perfis" \
        "$(jq -nc --arg n "$nome" --argjson p "$permissoes" --argjson t "$(stat -c%s "$PDF_DO_PERFIL")" \
            '{nome: $n, descricao: "Permissões que o roteiro local de processo de teste usa.", permissoes: $p, protocolo: "ROTEIRO-LOCAL-0001/2026", tamanhoDoDocumento: $t}')")
    enviar_documento_do_perfil "$envio" "$criador"
    perfil=$(jq -r '.perfilId' <<< "$envio")
    aprovar_versao_do_perfil "$perfil" "$(jq -r '.versaoId' <<< "$envio")" "$nome"
    printf '%s\n' "$perfil"
}

# O id do perfil do roteiro em vigor, criado se não existe. $1=nome $2=permissões (JSON)
perfil_do_roteiro() {
    local nome="$1" listado
    listado=$(como "a lista de perfis" "$(token_de "${LISTA_DE_GESTORES[0]}" "$SENHA_GESTORES")" GET "/api/autorizacao/perfis?limit=100" \
        | jq -c --arg n "$nome" 'first(.[] | select(.nome == $n)) // empty')
    if [ -z "$listado" ]; then
        criar_perfil_do_roteiro "$nome" "$2"
    elif ! jq -e '.ativo' <<< "$listado" >/dev/null; then
        fail "o perfil '$nome' existe, mas está desativado. Reative-o na tela de perfis ou recrie a base."
    else
        jq -e '.temVersaoVigente' <<< "$listado" >/dev/null || retomar_versao_pendente "$(jq -r '.id' <<< "$listado")" "$nome"
        jq -r '.id' <<< "$listado"
    fi
}

# O gestor que decide o pedido: nunca o próprio executor, que é parte dele.
gestor_que_decide() {
    local gestor
    for gestor in "${LISTA_DE_GESTORES[@]}"; do
        [ "$gestor" = "$ADMIN_USER" ] || { printf '%s\n' "$gestor"; return 0; }
    done
    fail "não há gestor de acessos, além do executor, para aprovar o pedido (GESTORES='$GESTORES')."
}

# O executor pede o perfil para si e um gestor aprova. $1=perfil $2=unidade (vazio: instituição) $3=nome
pedir_e_aprovar() {
    local perfil="$1" unidade="$2" nome="$3" corpo pedido gestor situacao
    corpo=$(jq -nc --arg p "$perfil" --arg u "$unidade" \
        '{beneficiarioEmail: null, perfilId: $p, unidadeId: (if $u == "" then null else $u end),
          justificativa: "Executar o roteiro local do processo de teste."}')
    pedido=$(como "o pedido do perfil '$nome' por $ADMIN_USER" "$TOKEN" POST "/api/autorizacao/pedidos-de-concessao" "$corpo" | jq -r '.id')
    gestor=$(gestor_que_decide)
    situacao=$(como "a aprovação do pedido do perfil '$nome' por $gestor" "$(token_de "$gestor" "$SENHA_GESTORES")" POST \
        "/api/autorizacao/pedidos-de-concessao/$pedido/aprovacoes" "" | jq -r '.situacao')
    [ "$situacao" = "aprovado" ] || fail "o pedido $pedido do perfil '$nome' ficou em '$situacao', não em 'aprovado'."
}

# $1=nome do perfil $2=permissões (JSON) $3=unidade (vazio: instituição)
garantir_perfil() {
    local nome="$1" permissoes="$2" unidade="${3:-}" escopo perfil
    escopo=${unidade:+"na unidade $unidade"}
    escopo=${escopo:-"na instituição"}
    if executor_tem_perfil "$nome" "$unidade"; then
        ok "$ADMIN_USER já tem o perfil '$nome' $escopo"
        return 0
    fi
    read -r -a LISTA_DE_GESTORES <<< "$GESTORES"
    [ "${#LISTA_DE_GESTORES[@]}" -eq 2 ] || fail "GESTORES deve ter exatamente dois gestores de acessos (recebido: '$GESTORES')."
    log "Concedendo a $ADMIN_USER o perfil '$nome' $escopo"
    perfil=$(perfil_do_roteiro "$nome" "$permissoes")
    # A espera pelo verificador pode passar da validade do token do executor.
    TOKEN=$(token_de "$ADMIN_USER" "$ADMIN_PASS")
    pedir_e_aprovar "$perfil" "$unidade" "$nome"
    executor_tem_perfil "$nome" "$unidade" || fail "o pedido foi aprovado, mas GET /api/profile/me de $ADMIN_USER não mostra o perfil '$nome' $escopo."
    ok "perfil '$nome' concedido $escopo"
}

garantir_acesso_institucional() {
    garantir_perfil "$PERFIL_INSTITUCIONAL" "$PERMISSOES_INSTITUCIONAIS"
}

# $1=id da unidade que conduz o processo
garantir_acesso_na_unidade() {
    garantir_perfil "$PERFIL_DA_UNIDADE" "$PERMISSOES_DA_UNIDADE" "$1"
}
