#!/usr/bin/env bash
# governanca-do-roteiro.sh — prepara, pela API, a governança que o roteiro de
# processo de teste precisa numa base nova: designa dois Representantes do
# controlador e faz o primeiro aprovar a proteção de dados do catálogo de tipos
# de documento e de fatos do candidato. Lido com `source` por
# roteiro-processo-teste-inscricao.sh, depois de acessos-do-roteiro.sh; não
# roda sozinho.
#
# Sem Representante designado, ninguém aprova a proteção do catálogo, e aplicar
# o modelo de inscrição responde 422 (fato com proteção pendente).
#
# Designação, para cada Representante (ADR-0162):
#   1. se não há termo de responsabilidade vigente, o primeiro gestor publica um;
#   2. a pessoa pede o perfil de Representante para si, na instituição, e anexa
#      o PDF fictício de designação, que passa pelo verificador de arquivos;
#   3. ela aceita o termo fixado no pedido;
#   4. um gestor aprova. Na designação do primeiro Representante a segunda etapa
#      é dispensada (UNI-REQ-0370); na do segundo, o primeiro a decide.
# São duas pessoas distintas, para que a revogação de um deles ainda deixe um
# Representante vigente.
#
# Proteção do catálogo: o executor envia à proteção de dados os itens
# pendentes, e o primeiro Representante aprova cada item enviado, com a
# classificação, a hipótese legal e a finalidade que o item já declara.
#
# Variáveis de ambiente opcionais, além das do roteiro e de acessos-do-roteiro.sh:
#   REPRESENTANTES   (default: "betina.neves isabella.figueiredo") — duas
#                    personas fictícias do portal, nenhuma delas gestor
#   SENHA_REPRESENTANTES (default: $ADMIN_PASS)

REPRESENTANTES="${REPRESENTANTES:-betina.neves isabella.figueiredo}"
SENHA_REPRESENTANTES="${SENHA_REPRESENTANTES:-$ADMIN_PASS}"
PERFIL_REPRESENTANTE_ID="019a0000-0000-7000-8000-000000000002"
PDF_DE_DESIGNACAO="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/edital-de-teste.pdf"

# Envia o PDF à URL assinada e devolve o ETag sem aspas. $1=URL
enviar_pdf() {
    local -a redirecionamento=()
    local etag
    [ -z "${ARMAZENAMENTO_CONNECT_TO:-}" ] || redirecionamento=(--connect-to "$ARMAZENAMENTO_CONNECT_TO")
    etag=$(curl -fsS -D - -o /dev/null -X PUT -H "Content-Type: application/pdf" ${redirecionamento[@]+"${redirecionamento[@]}"} \
        --data-binary @"$PDF_DE_DESIGNACAO" "$1" | tr -d '\r' | awk -F': ' 'tolower($1)=="etag"{print $2}') \
        || fail "o envio do PDF de designação à URL assinada falhou (MinIO acessível?)."
    printf '%s\n' "${etag//\"/}"
}

eh_representante() {
    como "a leitura dos perfis de $1" "$(token_de "$1" "$SENHA_REPRESENTANTES")" GET "/api/profile/me" \
        | jq -e --arg p "$PERFIL_REPRESENTANTE_ID" 'any(.perfis[]; .perfilId == $p)' >/dev/null
}

# A versão vigente do termo; publicada pelo primeiro gestor se não há nenhuma.
garantir_termo_vigente() {
    local gestor status
    gestor=$(token_de "${LISTA_DE_GESTORES[0]}" "$SENHA_GESTORES")
    status=$(curl -s -o /dev/null -w '%{http_code}' -H "Authorization: Bearer $gestor" "$API_URL/api/autorizacao/termos-de-responsabilidade/vigente")
    [ "$status" = 404 ] || return 0
    log "Publicando o termo de responsabilidade do ambiente local (${LISTA_DE_GESTORES[0]})"
    como "a publicação do termo de responsabilidade" "$gestor" POST "/api/autorizacao/termos-de-responsabilidade/versoes" \
        "$(jq -nc --arg t "$(printf 'Termo de responsabilidade fictício do ambiente local de desenvolvimento. %.0s' {1..5})" \
            '{texto: $t, ato: "Portaria fictícia do ambiente local nº 1/2026", versaoVigenteEsperadaId: null}')" >/dev/null
}

# O pedido ativo de Representante da pessoa, criado se não existe; devolve o id. $1=token da pessoa
pedido_de_representante() {
    local token="$1" id termo
    id=$(como "a lista dos pedidos" "$token" GET "/api/autorizacao/pedidos-de-concessao?fila=meus" \
        | jq -r --arg p "$PERFIL_REPRESENTANTE_ID" \
            'first(.[] | select(.perfil.id == $p and (.situacao | test("^aguardando_")))) | .id // empty')
    if [ -z "$id" ]; then
        termo=$(como "a leitura do termo vigente" "$token" GET "/api/autorizacao/termos-de-responsabilidade/vigente" | jq -r '.id')
        id=$(como "o pedido de Representante do controlador" "$token" POST "/api/autorizacao/pedidos-de-concessao" \
            "$(jq -nc --arg p "$PERFIL_REPRESENTANTE_ID" --arg t "$termo" \
                '{beneficiarioEmail: null, perfilId: $p, unidadeId: null, anexaraDocumento: true, termoVersaoApresentadaId: $t,
                  justificativa: "Designação fictícia para o ambiente local de desenvolvimento."}')" | jq -r '.id')
    fi
    printf '%s\n' "$id"
}

# Anexa o PDF de designação e espera o verificador liberar o pedido. $1=pedido $2=token da pessoa
anexar_designacao() {
    local pedido="$1" token="$2" envio etag fim=$((SECONDS + ESPERA_VERIFICACAO))
    envio=$(como "o anexo da designação" "$token" POST "/api/autorizacao/pedidos-de-concessao/$pedido/anexos" \
        "$(jq -nc --argjson t "$(stat -c%s "$PDF_DE_DESIGNACAO")" '{tamanhoDoPdf: $t}')")
    etag=$(enviar_pdf "$(jq -r '.urlDeEnvio' <<< "$envio")")
    como "a confirmação do anexo da designação" "$token" POST \
        "/api/autorizacao/pedidos-de-concessao/$pedido/anexos/$(jq -r '.id' <<< "$envio")/confirmacao" \
        "$(jq -nc --arg e "$etag" '{etag: $e}')" >/dev/null
    while [ "$SECONDS" -lt "$fim" ]; do
        [ "$(situacao_do_pedido "$pedido" "$token")" != aguardando_documento ] && return 0
        sleep 3
    done
    fail "o documento de designação do pedido $pedido não foi verificado em ${ESPERA_VERIFICACAO}s. Confira o serviço 'verificador'."
}

situacao_do_pedido() {
    como "a leitura do pedido $1" "$2" GET "/api/autorizacao/pedidos-de-concessao/$1" | jq -r '.situacao'
}

aceitar_termo_do_pedido() {
    local termo
    termo=$(como "a leitura do pedido $1" "$2" GET "/api/autorizacao/pedidos-de-concessao/$1" | jq -r '.termo.versaoId')
    como "o aceite do termo no pedido $1" "$2" POST "/api/autorizacao/pedidos-de-concessao/$1/aceite" \
        "$(jq -nc --arg t "$termo" '{termoVersaoApresentadaId: $t}')" >/dev/null
}

# Decide a etapa que o pedido aguarda. $1=pedido $2=quem decide $3=senha de quem decide
decidir_pedido() {
    como "a aprovação do pedido $1 por $2" "$(token_de "$2" "$3")" POST \
        "/api/autorizacao/pedidos-de-concessao/$1/aprovacoes" "" >/dev/null
}

# Leva o pedido até a concessão, retomando de onde uma execução anterior parou. $1=pedido $2=pessoa $3=token
avancar_pedido() {
    local pedido="$1" pessoa="$2" token="$3" situacao
    for _ in 1 2 3 4 5; do
        situacao=$(situacao_do_pedido "$pedido" "$token")
        case "$situacao" in
            aprovado) return 0 ;;
            aguardando_documento) anexar_designacao "$pedido" "$token" ;;
            aguardando_beneficiario) aceitar_termo_do_pedido "$pedido" "$token" ;;
            aguardando_gestor) decidir_pedido "$pedido" "${LISTA_DE_GESTORES[0]}" "$SENHA_GESTORES" ;;
            aguardando_protecao_de_dados) decidir_pedido "$pedido" "$(representante_que_decide "$pessoa")" "$SENHA_REPRESENTANTES" ;;
            *) fail "o pedido de Representante de $pessoa ficou em '$situacao'." ;;
        esac
    done
    fail "o pedido de Representante de $pessoa não chegou a 'aprovado' (última situação: '$situacao')."
}

# O Representante vigente da lista que decide a segunda etapa: nunca a própria pessoa, que é parte do pedido.
representante_que_decide() {
    local outro
    for outro in "${LISTA_DE_REPRESENTANTES[@]}"; do
        [ "$outro" != "$1" ] && eh_representante "$outro" && { printf '%s\n' "$outro"; return 0; }
    done
    fail "o pedido de $1 aguarda a proteção de dados, e nenhum outro Representante da lista ($REPRESENTANTES) está vigente para decidi-lo. Decida-o na tela de pedidos com o Representante vigente."
}

designar_representante() {
    local pessoa="$1" token
    if eh_representante "$pessoa"; then
        ok "$pessoa já é Representante do controlador"
        return 0
    fi
    log "Designando $pessoa Representante do controlador"
    token=$(token_de "$pessoa" "$SENHA_REPRESENTANTES")
    avancar_pedido "$(pedido_de_representante "$token")" "$pessoa" "$token"
    eh_representante "$pessoa" || fail "o pedido foi aprovado, mas GET /api/profile/me de $pessoa não mostra o perfil de Representante."
    ok "$pessoa designado Representante do controlador"
}

garantir_representantes() {
    read -r -a LISTA_DE_GESTORES <<< "$GESTORES"
    read -r -a LISTA_DE_REPRESENTANTES <<< "$REPRESENTANTES"
    [ "${#LISTA_DE_REPRESENTANTES[@]}" -eq 2 ] && [ "${LISTA_DE_REPRESENTANTES[0]}" != "${LISTA_DE_REPRESENTANTES[1]}" ] \
        || fail "REPRESENTANTES deve ter duas pessoas distintas (recebido: '$REPRESENTANTES')."
    garantir_termo_vigente
    designar_representante "${LISTA_DE_REPRESENTANTES[0]}"
    designar_representante "${LISTA_DE_REPRESENTANTES[1]}"
}

# Os itens da proteção do catálogo numa situação, como os vê o token. $1=token $2=situação
itens_da_protecao() {
    como "a lista da proteção do catálogo" "$1" GET "/api/configuracao/admin/protecao-do-catalogo?situacao=$2"
}

decidir_item_da_protecao() {
    local item="$1" token="$2"
    como "a aprovação da proteção de $(jq -r '.codigo' <<< "$item")" "$token" POST "/api/configuracao/admin/protecao-do-catalogo" \
        "$(jq -c '{cadastro: (if .tipo == "FATO" then "FATO" else "TIPO_DOCUMENTO" end), itemId: .id, decisao: "APROVAR",
                   classificacaoProtecao, hipoteseLegal, finalidadeTratamento,
                   ciclo: .aprovacao.ciclo, manifestacoesNoCiclo: .aprovacao.manifestacoesNoCiclo}' <<< "$item")" >/dev/null
}

garantir_protecao_do_catalogo() {
    local pendentes representante item solicitados
    pendentes=$(itens_da_protecao "$TOKEN" PENDENTE)
    if [ "$(jq 'length' <<< "$pendentes")" -gt 0 ]; then
        log "Enviando à proteção de dados $(jq 'length' <<< "$pendentes") itens do catálogo"
        como "o envio à proteção de dados" "$TOKEN" POST "/api/configuracao/admin/protecao-do-catalogo/solicitacoes" \
            "$(jq -c '{itens: map({cadastro: (if .tipo == "FATO" then "FATO" else "TIPO_DOCUMENTO" end), id})}' <<< "$pendentes")" >/dev/null
    fi
    representante=$(token_de "${LISTA_DE_REPRESENTANTES[0]}" "$SENHA_REPRESENTANTES")
    # A lista vem para uma variável antes do laço: falha dentro de <(...) não interromperia o roteiro.
    solicitados=$(itens_da_protecao "$representante" SOLICITADA)
    while read -r item; do
        [ -n "$item" ] && decidir_item_da_protecao "$item" "$representante"
    done < <(jq -c '.[]' <<< "$solicitados")
    ok "proteção do catálogo aprovada por ${LISTA_DE_REPRESENTANTES[0]}"
}
