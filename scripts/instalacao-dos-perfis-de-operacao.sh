#!/usr/bin/env bash
# instalacao-dos-perfis-de-operacao.sh — roteiro de instalação: cria, pela API, os perfis de operação listados em
# scripts/perfis-de-operacao.json, com o documento que os institui, e depois confere o que ficou em vigor. Serve a
# homologação (com documento de teste) e a produção (com o documento real). Roda só quando a liderança técnica
# autorizar e é executado por ela; em produção, só com ordem expressa dela.
#
# O roteiro usa o mesmo caminho da tela de perfis. Ele não pede acesso, não aprova nada, não cria unidade e não grava
# no banco. Quem cria é um dos dois gestores de acessos instalados, que entra por código de dispositivo (abre o endereço
# mostrado no navegador e entra com a própria conta); o token fica só na memória deste processo e some ao fim.
# As aprovações das versões pendentes são feitas na tela pelo outro gestor de acessos e, nos perfis com dado pessoal ou
# sensível, também pelo Representante do controlador, em até 15 dias.
#
# Uso:
#   scripts/instalacao-dos-perfis-de-operacao.sh criar      cria as versões pendentes dos perfis que faltam
#   scripts/instalacao-dos-perfis-de-operacao.sh conferir   depois das aprovações, confere alcance e permissões em vigor
#
# Parâmetros obrigatórios, sem valor padrão (a liderança técnica os lê do cofre de segredos ou os informa):
#   API_URL      endereço da API, em https
#   KC_URL       endereço do Keycloak, em https, até o /auth quando houver
#   KC_REALM     realm do Keycloak
#   KC_CLIENT_ID cliente público do Keycloak com o login por código de dispositivo ligado
#   DOCUMENTO_INSTITUIDOR      caminho do PDF que institui os perfis (só em "criar")
#   PROTOCOLO_DO_DOCUMENTO     número do processo ou protocolo desse documento (só em "criar")
# Parâmetro opcional: ARQUIVO_DOS_PERFIS aponta outro arquivo de perfis (a prova em homologação usa um perfil de teste).
# Saída de "conferir": 0 tudo conforme; 1 diferença encontrada; 2 perfil ainda aguardando a aprovação pela tela.

set -euo pipefail

PERFIS_DE_OPERACAO="${ARQUIVO_DOS_PERFIS:-$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/perfis-de-operacao.json}"

# shellcheck source=chamadas-a-api.sh
source "$(dirname "${BASH_SOURCE[0]}")/chamadas-a-api.sh"
# shellcheck source=perfis-pela-api.sh
source "$(dirname "${BASH_SOURCE[0]}")/perfis-pela-api.sh"

ACCESS_TOKEN="" REFRESH_TOKEN="" TOKEN_EXPIRA_EM=0 RESULTADO_DO_PERFIL=0

limpar() { unset ACCESS_TOKEN REFRESH_TOKEN; }
trap limpar EXIT

exigir_parametro() {
    [ -n "${!1:-}" ] || fail "o parâmetro $1 é obrigatório e não tem valor padrão."
}

exigir_https() {
    case "${!1}" in
        https://*) ;;
        *) fail "o parâmetro $1 precisa ser um endereço https." ;;
    esac
}

conferir_parametros() {
    local nome
    for nome in API_URL KC_URL KC_REALM KC_CLIENT_ID; do exigir_parametro "$nome"; done
    exigir_https API_URL
    exigir_https KC_URL
    for cmd in jq curl; do command -v "$cmd" >/dev/null 2>&1 || fail "comando ausente: $cmd"; done
}

# Pede ao Keycloak o código de dispositivo e guarda o token da pessoa depois que ela entra pelo navegador.
entrar_com_codigo_de_dispositivo() {
    local url_base="$KC_URL/realms/$KC_REALM/protocol/openid-connect" resposta codigo intervalo fim
    resposta=$(curl -fsS -X POST "$url_base/auth/device" --data-urlencode "client_id=$KC_CLIENT_ID" --data-urlencode scope=openid) \
        || fail "o Keycloak recusou o pedido de código de dispositivo (o cliente '$KC_CLIENT_ID' existe e permite esse login?)."
    codigo=$(jq -r '.device_code' <<< "$resposta")
    intervalo=$(jq -r '.interval // 5' <<< "$resposta")
    fim=$((SECONDS + $(jq -r '.expires_in // 600' <<< "$resposta")))
    log "Abra $(jq -r '.verification_uri_complete // .verification_uri' <<< "$resposta") e entre com a conta de gestor de acessos"
    while [ "$SECONDS" -lt "$fim" ]; do
        sleep "$intervalo"
        resposta=$(curl -sS -X POST "$url_base/token" -K <(printf 'data-urlencode = "device_code=%s"\n' "$codigo") \
            --data-urlencode grant_type=urn:ietf:params:oauth:grant-type:device_code --data-urlencode "client_id=$KC_CLIENT_ID")
        case "$(jq -r '.error // empty' <<< "$resposta")" in
            "") guardar_token "$resposta"; ok "login concluído"; return 0 ;;
            authorization_pending) ;;
            slow_down) intervalo=$((intervalo + 5)) ;;
            *) fail "o login por código de dispositivo terminou sem sucesso: $(jq -r '.error' <<< "$resposta")." ;;
        esac
    done
    fail "o código de dispositivo expirou antes do login. Rode o roteiro de novo."
}

# $1=resposta do endpoint de token
guardar_token() {
    ACCESS_TOKEN=$(jq -r '.access_token // empty' <<< "$1")
    REFRESH_TOKEN=$(jq -r '.refresh_token // empty' <<< "$1")
    [ -n "$ACCESS_TOKEN" ] || fail "o Keycloak não devolveu o token de acesso."
    TOKEN_EXPIRA_EM=$((SECONDS + $(jq -r '.expires_in // 60' <<< "$1")))
}

# Renova o token com o token de renovação quando falta pouco para vencer.
renovar_token_se_preciso() {
    local resposta
    [ "$SECONDS" -ge $((TOKEN_EXPIRA_EM - 30)) ] || return 0
    [ -n "$REFRESH_TOKEN" ] || fail "o token venceu e o Keycloak não deu token de renovação. Rode o roteiro de novo."
    resposta=$(curl -sS -X POST "$KC_URL/realms/$KC_REALM/protocol/openid-connect/token" \
        -K <(printf 'data-urlencode = "refresh_token=%s"\n' "$REFRESH_TOKEN") \
        --data-urlencode grant_type=refresh_token --data-urlencode "client_id=$KC_CLIENT_ID")
    guardar_token "$resposta"
}

total_de_perfis() { jq length "$PERFIS_DE_OPERACAO"; }
campo_do_perfil() { jq -r ".[$1].$2" "$PERFIS_DE_OPERACAO"; }
permissoes_do_perfil() { jq -c ".[$1].permissoes" "$PERFIS_DE_OPERACAO"; }

# A ação do roteiro para um perfil, e a razão quando ele para. $1=situação (de situacao_do_perfil)
acao_para_a_situacao() {
    case "$1" in
        VIGENTE_IGUAL) echo "NADA" ;;
        PENDENTE_COM_DOCUMENTO) echo "AGUARDAR" ;;
        PENDENTE_SEM_DOCUMENTO) echo "REENVIAR" ;;
        ENCERRADA_EXPIRADA) echo "NOVA_VERSAO" ;;
        VIGENTE_DIFERENTE) echo "PARAR: está em vigor com permissões diferentes do arquivo" ;;
        PENDENTE_DIFERENTE) echo "PARAR: tem versão pendente com permissões diferentes do arquivo" ;;
        DESATIVADO) echo "PARAR: está desativado" ;;
        SUSPENSO) echo "PARAR: está suspenso" ;;
        *) echo "PARAR: a última versão foi recusada ou cancelada por uma pessoa" ;;
    esac
}

# Confere todos os perfis do arquivo sem gravar nada; preenche ACAO[i] e ID_DO_PERFIL[i]. Para se algum não pode seguir.
planejar_perfis() {
    local i nome listado detalhe situacao parados=0
    for ((i = 0; i < $(total_de_perfis); i++)); do
        renovar_token_se_preciso
        nome=$(campo_do_perfil "$i" nome)
        listado=$(perfil_pelo_nome "$ACCESS_TOKEN" "$nome")
        ID_DO_PERFIL[i]=""
        if [ -z "$listado" ]; then
            ACAO[i]=CRIAR
        else
            ID_DO_PERFIL[i]=$(jq -r '.id' <<< "$listado")
            detalhe=$(detalhe_do_perfil "$ACCESS_TOKEN" "${ID_DO_PERFIL[i]}")
            situacao=$(situacao_do_perfil "$detalhe" "$(permissoes_do_perfil "$i")")
            ACAO[i]=$(acao_para_a_situacao "$situacao")
            DIFERENCA[i]=$(diferenca_da_situacao "$detalhe" "$(permissoes_do_perfil "$i")" "$situacao")
        fi
        case "${ACAO[i]}" in
            PARAR*) warn "perfil '$nome' ${ACAO[i]#PARAR: } (diferença: ${DIFERENCA[i]:-nenhuma})"; parados=1 ;;
            *) ok "perfil '$nome': ${ACAO[i]}" ;;
        esac
    done
    [ "$parados" -eq 0 ] || fail "nenhum perfil foi gravado: resolva os perfis acima na tela de perfis e rode de novo."
}

# Cria o perfil ou a versão que a ação pede e envia o documento. $1=índice do perfil
executar_acao() {
    local i="$1" nome descricao envio permissoes tamanho
    nome=$(campo_do_perfil "$i" nome) descricao=$(campo_do_perfil "$i" descricao) permissoes=$(permissoes_do_perfil "$i")
    tamanho=$(stat -c%s "$DOCUMENTO_INSTITUIDOR")
    renovar_token_se_preciso
    case "${ACAO[i]}" in
        CRIAR) envio=$(propor_perfil_novo "$ACCESS_TOKEN" "$nome" "$descricao" "$permissoes" "$PROTOCOLO_DO_DOCUMENTO" "$tamanho") ;;
        NOVA_VERSAO) envio=$(propor_nova_versao "$ACCESS_TOKEN" "${ID_DO_PERFIL[i]}" "$nome" "$descricao" "$permissoes" "$PROTOCOLO_DO_DOCUMENTO" "$tamanho") ;;
        REENVIAR) envio=$(reenviar_documento "${ID_DO_PERFIL[i]}" "$tamanho") ;;
        *) ok "perfil '$nome': nada a gravar"; return 0 ;;
    esac
    enviar_e_confirmar_documento "$ACCESS_TOKEN" "$envio" "$DOCUMENTO_INSTITUIDOR"
    ok "perfil '$nome': documento enviado e confirmado"
}

# Pede a URL de envio do documento da versão pendente do perfil, conforme a situação do documento. $1=perfil $2=tamanho do PDF
reenviar_documento() {
    local detalhe
    detalhe=$(detalhe_do_perfil "$ACCESS_TOKEN" "$1")
    pedir_nova_url_do_documento "$ACCESS_TOKEN" "$1" "$(jq -r '.versaoPendente.versaoId' <<< "$detalhe")" "$2" \
        "$(jq -r '.versaoPendente.situacaoDoDocumento.codigo' <<< "$detalhe")"
}

# O que falta para cada perfil entrar em vigor, lido da API depois da gravação.
relatar_pendencias() {
    local i nome detalhe
    log "Situação dos perfis (as aprovações são feitas na tela de perfis)"
    for ((i = 0; i < $(total_de_perfis); i++)); do
        renovar_token_se_preciso
        nome=$(campo_do_perfil "$i" nome)
        detalhe=$(detalhe_do_perfil "$ACCESS_TOKEN" "$(perfil_pelo_nome "$ACCESS_TOKEN" "$nome" | jq -r '.id')")
        jq -r --arg n "$nome" 'if .composicaoEmVigor != null then "    \($n): em vigor"
            else "    \($n): aguarda \(.versaoPendente.etapaAtual.codigo); proteção de dados também aprova: \(.versaoPendente.exigeProtecaoDeDados); documento: \(.versaoPendente.situacaoDoDocumento.codigo); prazo até \(.versaoPendente.expiraEm)" end' <<< "$detalhe" >&2
    done
}

etapa_criar() {
    local i
    exigir_parametro DOCUMENTO_INSTITUIDOR
    exigir_parametro PROTOCOLO_DO_DOCUMENTO
    [ -s "$DOCUMENTO_INSTITUIDOR" ] || fail "o documento instituidor '$DOCUMENTO_INSTITUIDOR' não existe ou está vazio."
    entrar_com_codigo_de_dispositivo
    planejar_perfis
    for ((i = 0; i < $(total_de_perfis); i++)); do executar_acao "$i"; done
    relatar_pendencias
    ok "etapa 'criar' concluída: aprove as versões pendentes na tela e rode 'conferir'"
}

# Um perfil que não está na lista para pedido: não existe (1), aguarda a aprovação de uma versão pendente com documento (2)
# ou está em outra situação, inclusive sem documento enviado, que exige ação (1). Guarda o resultado em RESULTADO_DO_PERFIL. $1=índice $2=nome
conferir_perfil_fora_da_lista() {
    local id detalhe situacao
    id=$(perfil_pelo_nome "$ACCESS_TOKEN" "$2" | jq -r '.id // empty')
    if [ -z "$id" ]; then
        warn "perfil '$2' não existe: rode 'criar'"
        RESULTADO_DO_PERFIL=1
        return 0
    fi
    detalhe=$(detalhe_do_perfil "$ACCESS_TOKEN" "$id")
    situacao=$(situacao_do_perfil "$detalhe" "$(permissoes_do_perfil "$1")")
    case "$situacao" in
        PENDENTE_COM_DOCUMENTO)
            warn "perfil '$2' ainda não está em vigor: aguarde as aprovações pela tela"
            RESULTADO_DO_PERFIL=2 ;;
        PENDENTE_SEM_DOCUMENTO)
            warn "perfil '$2' tem versão pendente sem documento enviado ou com documento recusado: rode 'criar' para enviá-lo"
            RESULTADO_DO_PERFIL=1 ;;
        *)
            warn "perfil '$2' fora da lista para pedido, na situação $situacao: resolva na tela de perfis"
            RESULTADO_DO_PERFIL=1 ;;
    esac
}

# Compara um perfil com o arquivo e guarda em RESULTADO_DO_PERFIL: 0 conforme, 1 diferente, 2 aguardando aprovação.
# Não é chamada em condição, para que uma falha de leitura interrompa o roteiro. $1=índice $2=lista de perfis para pedido
conferir_perfil() {
    local i="$1" nome pedivel detalhe situacao alcance
    RESULTADO_DO_PERFIL=0
    nome=$(campo_do_perfil "$i" nome)
    pedivel=$(jq -c --arg n "$nome" 'first(.[] | select(.nome == $n)) // empty' <<< "$2")
    if [ -z "$pedivel" ]; then
        conferir_perfil_fora_da_lista "$i" "$nome"
        return 0
    fi
    detalhe=$(detalhe_do_perfil "$ACCESS_TOKEN" "$(jq -r '.id' <<< "$pedivel")")
    situacao=$(situacao_do_perfil "$detalhe" "$(permissoes_do_perfil "$i")")
    alcance=$(jq -r '.escopo.codigo' <<< "$pedivel")
    if [ "$situacao" != VIGENTE_IGUAL ] || [ "$alcance" != "$(campo_do_perfil "$i" alcance)" ]; then
        warn "perfil '$nome' difere do arquivo: situação $situacao; alcance na API '$alcance', esperado '$(campo_do_perfil "$i" alcance)'; permissões $(diferenca_da_situacao "$detalhe" "$(permissoes_do_perfil "$i")" "$situacao")"
        RESULTADO_DO_PERFIL=1
        return 0
    fi
    ok "perfil '$nome': conforme"
}

etapa_conferir() {
    local i pedidos saida=0
    entrar_com_codigo_de_dispositivo
    renovar_token_se_preciso
    pedidos=$(listar_paginas "$ACCESS_TOKEN" "/api/autorizacao/perfis-para-pedido?limit=100")
    for ((i = 0; i < $(total_de_perfis); i++)); do
        renovar_token_se_preciso
        conferir_perfil "$i" "$pedidos"
        case "$RESULTADO_DO_PERFIL" in
            1) saida=1 ;;
            2) [ "$saida" -eq 1 ] || saida=2 ;;
        esac
    done
    return "$saida"
}

main_instalacao() {
    declare -a ACAO ID_DO_PERFIL DIFERENCA
    conferir_parametros
    case "${1:-}" in
        criar) etapa_criar ;;
        conferir) etapa_conferir ;;
        *) fail "uso: $0 criar | conferir" ;;
    esac
}

main_instalacao "$@"
