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
# Configuração) e o envio do catálogo à proteção de dados; o da unidade cobre a configuração e a publicação do processo e
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
# shellcheck source=perfis-pela-api.sh
source "$(dirname "${BASH_SOURCE[0]}")/perfis-pela-api.sh"

# Descrição do perfil criado; o roteiro dos perfis de operação a troca por perfil.
DESCRICAO_DO_PERFIL="${DESCRICAO_DO_PERFIL:-Permissões que o roteiro local de processo de teste usa.}"
PERFIL_INSTITUCIONAL="Roteiro de teste — cadastros institucionais"
PERFIL_DE_MOTIVOS="Roteiro de teste — motivos de decisão"
# O pedido do roteiro traz sempre esta justificativa, o que o distingue de um pedido de outra pessoa para o executor.
JUSTIFICATIVA_DO_PEDIDO="Executar o roteiro local do processo de teste."
PERMISSOES_INSTITUCIONAIS='[
    "publicacoes:tipos-ato:manter",
    "organizacao:instituicao:manter", "organizacao:unidades:manter",
    "configuracao:campi:manter", "configuracao:locais-oferta:manter",
    "configuracao:cursos:manter", "configuracao:ofertas-curso:manter",
    "configuracao:referencias-reserva-demografica:manter",
    "configuracao:condicoes-atendimento:manter", "configuracao:recursos-acessibilidade:manter",
    "configuracao:tipos-deficiencia:manter", "configuracao:base-legal-do-fada:manter",
    "configuracao:calendarios-dias-uteis:manter", "configuracao:pesos-area-enem:manter",
    "configuracao:fatos-candidato:manter", "configuracao:tipos-documento:manter",
    "configuracao:modelos-formulario:consultar"
]'
PERFIL_DA_UNIDADE="Roteiro de teste — processo seletivo da unidade"
PERMISSOES_DA_UNIDADE='["configuracao:modelos-formulario:consultar-na-unidade", "selecao:processos:configurar", "selecao:processos:publicar"]'

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

# As esperas pelo verificador de arquivos podem passar da validade do token do executor (300 s no realm local):
# quem chama depois de uma espera renova antes.
renovar_token_do_executor() {
    TOKEN=$(token_de "$ADMIN_USER" "$ADMIN_PASS") || fail "não foi possível renovar o token do executor."
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
        [ "$situacao" = "VERIFICADO" ] && return 0
        sleep 3
    done
    fail "o documento do perfil $perfil não foi verificado em ${ESPERA_VERIFICACAO}s (situação: '${situacao:-desconhecida}'). Confira o serviço 'verificador'."
}

# A etapa em que a versão pendente do perfil espera decisão (vazia se não há versão pendente). $1=perfil
etapa_pendente_do_perfil() {
    detalhe_do_perfil_do_gestor "$1" | jq -r '.versaoPendente.etapaAtual // empty'
}

# O segundo gestor aprova a versão depois do verificador. Se o perfil é protegido (traz dado pessoal), a versão ainda
# espera a aprovação da proteção de dados, que o primeiro Representante do controlador dá. Cada etapa só é decidida se
# ainda espera decisão, para que uma execução interrompida entre as duas possa continuar. $1=perfil $2=versão $3=nome
aprovar_versao_do_perfil() {
    local etapa
    aguardar_documento_do_perfil "$1" "$(token_de "${LISTA_DE_GESTORES[0]}" "$SENHA_GESTORES")" || return 1
    etapa=$(etapa_pendente_do_perfil "$1") || fail "não foi possível consultar a etapa pendente do perfil $1."
    if [ "$etapa" = GESTAO_DE_ACESSOS ]; then
        como "a aprovação da versão do perfil '$3' por ${LISTA_DE_GESTORES[1]}" "$(token_de "${LISTA_DE_GESTORES[1]}" "$SENHA_GESTORES")" POST \
            "/api/autorizacao/perfis/$1/versoes/$2/aprovacao" '{"etapa": "GESTAO_DE_ACESSOS"}' >/dev/null || return 1
        etapa=$(etapa_pendente_do_perfil "$1") || fail "não foi possível consultar a etapa pendente do perfil $1."
    fi
    if [ "$etapa" = PROTECAO_DE_DADOS ]; then
        [ "${#LISTA_DE_REPRESENTANTES[@]}" -gt 0 ] || fail "a versão do perfil '$3' espera a proteção de dados, e não há Representante do controlador designado."
        como "a aprovação da proteção de dados da versão do perfil '$3' por ${LISTA_DE_REPRESENTANTES[0]}" \
            "$(token_de "${LISTA_DE_REPRESENTANTES[0]}" "$SENHA_REPRESENTANTES")" POST \
            "/api/autorizacao/perfis/$1/versoes/$2/aprovacao" '{"etapa": "PROTECAO_DE_DADOS"}' >/dev/null || return 1
    fi
}

# O detalhe do perfil, lido com o token do primeiro gestor. $1=perfil
detalhe_do_perfil_do_gestor() {
    detalhe_do_perfil "$(token_de "${LISTA_DE_GESTORES[0]}" "$SENHA_GESTORES")" "$1"
}

# Retoma a versão pendente que uma execução interrompida deixou com o documento já enviado, a partir do detalhe do perfil
# que o chamador já leu. $1=perfil $2=nome $3=detalhe do perfil
retomar_versao_pendente() {
    local pendente
    pendente=$(jq -c '.versaoPendente // empty' <<< "$3")
    case "$(jq -r '.situacaoDoDocumento // empty' <<< "$pendente")" in
        EM_VERIFICACAO | VERIFICADO) aprovar_versao_do_perfil "$1" "$(jq -r '.versaoId' <<< "$pendente")" "$2" ;;
        *) fail "o perfil '$2' existe sem versão em vigor e sem documento enviado. Cancele a versão pendente na tela de perfis ou recrie a base." ;;
    esac
}

# Cria o perfil e o põe em vigor; devolve o id. $1=nome $2=permissões (JSON)
criar_perfil_do_roteiro() {
    local nome="$1" permissoes="$2" criador envio perfil
    criador=$(token_de "${LISTA_DE_GESTORES[0]}" "$SENHA_GESTORES") || return 1
    envio=$(como "a criação do perfil '$nome' por ${LISTA_DE_GESTORES[0]}" "$criador" POST "/api/autorizacao/perfis" \
        "$(jq -nc --arg n "$nome" --arg d "$DESCRICAO_DO_PERFIL" --argjson p "$permissoes" --argjson t "$(stat -c%s "$PDF_DO_PERFIL")" \
            '{nome: $n, descricao: $d, permissoes: $p, protocolo: "ROTEIRO-LOCAL-0001/2026", tamanhoDoDocumento: $t}')") || return 1
    enviar_e_confirmar_documento "$criador" "$envio" "$PDF_DO_PERFIL" || return 1
    perfil=$(jq -r '.perfilId' <<< "$envio")
    aprovar_versao_do_perfil "$perfil" "$(jq -r '.versaoId' <<< "$envio")" "$nome" || return 1
    printf '%s\n' "$perfil"
}

# Confere a composição e acrescenta as permissões ausentes sem retirar as existentes.
# $1=perfil $2=nome $3=permissões pedidas $4=detalhe
atualizar_permissoes_do_perfil() {
    local id="$1" nome="$2" pedidas="$3" detalhe="$4" permissoes criador envio
    permissoes=$(jq -ce --argjson pedidas "$pedidas" \
        '(.composicaoEmVigor.permissoes | map(.codigo)) as $atuais | ($atuais + $pedidas | unique)' <<< "$detalhe") \
        || fail "não foi possível ler as permissões do perfil '$nome'."
    if ! jq -e --argjson p "$pedidas" '(.composicaoEmVigor.permissoes | map(.codigo)) as $atuais | ($p - $atuais | length) == 0' \
        <<< "$detalhe" >/dev/null; then
        if jq -e '.versaoPendente != null' <<< "$detalhe" >/dev/null; then
            jq -e --argjson p "$permissoes" \
                '(.versaoPendente.permissoes | map(.codigo)) as $pendentes | .versaoPendente.propostaPorQuemLe and (($p - $pendentes | length) == 0)' \
                <<< "$detalhe" >/dev/null \
                || fail "o perfil '$nome' tem uma versão pendente de outra proposta ou sem todas as permissões necessárias; resolva-a na tela de perfis."
            retomar_versao_pendente "$id" "$nome" "$detalhe" || return 1
        else
            criador=$(token_de "${LISTA_DE_GESTORES[0]}" "$SENHA_GESTORES") || return 1
            envio=$(como "a nova versão do perfil '$nome'" "$criador" POST "/api/autorizacao/perfis/$id/versoes" \
                "$(jq -nc --arg n "$nome" --arg d "$(jq -r '.descricao' <<< "$detalhe")" --argjson p "$permissoes" --argjson t "$(stat -c%s "$PDF_DO_PERFIL")" \
                    '{nome: $n, descricao: $d, permissoes: $p, protocolo: "ROTEIRO-LOCAL-0001/2026", tamanhoDoDocumento: $t}')") || return 1
            enviar_e_confirmar_documento "$criador" "$envio" "$PDF_DO_PERFIL" || return 1
            aprovar_versao_do_perfil "$id" "$(jq -r '.versaoId' <<< "$envio")" "$nome" || return 1
        fi
        detalhe=$(detalhe_do_perfil_do_gestor "$id") || return 1
        jq -e --argjson p "$pedidas" '(.composicaoEmVigor.permissoes | map(.codigo)) as $atuais | ($p - $atuais | length) == 0' \
            <<< "$detalhe" >/dev/null || fail "a versão em vigor do perfil '$nome' ainda não contém as permissões necessárias."
    fi
    # O acréscimo abre revisão e bloqueia novos pedidos. Encerra-a, revogando as concessões não migradas;
    # o executor solicita a versão atual logo depois. Também permite retomar uma execução interrompida.
    if jq -e '.revisaoDasConcessoes.situacao == "aberta"' <<< "$detalhe" >/dev/null; then
        como "o encerramento da revisão das concessões do perfil '$nome'" \
            "$(token_de "${LISTA_DE_GESTORES[0]}" "$SENHA_GESTORES")" POST \
            "/api/autorizacao/perfis/$id/revisao-das-concessoes/encerramento" \
            '{"motivo":"Atualização do perfil do roteiro local de teste.","confirmacaoDeExclusao":true}' >/dev/null \
            || fail "não foi possível encerrar a revisão das concessões do perfil '$nome'."
    fi
}

# O id do perfil do roteiro em vigor, criado se não existe. $1=nome $2=permissões (JSON)
perfil_do_roteiro() {
    local nome="$1" listado
    listado=$(como "a lista de perfis" "$(token_de "${LISTA_DE_GESTORES[0]}" "$SENHA_GESTORES")" GET "/api/autorizacao/perfis?limit=100" \
        | jq -c --arg n "$nome" 'first(.[] | select(.nome == $n)) // empty') \
        || fail "não foi possível consultar a lista de perfis para o roteiro."
    if [ -z "$listado" ]; then
        criar_perfil_do_roteiro "$nome" "$2"
    elif ! jq -e '.ativo' <<< "$listado" >/dev/null; then
        fail "o perfil '$nome' existe, mas está desativado. Reative-o na tela de perfis ou recrie a base."
    else
        local id detalhe
        id=$(jq -r '.id' <<< "$listado")
        detalhe=$(detalhe_do_perfil_do_gestor "$id") || return 1
        if jq -e '.composicaoEmVigor == null' <<< "$detalhe" >/dev/null; then
            retomar_versao_pendente "$id" "$nome" "$detalhe" || return 1
            detalhe=$(detalhe_do_perfil_do_gestor "$id") || return 1
        fi
        atualizar_permissoes_do_perfil "$id" "$nome" "$2" "$detalhe" || return 1
        printf '%s\n' "$id"
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

# O pedido do executor, para ele mesmo, que aguarda a decisão do gestor para o perfil e a unidade, de uma execução
# interrompida: pedir de novo recebe 409 (pedido duplicado). Vazio quando não há. $1=perfil $2=unidade (vazio: instituição)
pedido_em_andamento_do_executor() {
    local email
    email=$(como "a leitura do executor" "$TOKEN" GET "/api/profile/me" | jq -r '.email')
    como "a lista dos pedidos de $ADMIN_USER" "$TOKEN" GET "/api/autorizacao/pedidos-de-concessao?fila=meus&situacao=AGUARDANDO_GESTOR&limit=100" \
        | jq -r --arg p "$1" --arg u "$2" --arg e "$email" --arg j "$JUSTIFICATIVA_DO_PEDIDO" \
            'first(.[] | select(.perfil.id == $p and (.escopo.unidadeId // "") == $u and (.beneficiario.email | ascii_downcase) == ($e | ascii_downcase)
                                and .justificativa == $j and .situacao == "AGUARDANDO_GESTOR")) | .id // empty'
}

# O executor pede o perfil para si e um gestor aprova. $1=perfil $2=unidade (vazio: instituição) $3=nome
pedir_e_aprovar() {
    local perfil="$1" unidade="$2" nome="$3" corpo pedido gestor situacao
    corpo=$(jq -nc --arg p "$perfil" --arg u "$unidade" --arg j "$JUSTIFICATIVA_DO_PEDIDO" \
        '{beneficiarioEmail: null, perfilId: $p, unidadeId: (if $u == "" then null else $u end),
          justificativa: $j}')
    pedido=$(pedido_em_andamento_do_executor "$perfil" "$unidade")
    if [ -z "$pedido" ]; then
        pedido=$(como "o pedido do perfil '$nome' por $ADMIN_USER" "$TOKEN" POST "/api/autorizacao/pedidos-de-concessao" "$corpo" | jq -r '.id')
    fi
    gestor=$(gestor_que_decide) || fail "não foi possível escolher um gestor para aprovar o perfil '$nome'."
    situacao=$(como "a aprovação do pedido do perfil '$nome' por $gestor" "$(token_de "$gestor" "$SENHA_GESTORES")" POST \
        "/api/autorizacao/pedidos-de-concessao/$pedido/aprovacoes" "" | jq -r '.situacao')
    [ "$situacao" = "APROVADO" ] || fail "o pedido $pedido do perfil '$nome' ficou em '$situacao', não em 'aprovado'."
}

# $1=nome do perfil $2=permissões (JSON) $3=unidade (vazio: instituição)
garantir_perfil() {
    local nome="$1" permissoes="$2" unidade="${3:-}" escopo perfil
    escopo=${unidade:+"na unidade $unidade"}
    escopo=${escopo:-"na instituição"}
    read -r -a LISTA_DE_GESTORES <<< "$GESTORES"
    [ "${#LISTA_DE_GESTORES[@]}" -eq 2 ] || fail "GESTORES deve ter exatamente dois gestores de acessos (recebido: '$GESTORES')."
    log "Concedendo a $ADMIN_USER o perfil '$nome' $escopo"
    perfil=$(perfil_do_roteiro "$nome" "$permissoes") \
        || fail "não foi possível preparar o perfil '$nome' para o roteiro."
    renovar_token_do_executor
    if executor_tem_perfil "$nome" "$unidade"; then
        ok "$ADMIN_USER já tem o perfil '$nome' $escopo"
        return 0
    fi
    pedir_e_aprovar "$perfil" "$unidade" "$nome"
    renovar_token_do_executor
    executor_tem_perfil "$nome" "$unidade" || fail "o pedido foi aprovado, mas GET /api/profile/me de $ADMIN_USER não mostra o perfil '$nome' $escopo."
    ok "perfil '$nome' concedido $escopo"
}

garantir_acesso_institucional() {
    garantir_perfil "$PERFIL_INSTITUCIONAL" "$PERMISSOES_INSTITUCIONAIS"
    garantir_perfil "$PERFIL_DE_MOTIVOS" '["selecao:motivos-de-decisao:manter"]'
}

# $1=id da unidade que conduz o processo
garantir_acesso_na_unidade() {
    garantir_perfil "$PERFIL_DA_UNIDADE" "$PERMISSOES_DA_UNIDADE" "$1"
}
