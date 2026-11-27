#!/usr/bin/env bash
# perfis-de-operacao.sh — cria, pela API, os perfis de operação, as unidades CEPS e CRCA e as concessões às personas
# fictícias da base local pronta (docker/base-local). É o que dá à base os perfis e as concessões sem depender de uma
# geração anterior dos dados: ver "Gerar de novo a base local" em docs/setup-ambiente-local.md.
#
# Os perfis (nome, descrição, alcance e permissões) estão em scripts/perfis-de-operacao.json. Cada perfil nasce pelo mesmo
# caminho de uma pessoa: o primeiro gestor de acessos o cria e envia o PDF que o institui, o segundo gestor aprova a versão
# e, se o perfil é protegido, um Representante do controlador aprova a proteção de dados. Cada concessão é pedida pela
# própria pessoa, que aceita o termo de responsabilidade; o gestor de acessos a aprova e, se o perfil é protegido, o outro
# Representante do controlador decide a segunda etapa. Nada é gravado direto no banco. Rodar de novo não repete o que já existe.
#
# Pré-requisitos: ambiente local do docker compose no ar, com o serviço ambiente-init concluído (gestores de acessos
# instalados, 60 personas no realm), jq e curl.
#
# Uso:
#   scripts/perfis-de-operacao.sh
#
# Variáveis de ambiente opcionais: as de roteiro-processo-teste-inscricao.sh (KC_URL, KC_REALM, API_URL, ADMIN_PASS), mais
#   ADMIN_USER   (default: diogo.souza) — gestor de acessos que cria as unidades e os perfis
#   GESTORES, REPRESENTANTES, SENHA_GESTORES, SENHA_REPRESENTANTES, ESPERA_VERIFICACAO — como em acessos-do-roteiro.sh
#                e governanca-do-roteiro.sh

set -euo pipefail

ADMIN_USER="${ADMIN_USER:-diogo.souza}"
PERFIS_DE_OPERACAO="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/perfis-de-operacao.json"

# shellcheck source=roteiro-processo-teste-inscricao.sh
source "$(dirname "${BASH_SOURCE[0]}")/roteiro-processo-teste-inscricao.sh"

declare -A PERFIL_ID

# Cria cada perfil do arquivo (ou reaproveita o que já existe) e guarda o id pelo nome.
criar_perfis_de_operacao() {
    local i total nome
    total=$(jq length "$PERFIS_DE_OPERACAO")
    for ((i = 0; i < total; i++)); do
        nome=$(jq -r ".[$i].nome" "$PERFIS_DE_OPERACAO")
        DESCRICAO_DO_PERFIL=$(jq -r ".[$i].descricao" "$PERFIS_DE_OPERACAO")
        log "Perfil '$nome'"
        PERFIL_ID[$nome]=$(perfil_do_roteiro "$nome" "$(jq -c ".[$i].permissoes" "$PERFIS_DE_OPERACAO")")
        ok "perfil '$nome' em vigor"
    done
}

# O id de um perfil que a própria migration cria. $1=nome
perfil_da_migration() {
    renovar_token_do_executor
    como "a lista de perfis para pedido" "$TOKEN" GET "/api/autorizacao/perfis-para-pedido" \
        | jq -r --arg n "$1" 'first(.[] | select(.nome == $n)) | .id // empty'
}

# A pessoa já tem o perfil no escopo? A leitura que falha interrompe o roteiro; só a resposta válida sem o perfil dá "não".
# $1=token da pessoa $2=perfil $3=unidade (vazio: instituição)
pessoa_tem_perfil() {
    local resposta
    resposta=$(como "a leitura dos perfis da pessoa" "$1" GET "/api/profile/me")
    jq -e '.perfis | type == "array"' >/dev/null <<< "$resposta" || fail "a leitura dos perfis da pessoa não trouxe a lista de perfis."
    jq -e --arg n "$2" --arg u "$3" \
        'any(.perfis[]; .nome == $n and (if $u == "" then .escopoTipo.codigo == "INSTITUICAO" else .unidadeId == $u end))' \
        >/dev/null <<< "$resposta"
}

# O pedido ainda aberto da pessoa para o perfil e a unidade, se uma execução anterior parou no meio.
# $1=token $2=id do perfil $3=unidade (vazio: instituição)
pedido_aberto_da_pessoa() {
    como "a lista dos pedidos da pessoa" "$1" GET "/api/autorizacao/pedidos-de-concessao?fila=meus" \
        | jq -r --arg p "$2" --arg u "$3" 'first(.[] | select(.perfil.id == $p and (.situacao.codigo | test("^AGUARDANDO_"))
            and (if $u == "" then .escopo.unidadeId == null else .escopo.unidadeId == $u end))) | .id // empty'
}

# Pede o perfil para a pessoa. O termo de responsabilidade só entra no pedido de perfil protegido. $1=token $2=perfil $3=unidade $4=termo
pedir_perfil() {
    local token="$1" perfil="$2" unidade="$3" termo="$4" protegido
    protegido=$(como "os perfis para pedido" "$token" GET "/api/autorizacao/perfis-para-pedido" \
        | jq -r --arg p "$perfil" 'first(.[] | select(.id == $p)) | .exigeAprovacaoDeProtecaoDeDados')
    como "o pedido do perfil" "$token" POST "/api/autorizacao/pedidos-de-concessao" \
        "$(jq -nc --arg p "$perfil" --arg u "$unidade" --arg t "$termo" --argjson protegido "$protegido" \
            '{beneficiarioEmail: null, perfilId: $p, unidadeId: (if $u == "" then null else $u end), anexaraDocumento: false,
              justificativa: "Perfil de teste da base local de desenvolvimento."}
             + (if $protegido then {termoVersaoApresentadaId: $t} else {} end)')" | jq -r '.id'
}

# Dá ao pedido o passo que a situação dele espera. $1=pessoa $2=token da pessoa $3=pedido $4=situação $5=termo
avancar_pedido_de_perfil() {
    local pessoa="$1" token="$2" pedido="$3" situacao="$4" termo="$5" gestor g
    case "$situacao" in
        AGUARDANDO_DOCUMENTO) anexar_designacao "$pedido" "$token" ;;
        AGUARDANDO_BENEFICIARIO)
            como "o aceite do termo" "$token" POST "/api/autorizacao/pedidos-de-concessao/$pedido/aceite" \
                "$(jq -nc --arg t "$termo" '{termoVersaoApresentadaId: $t}')" >/dev/null ;;
        AGUARDANDO_GESTOR)
            gestor=""
            for g in "${LISTA_DE_GESTORES[@]}"; do
                [ "$g" != "$pessoa" ] && { gestor="$g"; break; }
            done
            como "a aprovação do gestor" "$(token_de "$gestor" "$SENHA_GESTORES")" POST \
                "/api/autorizacao/pedidos-de-concessao/$pedido/aprovacoes" "" >/dev/null ;;
        AGUARDANDO_PROTECAO_DE_DADOS)
            como "a decisão do Representante" "$(token_de "$(representante_que_decide "$pessoa")" "$SENHA_REPRESENTANTES")" POST \
                "/api/autorizacao/pedidos-de-concessao/$pedido/aprovacoes" "" >/dev/null ;;
        *) fail "o pedido de $pessoa ficou em '$situacao'." ;;
    esac
}

# A própria pessoa pede o perfil; os passos seguintes são os de uma concessão real. $1=pessoa $2=perfil (nome) $3=unidade (vazio: instituição)
conceder_perfil() {
    local pessoa="$1" nome="$2" unidade="${3:-}" token termo pedido situacao
    token=$(token_de "$pessoa" "$ADMIN_PASS")
    if pessoa_tem_perfil "$token" "$nome" "$unidade"; then
        ok "$pessoa já tem '$nome'"
        return 0
    fi
    termo=$(como "o termo vigente" "$token" GET "/api/autorizacao/termos-de-responsabilidade/vigente" | jq -r '.id')
    pedido=$(pedido_aberto_da_pessoa "$token" "${PERFIL_ID[$nome]}" "$unidade")
    [ -n "$pedido" ] || pedido=$(pedir_perfil "$token" "${PERFIL_ID[$nome]}" "$unidade" "$termo")
    for _ in 1 2 3 4 5 6; do
        situacao=$(como "a leitura do pedido" "$token" GET "/api/autorizacao/pedidos-de-concessao/$pedido" | jq -r '.situacao.codigo')
        if [ "$situacao" = APROVADO ]; then
            ok "$pessoa: '$nome' concedido"
            return 0
        fi
        avancar_pedido_de_perfil "$pessoa" "$token" "$pedido" "$situacao" "$termo"
    done
    fail "o pedido de '$nome' de $pessoa não chegou a 'aprovado' (última situação: '$situacao')."
}

# As concessões institucionais do administrador. Ele precisa delas antes de criar as unidades: sem o perfil Organização
# institucional, a criação da unidade é recusada.
conceder_perfis_da_instituicao() {
    conceder_perfil "$ADMIN_USER" "Administração dos cadastros" ""
    conceder_perfil "$ADMIN_USER" "Organização institucional" ""
    conceder_perfil "$ADMIN_USER" "Publicações" ""
    PERFIL_ID["Gestor de proteção de dados"]=$(perfil_da_migration "Gestor de proteção de dados")
    exigir_id "o perfil Gestor de proteção de dados" "${PERFIL_ID[Gestor de proteção de dados]}"
    conceder_perfil marlene.martins "Gestor de proteção de dados" ""
}

# As concessões nas unidades. $1=unidade CEPS $2=unidade CRCA
conceder_perfis_das_unidades() {
    local ceps="$1" crca="$2"
    conceder_perfil betina.neves "Coordenação de processo seletivo" "$ceps"
    conceder_perfil isabella.figueiredo "Equipe de inscrições" "$ceps"
    conceder_perfil kevin.carvalho "Equipe de inscrições" "$ceps"
    conceder_perfil kevin.carvalho "Intercâmbio de dados com o INEP" "$ceps"
    conceder_perfil helena.rezende "Equipe de inscrições" "$ceps"
    conceder_perfil helena.rezende "Dados e documentos sensíveis" "$ceps"
    conceder_perfil erick.ramos "Consulta sem dados pessoais" "$ceps"
    conceder_perfil breno.novaes "Equipe de inscrições" "$crca"
}

main_perfis() {
    local ceps crca
    for cmd in jq curl; do command -v "$cmd" >/dev/null 2>&1 || fail "comando ausente: $cmd"; done
    obter_token
    read -r -a LISTA_DE_GESTORES <<< "$GESTORES"
    garantir_representantes
    criar_perfis_de_operacao
    conceder_perfis_da_instituicao
    renovar_token_do_executor
    ceps=$(criar_unidade CEPS "Centro de Processos Seletivos" CENTRO false)
    crca=$(criar_unidade CRCA "Centro de Registro e Controle Acadêmico" CENTRO false)
    conceder_perfis_das_unidades "$ceps" "$crca"
    ok "perfis de operação e concessões prontos"
}

# Executado direto, roda o roteiro; lido com `source`, só expõe as funções.
if [[ "${BASH_SOURCE[0]}" == "$0" ]]; then
    main_perfis
fi
