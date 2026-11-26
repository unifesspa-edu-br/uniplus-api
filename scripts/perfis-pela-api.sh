#!/usr/bin/env bash
# perfis-pela-api.sh — leitura e proposta de perfis de acesso pela API, comuns aos roteiros de perfis. Lido com `source`
# depois de chamadas-a-api.sh; não roda sozinho. Toda função recebe o token da pessoa como parâmetro e nenhuma grava
# o token, a URL assinada de envio nem dado de pessoa.

# Todas as páginas de uma listagem, juntas num só vetor JSON; a página seguinte vem no cabeçalho Link. $1=token $2=caminho
listar_paginas() {
    local token="$1" caminho="$2" cabecalhos corpo proxima acumulado='[]'
    cabecalhos=$(mktemp)
    while [ -n "$caminho" ]; do
        corpo=$(curl -fsS -D "$cabecalhos" "$API_URL$caminho" -K <(printf 'header = "Authorization: Bearer %s"\n' "$token")) \
            || { rm -f "$cabecalhos"; fail "a leitura de $caminho falhou."; }
        acumulado=$(jq -c --argjson pagina "$corpo" '. + $pagina' <<< "$acumulado")
        proxima=$(tr -d '\r' < "$cabecalhos" | grep -i '^link:' | tr ',' '\n' | grep 'rel="next"' | sed -n 's/.*<[^>?]*?\([^>]*\)>.*/\1/p' || true)
        caminho=${proxima:+"${caminho%%\?*}?$proxima"}
    done
    rm -f "$cabecalhos"
    printf '%s\n' "$acumulado"
}

# O perfil da listagem com o nome dado; vazio quando não existe. $1=token $2=nome
perfil_pelo_nome() {
    listar_paginas "$1" "/api/autorizacao/perfis?limit=100" | jq -c --arg n "$2" 'first(.[] | select(.nome == $n)) // empty'
}

# O detalhe do perfil, conferido; interrompe se a resposta não é um detalhe de perfil. $1=token $2=perfil
detalhe_do_perfil() {
    local detalhe
    detalhe=$(como "a leitura do perfil $2" "$1" GET "/api/autorizacao/perfis/$2")
    jq -e 'type == "object" and has("composicaoEmVigor")' <<< "$detalhe" >/dev/null \
        || fail "a resposta da leitura do perfil $2 não é um detalhe de perfil válido."
    printf '%s\n' "$detalhe"
}

# As permissões que faltam e as que sobram na composição (em vigor, ou pendente se não há vigor) em relação às pedidas.
# $1=detalhe $2=permissões pedidas (JSON). Saída: um objeto {faltam: [...], sobram: [...]}.
diferenca_de_permissoes() {
    jq -c --argjson pedidas "$2" \
        '((.composicaoEmVigor // .versaoPendente // {permissoes: []}).permissoes | map(.codigo)) as $atuais
         | {faltam: ($pedidas - $atuais), sobram: ($atuais - $pedidas)}' <<< "$1"
}

# O mesmo, para a versão pendente, que não precisa ser a que está em vigor. $1=detalhe $2=permissões pedidas (JSON)
diferenca_da_versao_pendente() {
    jq -c --argjson pedidas "$2" \
        '(.versaoPendente.permissoes | map(.codigo)) as $atuais | {faltam: ($pedidas - $atuais), sobram: ($atuais - $pedidas)}' <<< "$1"
}

# A diferença que explica a situação do perfil: a da versão pendente quando é ela que diverge, a da composição nos demais
# casos. $1=detalhe $2=permissões pedidas (JSON) $3=situação (de situacao_do_perfil)
diferenca_da_situacao() {
    if [ "$3" = PENDENTE_DIFERENTE ]; then
        diferenca_da_versao_pendente "$1" "$2"
    else
        diferenca_de_permissoes "$1" "$2"
    fi
}

# Zero quando a diferença não tem permissão faltando nem sobrando. $1=diferença (JSON)
sem_diferenca() {
    jq -e '(.faltam | length) + (.sobram | length) == 0' <<< "$1" >/dev/null
}

# O que o roteiro faz com o perfil, pelo detalhe. $1=detalhe $2=permissões pedidas (JSON). Saída, uma palavra:
#   DESATIVADO, SUSPENSO, VIGENTE_IGUAL, VIGENTE_DIFERENTE, PENDENTE_COM_DOCUMENTO, PENDENTE_SEM_DOCUMENTO,
#   PENDENTE_DIFERENTE, ENCERRADA_EXPIRADA ou ENCERRADA_POR_DECISAO.
situacao_do_perfil() {
    local detalhe="$1"
    jq -e '.ativo | not' <<< "$detalhe" >/dev/null && { echo DESATIVADO; return; }
    jq -e '.suspensao != null' <<< "$detalhe" >/dev/null && { echo SUSPENSO; return; }
    if jq -e '.composicaoEmVigor != null' <<< "$detalhe" >/dev/null; then
        situacao_do_perfil_em_vigor "$detalhe" "$2"
        return
    fi
    if jq -e '.versaoPendente != null' <<< "$detalhe" >/dev/null; then
        situacao_da_versao_pendente "$detalhe" "$2"
        return
    fi
    jq -e '.ultimaVersaoEncerradaSemVigor.situacao == "EXPIRADA"' <<< "$detalhe" >/dev/null && echo ENCERRADA_EXPIRADA || echo ENCERRADA_POR_DECISAO
}

# A situação de um perfil com versão em vigor: as permissões em vigor e as da versão pendente, se houver, devem ser as
# do arquivo. $1=detalhe $2=permissões pedidas (JSON)
situacao_do_perfil_em_vigor() {
    sem_diferenca "$(diferenca_de_permissoes "$1" "$2")" || { echo VIGENTE_DIFERENTE; return; }
    if jq -e '.versaoPendente != null' <<< "$1" >/dev/null && ! sem_diferenca "$(diferenca_da_versao_pendente "$1" "$2")"; then
        echo PENDENTE_DIFERENTE
        return
    fi
    echo VIGENTE_IGUAL
}

# A situação de um perfil sem versão em vigor e com versão pendente. $1=detalhe $2=permissões pedidas
situacao_da_versao_pendente() {
    sem_diferenca "$(diferenca_de_permissoes "$1" "$2")" || { echo PENDENTE_DIFERENTE; return; }
    case "$(jq -r '.versaoPendente.situacaoDoDocumento' <<< "$1")" in
        EM_VERIFICACAO | VERIFICADO) echo PENDENTE_COM_DOCUMENTO ;;
        *) echo PENDENTE_SEM_DOCUMENTO ;;
    esac
}

# O corpo de uma proposta de perfil ou de versão. $1=nome $2=descrição $3=permissões (JSON) $4=protocolo $5=tamanho do PDF
corpo_da_proposta() {
    jq -nc --arg n "$1" --arg d "$2" --argjson p "$3" --arg c "$4" --argjson t "$5" \
        '{nome: $n, descricao: $d, permissoes: $p, protocolo: $c, tamanhoDoDocumento: $t}'
}

# Cria um perfil novo, com a primeira versão pendente; devolve o envio do documento. Argumentos: $1=token e os de corpo_da_proposta.
propor_perfil_novo() {
    como "a criação do perfil '$2'" "$1" POST "/api/autorizacao/perfis" "$(corpo_da_proposta "${@:2}")"
}

# Propõe nova versão de um perfil; devolve o envio do documento. $1=token $2=perfil e os de corpo_da_proposta.
propor_nova_versao() {
    como "a nova versão do perfil '$3'" "$1" POST "/api/autorizacao/perfis/$2/versoes" "$(corpo_da_proposta "${@:3}")" \
        | jq -c '.envio // .'
}

# Nova URL de envio do documento de uma versão pendente que ainda não o recebeu: outra URL para o documento que aguarda o
# envio ou a reserva de outro documento depois de a verificação recusar o anterior.
# $1=token $2=perfil $3=versão $4=tamanho $5=situação do documento
pedir_nova_url_do_documento() {
    local base="/api/autorizacao/perfis/$2/versoes/$3/documento"
    if [ "$5" = RECUSADO ]; then
        como "a reserva de outro documento do perfil $2" "$1" POST "$base" "$(jq -nc --argjson t "$4" '{tamanhoDoDocumento: $t}')"
    else
        como "a nova URL do documento do perfil $2" "$1" POST "$base/envio"
    fi
}

# Envia o PDF à URL assinada do envio e confirma o envio à API. A URL é credencial: só passa pelo arquivo de configuração
# do curl. ARMAZENAMENTO_CONNECT_TO (opcional, só na base local) redireciona o host do armazenamento.
# $1=token $2=envio (JSON) $3=caminho do PDF
enviar_e_confirmar_documento() {
    local envio="$2" url tipo etag
    local -a redirecionamento=()
    [ -z "${ARMAZENAMENTO_CONNECT_TO:-}" ] || redirecionamento=(--connect-to "$ARMAZENAMENTO_CONNECT_TO")
    url=$(jq -r '.urlDeEnvio' <<< "$envio")
    tipo=$(jq -r '.tipoDeMidia // "application/pdf"' <<< "$envio")
    etag=$(curl -fsS -D - -o /dev/null -X PUT -H "Content-Type: $tipo" ${redirecionamento[@]+"${redirecionamento[@]}"} --data-binary @"$3" \
        -K <(printf 'url = "%s"\n' "$url") \
        | tr -d '\r' | awk -F': ' 'tolower($1)=="etag"{print $2}') \
        || fail "o envio do documento à URL assinada falhou (o armazenamento está acessível desta máquina?)."
    [ -n "$etag" ] || fail "o armazenamento não devolveu a identificação do arquivo enviado."
    como "a confirmação do documento do perfil" "$1" POST \
        "/api/autorizacao/perfis/$(jq -r '.perfilId' <<< "$envio")/versoes/$(jq -r '.versaoId' <<< "$envio")/documento/confirmacao" \
        "$(jq -nc --arg etag "${etag//\"/}" '{etag: $etag}')" >/dev/null
}
