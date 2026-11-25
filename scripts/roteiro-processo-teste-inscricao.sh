#!/usr/bin/env bash
# roteiro-processo-teste-inscricao.sh — cria o processo local de validação: o
# Processo Seletivo para o Curso de Medicina Unifesspa 2027, pela minuta do
# edital nº XX/2026-CEPS/Unifesspa, publicado e divulgado, com inscrição
# aberta no dia da execução e modelos de inscrição sem prova e de habilitação.
# A oferta é em turno integral. O FADA prevê acréscimo de 15% e usa os 38
# municípios do Anexo V.
#
# Os cadastros (unidades, campus, curso, oferta) têm os códigos da minuta e são
# reaproveitados numa segunda execução, assim como o processo identificado pelo
# mesmo identificador legível.
#
# A minuta remete os pesos à Resolução nº 966/2026 e não os traz; a nota do
# ENEM entra importada. O cronograma usa datas deslocadas do Anexo I para
# inscrição, resultado, habilitação, chamada e matrícula; por padrão, a inscrição
# está aberta no dia da execução. Na habilitação, o roteiro define documentos
# comuns e documentos para comprovar o FADA, preservando os do modelo.
#
# Sequência:
#   Setup (Publicações/Organização/Configuração) →
#   Criar ProcessoSeletivo → Atendimento Especializado → Distribuição de Vagas
#   (Anexo II) → Cascata de Remanejamento (Quadro 3) → FADA (item
#   1.6) → Critério de Desempate → Classificação importada → Taxa de Inscrição
#   (gratuita, item 2.2) → Cronograma de Fases → aplicar e vincular os modelos
#   de inscrição e habilitação → documentos comuns e do FADA → Documento do
#   Edital → Divulgação (sempre ANTES de publicar: depois da publicação o
#   processo só aceita mutação por retificação) → Publicar.
#
# Pré-requisitos: stack local do docker compose no ar, com uniplus-api,
# keycloak, o verificador de arquivos e os demais serviços de infraestrutura,
# e o serviço ambiente-init concluído, com os dois gestores de acessos
# instalados (ver docker/README.md ou CONTRIBUTING.md — não precisa dos *-web
# nem do openldap), jq, curl. Roda contra o realm `unifesspa`.
#
# O executor não recebe permissão do Keycloak: no início, o roteiro faz um
# gestor conceder a ele os perfis de que precisa, pela API
# (acessos-do-roteiro.sh). Em seguida designa dois Representantes do
# controlador e faz o primeiro aprovar a proteção do catálogo de tipos de
# documento e de fatos do candidato (governanca-do-roteiro.sh). Numa segunda
# execução, as concessões, as designações e as aprovações são reaproveitadas.
#
# Uso:
#   scripts/roteiro-processo-teste-inscricao.sh
#
# Variáveis de ambiente opcionais:
#   KC_URL        (default: http://localhost:8080)
#   KC_REALM      (default: unifesspa)
#   API_URL       (default: http://localhost:5200)
#   ADMIN_USER    (default: admin)
#   ADMIN_PASS    (default: $TEST_PASSWORD, senão Uni+Teste26 — mesmo default
#                 de setup-keycloak-dev.sh)
#   MODELO_INSCRICAO_ID (default: 5e3d0006-0000-7000-8000-000000000004 —
#                 PSR_INSCRICAO_SEM_PROVA, SementePsrMedicina2027; para
#                 simular processo com prova, 5e3d0006-0000-7000-8000-000000000003,
#                 PSR_INSCRICAO_COM_PROVA)
#   GEO_URL       (default: http://localhost:5400) — cadastro geográfico local
#   MUNICIPIOS_DE_PROVA (JSON, na ordem desejada; padrão: Marabá/PA):
#                 [{"codigoIbge":"1504208","uf":"PA"}]
#   EDITAL_PDF    (default: scripts/edital-de-teste.pdf) — PDF enviado como
#                 documento do edital, por exemplo a minuta
#   NOME_PROCESSO (default: o nome do processo da Medicina 2027)
#   IDENTIFICADOR_PROCESSO (default: psm-2027) — o identificador legível é único:
#                 outro processo na mesma base precisa de outro valor
#   INICIO_INSCRICAO, FIM_INSCRICAO — início e fim do período de inscrição,
#                 em ISO 8601 com fuso Z ou ±HH:MM e fração de segundo opcional.
#                 Por padrão, o início é ontem à meia-noite e o fim é o do
#                 Anexo I deslocado (31 dias após a data do início, às 23h59min59s).
#                 As demais fases acompanham a data do início no fuso de Belém.
#
# O arquivo também pode ser lido com `source` por um roteiro irmão (ver
# roteiro-processo-teste-notas-do-enem.sh): as funções ficam disponíveis e o
# `main` só roda quando o arquivo é executado; ao carregar, o período é conferido.

set -euo pipefail

KC_URL="${KC_URL:-http://localhost:8080}"
KC_REALM="${KC_REALM:-unifesspa}"
API_URL="${API_URL:-http://localhost:5200}"
ADMIN_USER="${ADMIN_USER:-admin}"
# Mesmo default de setup-keycloak-dev.sh — se o setup rodou com TEST_PASSWORD
# diferente, o admin está com esse valor, não com Uni+Teste26.
ADMIN_PASS="${ADMIN_PASS:-${TEST_PASSWORD:-Uni+Teste26}}"
MODELO_INSCRICAO_ID="${MODELO_INSCRICAO_ID:-5e3d0006-0000-7000-8000-000000000004}"
GEO_URL="${GEO_URL:-http://localhost:5400}"
if [ -z "${MUNICIPIOS_DE_PROVA:-}" ]; then
    MUNICIPIOS_DE_PROVA='[{"codigoIbge":"1504208","uf":"PA"}]'
fi
MODELO_PEDE_CIDADE_DE_PROVA="false"
FATO_CIDADE_DE_PROVA=""
# Um PDF de verdade: o portal abre o edital no navegador, e bytes que só começam
# com %PDF- abririam uma aba em branco.
EDITAL_PDF="${EDITAL_PDF:-$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/edital-de-teste.pdf}"
NOME_PROCESSO="${NOME_PROCESSO:-Processo Seletivo para o Curso de Medicina Unifesspa 2027}"
IDENTIFICADOR_PROCESSO="${IDENTIFICADOR_PROCESSO:-psm-2027}"
# As ofertas de curso da distribuição de vagas e o número de opções que o
# candidato escolhe. O processo da Medicina tem uma só oferta e uma só opção.
OFERTAS_DISTRIBUIDAS=()
NUMERO_OPCOES_DE_CURSO=1
# A fase de classificação só entra no cronograma quando o roteiro das notas do
# ENEM a obtém; nunca vem do ambiente.
FASE_CLASSIFICACAO_ID=""
TOKEN=""

log()  { printf '\033[1;36m==> %s\033[0m\n' "$*" >&2; }
ok()   { printf '\033[1;32m    OK %s\033[0m\n' "$*" >&2; }
warn() { printf '\033[1;33m!! %s\033[0m\n' "$*" >&2; }
fail() { printf '\033[1;31m✗ %s\033[0m\n' "$*" >&2; exit 1; }

idem() { cat /proc/sys/kernel/random/uuid; }

# As concessões que o executor precisa, pedidas e aprovadas pela API.
# shellcheck source=acessos-do-roteiro.sh
source "$(dirname "${BASH_SOURCE[0]}")/acessos-do-roteiro.sh"
# Os Representantes do controlador e a proteção do catálogo aprovada.
# shellcheck source=governanca-do-roteiro.sh
source "$(dirname "${BASH_SOURCE[0]}")/governanca-do-roteiro.sh"

# Falha alto quando um id de catálogo (criado ou reaproveitado via fallback
# de busca) sai vazio, em vez de deixar "" descer em silêncio para o próximo
# PUT/POST e estourar um 400/422 longe da causa real.
exigir_id() { [ -n "$2" ] || fail "$1 ficou vazio (409 sem entrada equivalente encontrada no fallback de busca?)"; }

municipios_da_base_legal_conferem() {
    jq -e --argjson esperados "$2" \
        '([.municipios[] | {codigoIbge, nome, uf}] | sort_by(.codigoIbge, .nome, .uf)) ==
         ([$esperados[] | {codigoIbge, nome, uf}] | sort_by(.codigoIbge, .nome, .uf))' \
        <<< "$1" >/dev/null
}

# curl autenticado, falha (com o corpo do erro) em qualquer resposta fora de
# 2xx. $1=método $2=caminho (a partir de API_URL) $3=corpo (ou "")
api() {
    local method="$1" path="$2" body="${3:-}"
    local args=(-sS -w '\n%{http_code}' -X "$method" "$API_URL$path" -H "Authorization: Bearer $TOKEN" -H "Idempotency-Key: $(idem)")
    if [ -n "$body" ]; then
        args+=(-H "Content-Type: application/json" -d "$body")
    fi
    local resp http_code payload
    resp=$(curl "${args[@]}")
    http_code=$(tail -n1 <<< "$resp")
    payload=$(sed '$d' <<< "$resp")
    case "$http_code" in
        2??) echo "$payload" ;;
        403) explicar_recusa_de_acesso "$method $path"; fail "$method $path → 403: $payload" ;;
        *) fail "$method $path → $http_code: $payload" ;;
    esac
}

# GET autenticado que sempre espera 2xx — todo chamador deste helper trata a
# ausência como erro, nunca como "não encontrado, tudo bem". Os poucos pontos
# que toleram 404/loop de retry (conferir_resultado) usam curl bruto, não este.
api_get() {
    local resp http_code payload
    resp=$(curl -sS -w '\n%{http_code}' "$API_URL$1" -H "Authorization: Bearer $TOKEN")
    http_code=$(tail -n1 <<< "$resp")
    payload=$(sed '$d' <<< "$resp")
    case "$http_code" in
        2??) echo "$payload" ;;
        *) fail "GET $1 → $http_code: $payload" ;;
    esac
}

obter_token() {
    log "Obtendo token de admin (realm $KC_REALM)"
    TOKEN=$(curl -sS -X POST "$KC_URL/realms/$KC_REALM/protocol/openid-connect/token" \
        --data-urlencode "grant_type=password" --data-urlencode "client_id=admin-cli" \
        --data-urlencode "username=$ADMIN_USER" --data-urlencode "password=$ADMIN_PASS" \
        | jq -r '.access_token // empty')
    [ -n "$TOKEN" ] || fail "Não obtive token — stack no ar? setup-keycloak-dev.sh rodado?"
    ok "token obtido"
}

# POST tolerante a 409 (catálogo singleton ou já existente) — devolve o id
# capturado do 201, ou vazio em 409 (quem chama resolve via GET).
post_tolerante() {
    local path="$1" body="$2"
    local resp http_code
    resp=$(curl -sS -w '\n%{http_code}' -X POST "$API_URL$path" \
        -H "Authorization: Bearer $TOKEN" -H "Idempotency-Key: $(idem)" \
        -H "Content-Type: application/json" -d "$body")
    http_code=$(tail -n1 <<< "$resp")
    local payload
    payload=$(sed '$d' <<< "$resp")
    if [ "$http_code" = "201" ]; then
        # Alguns endpoints devolvem {"id": "..."}, outros o id puro como string
        # JSON. "null" sai como vazio — nenhum chamador deve tratar a string
        # "null" como um id válido.
        jq -r 'if type == "object" then (.id // "") else . end' <<< "$payload"
    elif [ "$http_code" = "409" ]; then
        echo ""
    elif [ "$http_code" = "403" ]; then
        explicar_recusa_de_acesso "POST $path"
        fail "POST $path → 403: $payload"
    else
        fail "POST $path → $http_code: $payload"
    fi
}

setup_publicacoes() {
    log "Setup — Publicações (tipos de ato)"
    post_tolerante "/api/publicacoes/admin/tipos-ato" '{
        "codigo": "EDITAL_ABERTURA", "nome": "Edital de abertura",
        "congelaConfiguracao": true, "unicoPorObjeto": true,
        "efeitoIrreversivel": false, "ehResultado": false, "publicavelComoAvulso": false, "exigeAtoCorrigido": false, "vigenciaInicio": "2020-01-01",
        "vigenciaFim": null, "baseLegal": null
    }' >/dev/null
    post_tolerante "/api/publicacoes/admin/tipos-ato" '{
        "codigo": "RESULTADO_CLASSIFICACAO", "nome": "Resultado da classificação",
        "congelaConfiguracao": false, "unicoPorObjeto": false,
        "efeitoIrreversivel": false, "publicavelComoAvulso": false, "exigeAtoCorrigido": false, "vigenciaInicio": "2020-01-01",
        "vigenciaFim": null, "baseLegal": null, "ehResultado": true
    }' >/dev/null

    # Catálogo pode já existir de uma rodada anterior sem `ehResultado=true` —
    # o cronograma de fases recusa um produto com papel PRELIMINAR/DEFINITIVO
    # referenciando um tipo de ato que não é resultado
    # (uniplus.selecao.produto_da_fase.papel_em_ato_que_nao_eh_resultado) —
    # upsert explícito via PUT.
    local atual eh_resultado id
    atual=$(api_get "/api/publicacoes/tipos-ato/RESULTADO_CLASSIFICACAO/vigente")
    eh_resultado=$(jq -r '.ehResultado' <<< "$atual")
    if [ "$eh_resultado" != "true" ]; then
        id=$(jq -r '.id' <<< "$atual")
        api PUT "/api/publicacoes/admin/tipos-ato/$id" "{
            \"id\": \"$id\", \"codigo\": \"RESULTADO_CLASSIFICACAO\", \"nome\": \"Resultado da classificação\",
            \"congelaConfiguracao\": false, \"unicoPorObjeto\": false,
            \"efeitoIrreversivel\": false, \"ehResultado\": true, \"publicavelComoAvulso\": false, \"exigeAtoCorrigido\": false,
            \"vigenciaInicio\": \"2020-01-01\", \"vigenciaFim\": null, \"baseLegal\": null
        }" >/dev/null
    fi
    ok "tipos de ato prontos"
}

setup_organizacao() {
    log "Setup — Organização"
    post_tolerante "/api/organizacao/admin/instituicao" '{
        "codigoEmec": "3990", "nome": "Universidade Federal do Sul e Sudeste do Pará",
        "sigla": "Unifesspa", "organizacaoAcademica": "Universidade",
        "categoriaAdministrativa": "Pública Federal", "cidadeCodigoIbge": "1504208",
        "cidadeNome": "Marabá", "cidadeUf": "PA"
    }' >/dev/null

    # O CEPS conduz o processo (item 1.1); o IESB oferta o curso (Anexo II).
    UNIDADE_ID=$(criar_unidade "CEPS" "Centro de Processos Seletivos" "CENTRO" false)
    UNIDADE_OFERTANTE_ID=$(criar_unidade "IESB" "Instituto de Estudos em Saúde e Biológicas" "INSTITUTO" true)
    ok "unidade=$UNIDADE_ID unidade_ofertante=$UNIDADE_OFERTANTE_ID"
}

# $1=sigla $2=nome $3=código do tipo (por exemplo CENTRO) $4=unidadeAcademica — devolve o id, criado ou reaproveitado pelo código.
criar_unidade() {
    local sigla="$1" nome="$2" tipo="$3" academica="$4" id
    id=$(post_tolerante "/api/organizacao/admin/unidades" "{
        \"nome\": \"$nome\", \"alias\": \"$sigla\",
        \"slug\": \"${sigla,,}\", \"sigla\": \"$sigla\",
        \"codigo\": \"$sigla\", \"unidadeSuperiorId\": null,
        \"tipo\": \"$tipo\", \"unidadeAcademica\": $academica,
        \"vigenciaInicio\": \"2020-01-01\", \"vigenciaFim\": null,
        \"cidadeCodigoIbge\": \"1504208\", \"cidadeNome\": \"Marabá\", \"cidadeUf\": \"PA\"
    }")
    [ -z "$id" ] && id=$(api_get "/api/organizacao/unidades?limit=100" | jq -r --arg c "$sigla" '.[] | select(.codigo==$c) | .id' | head -1)
    exigir_id "unidade $sigla" "$id"
    echo "$id"
}

setup_configuracao() {
    log "Setup — Configuração (campus, local de oferta, curso, oferta de curso)"
    CAMPUS_ID=$(post_tolerante "/api/configuracao/admin/campi" '{
        "sigla": "MARABA", "nome": "Campus de Marabá",
        "cidadeCodigoIbge": "1504208", "cidadeNome": "Marabá", "cidadeUf": "PA",
        "endereco": null, "codigoEmec": null
    }')
    [ -z "$CAMPUS_ID" ] && CAMPUS_ID=$(api_get "/api/configuracao/campi?limit=100" | jq -r '.[] | select(.sigla=="MARABA") | .id' | head -1)
    exigir_id "CAMPUS_ID" "$CAMPUS_ID"
    # Reaproveita o local pelo campus e a oferta pelo curso antes de criar.
    LOCAL_OFERTA_ID=$(api_get "/api/configuracao/locais-oferta?limit=100" | jq -r --arg c "$CAMPUS_ID" '.[] | select(.campusResponsavelId==$c) | .id' | head -1)
    if [ -z "$LOCAL_OFERTA_ID" ]; then
        LOCAL_OFERTA_ID=$(post_tolerante "/api/configuracao/admin/locais-oferta" "{
            \"tipo\": \"CAMPUS_SEDE\", \"campusResponsavelId\": \"$CAMPUS_ID\",
            \"cidadeCodigoIbge\": \"1504208\", \"cidadeNome\": \"Marabá\", \"cidadeUf\": \"PA\",
            \"endereco\": null, \"codigoEmec\": null
        }")
    fi
    exigir_id "LOCAL_OFERTA_ID" "$LOCAL_OFERTA_ID"
    CURSO_ID=$(post_tolerante "/api/configuracao/admin/cursos" '{
        "codigo": "MEDICINA", "nome": "Medicina",
        "grau": "Bacharelado", "nivelEnsino": "Graduação", "grupoAreaEnem": null
    }')
    [ -z "$CURSO_ID" ] && CURSO_ID=$(api_get "/api/configuracao/cursos?limit=100" | jq -r '.[] | select(.codigo=="MEDICINA") | .id' | head -1)
    exigir_id "CURSO_ID" "$CURSO_ID"
    OFERTA_CURSO_ID=$(api_get "/api/configuracao/ofertas-curso?limit=100" | jq -r --arg c "$CURSO_ID" --arg l "$LOCAL_OFERTA_ID" --arg u "$UNIDADE_OFERTANTE_ID" '.[] | select(.cursoId==$c and .localOfertaId==$l and .unidadeOfertante.origemId==$u) | .id' | head -1)
    if [ -z "$OFERTA_CURSO_ID" ]; then
        OFERTA_CURSO_ID=$(post_tolerante "/api/configuracao/admin/ofertas-curso" "{
            \"cursoId\": \"$CURSO_ID\", \"localOfertaId\": \"$LOCAL_OFERTA_ID\",
            \"unidadeOfertanteOrigemId\": \"$UNIDADE_OFERTANTE_ID\", \"codigo\": \"OFERTA_MEDICINA_INTEGRAL\",
            \"programaDeOferta\": \"REGULAR\",
            \"regimeDeFuncionamento\": \"EXTENSIVO\", \"formatoPedagogico\": \"PRESENCIAL\",
            \"regimeDeTurno\": \"INTEGRAL\", \"turnos\": [\"MATUTINO\", \"VESPERTINO\"],
            \"codigoEmec\": null, \"codigoNoSistemaAcademico\": null, \"vagasAnuaisAutorizadas\": null,
            \"baseLegal\": null, \"atoAutorizacaoMec\": null
        }")
        [ -z "$OFERTA_CURSO_ID" ] && OFERTA_CURSO_ID=$(api_get "/api/configuracao/ofertas-curso?limit=100" | jq -r --arg c "$CURSO_ID" --arg l "$LOCAL_OFERTA_ID" --arg u "$UNIDADE_OFERTANTE_ID" '.[] | select(.cursoId==$c and .localOfertaId==$l and .unidadeOfertante.origemId==$u) | .id' | head -1)
    fi
    [ -n "$OFERTA_CURSO_ID" ] || fail "A API recusou o cadastro da oferta de Medicina para o local e a unidade selecionados; nenhuma oferta correspondente foi encontrada."
    local oferta
    oferta=$(api_get "/api/configuracao/ofertas-curso/$OFERTA_CURSO_ID")
    jq -e --arg c "$CURSO_ID" --arg l "$LOCAL_OFERTA_ID" --arg u "$UNIDADE_OFERTANTE_ID" '.cursoId==$c and .localOfertaId==$l and .unidadeOfertante.origemId==$u' <<< "$oferta" >/dev/null \
        || fail "A oferta $OFERTA_CURSO_ID não corresponde ao curso, local e unidade selecionados para Medicina."
    if ! jq -e '(.regimeDeTurno.codigo == "INTEGRAL") and ([.turnos[]?.codigo] | sort == ["MATUTINO", "VESPERTINO"])' <<< "$oferta" >/dev/null; then
        api PUT "/api/configuracao/admin/ofertas-curso/$OFERTA_CURSO_ID" "$(jq -c '{
            id, codigo, programaDeOferta: .programaDeOferta.codigo,
            regimeDeFuncionamento: .regimeDeFuncionamento.codigo,
            regimeDeTurno: "INTEGRAL", turnos: ["MATUTINO", "VESPERTINO"],
            formatoPedagogico: .formatoPedagogico.codigo,
            codigoEmec, codigoNoSistemaAcademico, vagasAnuaisAutorizadas, baseLegal, atoAutorizacaoMec
        }' <<< "$oferta")" >/dev/null
        oferta=$(api_get "/api/configuracao/ofertas-curso/$OFERTA_CURSO_ID")
    fi
    jq -e '(.regimeDeTurno.codigo == "INTEGRAL") and ([.turnos[]?.codigo] | sort == ["MATUTINO", "VESPERTINO"])' <<< "$oferta" >/dev/null \
        || fail "A oferta de Medicina cadastrada deve ter regime INTEGRAL e os turnos MATUTINO e VESPERTINO. Confira a oferta $OFERTA_CURSO_ID."
    ok "campus=$CAMPUS_ID local_oferta=$LOCAL_OFERTA_ID curso=$CURSO_ID oferta_curso=$OFERTA_CURSO_ID"

    log "Setup — fases canônicas e referência de reserva demográfica"
    # As fases vêm do catálogo semeado (FaseCanonicaSeed).
    local fases
    fases=$(api_get "/api/configuracao/fases-canonicas?limit=100")
    FASE_INSCRICAO_ID=$(jq -r '.[] | select(.codigo=="INSCRICAO") | .id' <<< "$fases")
    FASE_RESULTADO_ID=$(jq -r '.[] | select(.codigo=="RESULTADO_FINAL") | .id' <<< "$fases")
    FASE_CHAMADA_ID=$(jq -r '.[] | select(.codigo=="CHAMADA") | .id' <<< "$fases")
    FASE_MATRICULA_ID=$(jq -r '.[] | select(.codigo=="MATRICULA") | .id' <<< "$fases")
    FASE_HABILITACAO_ID=$(jq -r '.[] | select(.codigo=="HABILITACAO") | .id' <<< "$fases")
    exigir_id "FASE_INSCRICAO_ID" "$FASE_INSCRICAO_ID"
    exigir_id "FASE_RESULTADO_ID" "$FASE_RESULTADO_ID"
    exigir_id "FASE_HABILITACAO_ID" "$FASE_HABILITACAO_ID"
    exigir_id "FASE_CHAMADA_ID" "$FASE_CHAMADA_ID"
    exigir_id "FASE_MATRICULA_ID" "$FASE_MATRICULA_ID"

    # Tipos de documento do catálogo semeado.
    local tipos_documento
    tipos_documento=$(api_get "/api/configuracao/tipos-documento?limit=100")
    TIPOS_DOCUMENTO="$tipos_documento"
    TIPO_DOCUMENTO_HISTORICO_ID=$(jq -r '.[] | select(.codigo=="HISTORICO_ENSINO_MEDIO") | .id' <<< "$tipos_documento")
    TIPO_DOCUMENTO_RESIDENCIA_ID=$(jq -r '.[] | select(.codigo=="COMPROVANTE_RESIDENCIA") | .id' <<< "$tipos_documento")
    exigir_id "TIPO_DOCUMENTO_HISTORICO_ID" "$TIPO_DOCUMENTO_HISTORICO_ID"
    exigir_id "TIPO_DOCUMENTO_RESIDENCIA_ID" "$TIPO_DOCUMENTO_RESIDENCIA_ID"

    # Referência demográfica usada na distribuição de vagas do processo.
    local censo="IBGE 2022/2010 PA" referencia_demografica
    RESERVA_DEMOGRAFICA_ID=$(post_tolerante "/api/configuracao/admin/referencias-reserva-demografica" "{
        \"censoReferencia\": \"$censo\",
        \"ppiPercentual\": 80.49, \"quilombolaPercentual\": 1.66, \"pcdPercentual\": 6.65,
        \"baseLegal\": \"Edital Medicina 2027, Anexo II, nota 2\"
    }")
    if [ -z "$RESERVA_DEMOGRAFICA_ID" ]; then
        referencia_demografica=$(api_get "/api/configuracao/referencias-reserva-demografica?limit=100" | jq -c --arg c "$censo" '[.[] | select(.censoReferencia == $c)] | .[0]')
        RESERVA_DEMOGRAFICA_ID=$(jq -r '.id // empty' <<< "$referencia_demografica")
        exigir_id "RESERVA_DEMOGRAFICA_ID" "$RESERVA_DEMOGRAFICA_ID"
        if ! jq -e '.ppiPercentual == 80.49 and .quilombolaPercentual == 1.66 and .pcdPercentual == 6.65' \
            <<< "$referencia_demografica" >/dev/null; then
            fail "A referência de reserva demográfica já cadastrada tem percentuais diferentes dos esperados: PPI 80,49%, quilombola 1,66% e PcD 6,65%."
        fi
    fi
    exigir_id "RESERVA_DEMOGRAFICA_ID" "$RESERVA_DEMOGRAFICA_ID"

    # PSR (Processo Seletivo Regular) é o tipo de processo do modelo de
    # inscrição semeado da Medicina (SementePsrMedicina2027) — aplicar o
    # modelo exige que o processo seja do mesmo tipo.
    TIPO_PROCESSO_ID=$(api_get "/api/configuracao/tipos-processo?limit=100" | jq -r '.[] | select(.codigo=="REGULAR_UNIFICADO") | .id')
    [ -n "$TIPO_PROCESSO_ID" ] || fail "tipo de processo 'REGULAR_UNIFICADO' não encontrado no catálogo semeado"

    # O modelo com prova coleta CONDICAO_ATENDIMENTO e RECURSO_ACESSIBILIDADE, cujas
    # opções são as que o processo oferta em atendimento especializado; a conformidade
    # recusa publicar o fato coletável sem ao menos uma opção. O processo sem prova
    # não oferta atendimento: o TIPO_DEFICIENCIA da reserva de vagas oferece os tipos
    # do cadastro, em qualquer processo (UNI-REQ-0012).
    # As condições são as do atendimento especializado do INEP (Edital ENEM
    # 52/2025): atendimento especializado não é só de quem tem deficiência.
    # "PCD" é o código reconhecido por AtendimentoEspecializado.CodigoCondicaoPcd
    # (ADR-0067) — tipo de deficiência só é aceito sob essa condição, por esse código.
    # "OUTRA_CONDICAO" é a condição cuja escolha o modelo de inscrição com prova cita
    # para pedir a descrição: aplicar esse modelo exige que o processo a oferte.
    local condicoes codigo nome descricao id
    CONDICAO_ATENDIMENTO_IDS="[]"
    condicoes=$(jq -nr '[
        ["PCD", "Pessoa com deficiência", "Deficiência, com o tipo informado na pergunta seguinte."],
        ["GESTANTE", "Gestante", "Pessoa em período de gestação."],
        ["LACTANTE", "Lactante", "Pessoa que amamenta."],
        ["PESSOA_IDOSA", "Pessoa idosa", "Pessoa com 60 anos ou mais."],
        ["DISLEXIA", "Dislexia", "Transtorno específico da leitura."],
        ["DISCALCULIA", "Discalculia", "Transtorno específico da matemática."],
        ["DEFICIT_ATENCAO", "Déficit de atenção", "Transtorno do déficit de atenção, com ou sem hiperatividade."],
        ["OUTRA_CONDICAO", "Outra condição específica", "Condição específica que não consta da lista."]
    ][] | @tsv')
    while IFS=$'\t' read -r codigo nome descricao; do
        id=$(post_tolerante "/api/configuracao/admin/condicoes-atendimento" "$(jq -nc \
            --arg c "$codigo" --arg n "$nome" --arg d "$descricao" '{codigo: $c, nome: $n, descricao: $d}')")
        [ -z "$id" ] && id=$(api_get "/api/configuracao/condicoes-atendimento?limit=100" | jq -r --arg c "$codigo" '.[] | select(.codigo==$c) | .id')
        exigir_id "condição de atendimento $codigo" "$id"
        CONDICAO_ATENDIMENTO_IDS=$(jq -c --arg id "$id" '. + [$id]' <<< "$CONDICAO_ATENDIMENTO_IDS")
    done <<< "$condicoes"

    # Os recursos de acessibilidade das etapas presenciais (heteroidentificação,
    # avaliação biopsicossocial e habilitação). O formulário pede o recurso a quem
    # pede atendimento, e a publicação recusa o fato coletado sem recurso ofertado.
    local recursos
    RECURSO_ACESSIBILIDADE_IDS="[]"
    recursos=$(jq -nr '[
        ["SALA_DE_FACIL_ACESSO", "Sala de fácil acesso", "Sala no térreo ou com acesso por rampa ou elevador."],
        ["ACOMPANHANTE", "Acompanhante", "Acompanhante durante a etapa presencial."],
        ["SALA_PARA_AMAMENTACAO", "Sala para amamentação", "Sala reservada para amamentar durante a etapa presencial."],
        ["TEMPO_ADICIONAL", "Tempo adicional", "Tempo adicional para cumprir a etapa presencial."],
        ["INTERPRETE_DE_LIBRAS", "Intérprete de Libras", "Tradutor-intérprete da Língua Brasileira de Sinais."],
        ["MATERIAL_AMPLIADO", "Material ampliado", "Material da etapa com fonte ampliada."],
        ["LEDOR", "Ledor", "Pessoa que lê o material da etapa para o candidato."],
        ["TRANSCRITOR", "Transcritor", "Pessoa que transcreve as respostas do candidato."]
    ][] | @tsv')
    while IFS=$'\t' read -r codigo nome descricao; do
        id=$(post_tolerante "/api/configuracao/admin/recursos-acessibilidade" "$(jq -nc \
            --arg c "$codigo" --arg n "$nome" --arg d "$descricao" '{codigo: $c, nome: $n, descricao: $d}')")
        [ -z "$id" ] && id=$(api_get "/api/configuracao/recursos-acessibilidade?limit=100" | jq -r --arg c "$codigo" '.[] | select(.codigo==$c) | .id')
        exigir_id "recurso de acessibilidade $codigo" "$id"
        RECURSO_ACESSIBILIDADE_IDS=$(jq -c --arg id "$id" '. + [$id]' <<< "$RECURSO_ACESSIBILIDADE_IDS")
    done <<< "$recursos"

    # Os tipos de deficiência do item 11.2.2 da minuta: o cadastro é a fonte das opções
    # do TIPO_DEFICIENCIA, e a oferta do processo com prova escolhe entre eles os atendidos.
    local tipos
    TIPO_DEFICIENCIA_IDS="[]"
    tipos=$(jq -nr '[
        ["FISICA", "Deficiência física"], ["SURDEZ", "Surdez"],
        ["AUDITIVA", "Deficiência auditiva"], ["DEFICIENCIA_VISUAL", "Deficiência visual"],
        ["INTELECTUAL", "Deficiência intelectual"],
        ["MULTIPLA", "Deficiência múltipla"], ["TEA", "Transtorno do Espectro Autista"],
        ["FIBROMIALGIA", "Fibromialgia"]
    ][] | @tsv')
    while IFS=$'\t' read -r codigo nome; do
        id=$(post_tolerante "/api/configuracao/admin/tipos-deficiencia" "{
            \"codigo\": \"$codigo\", \"nome\": \"$nome\", \"descricao\": \"$nome, conforme laudo.\", \"permanente\": true
        }")
        [ -z "$id" ] && id=$(api_get "/api/configuracao/tipos-deficiencia?limit=100" | jq -r --arg c "$codigo" '.[] | select(.codigo==$c) | .id')
        exigir_id "tipo de deficiência $codigo" "$id"
        TIPO_DEFICIENCIA_IDS=$(jq -c --arg id "$id" '. + [$id]' <<< "$TIPO_DEFICIENCIA_IDS")
    done <<< "$tipos"

    # Municípios do Anexo V da minuta para o FADA.
    local fada_identificacao="Edital nº XX/2026-CEPS/Unifesspa, itens 1.5 e 1.6"
    local municipios_fada base_legal_fada
    municipios_fada=$(cat <<'JSON'
[
        {"codigoIbge": "1500131", "nome": "Abel Figueiredo", "uf": "PA"},
        {"codigoIbge": "1500347", "nome": "Água Azul do Norte", "uf": "PA"},
        {"codigoIbge": "1501204", "nome": "Baião", "uf": "PA"},
        {"codigoIbge": "1501253", "nome": "Bannach", "uf": "PA"},
        {"codigoIbge": "1501576", "nome": "Bom Jesus do Tocantins", "uf": "PA"},
        {"codigoIbge": "1501758", "nome": "Brejo Grande do Araguaia", "uf": "PA"},
        {"codigoIbge": "1501782", "nome": "Breu Branco", "uf": "PA"},
        {"codigoIbge": "1502152", "nome": "Canaã dos Carajás", "uf": "PA"},
        {"codigoIbge": "1502707", "nome": "Conceição do Araguaia", "uf": "PA"},
        {"codigoIbge": "1502764", "nome": "Cumaru do Norte", "uf": "PA"},
        {"codigoIbge": "1502772", "nome": "Curionópolis", "uf": "PA"},
        {"codigoIbge": "1502954", "nome": "Eldorado dos Carajás", "uf": "PA"},
        {"codigoIbge": "1503044", "nome": "Floresta do Araguaia", "uf": "PA"},
        {"codigoIbge": "1503093", "nome": "Goianésia do Pará", "uf": "PA"},
        {"codigoIbge": "1503705", "nome": "Itupiranga", "uf": "PA"},
        {"codigoIbge": "1503804", "nome": "Jacundá", "uf": "PA"},
        {"codigoIbge": "1504208", "nome": "Marabá", "uf": "PA"},
        {"codigoIbge": "1504976", "nome": "Nova Ipixuna", "uf": "PA"},
        {"codigoIbge": "1505064", "nome": "Novo Repartimento", "uf": "PA"},
        {"codigoIbge": "1505437", "nome": "Ourilândia do Norte", "uf": "PA"},
        {"codigoIbge": "1505486", "nome": "Pacajá", "uf": "PA"},
        {"codigoIbge": "1505494", "nome": "Palestina do Pará", "uf": "PA"},
        {"codigoIbge": "1505536", "nome": "Parauapebas", "uf": "PA"},
        {"codigoIbge": "1505551", "nome": "Pau d'Arco", "uf": "PA"},
        {"codigoIbge": "1505635", "nome": "Piçarra", "uf": "PA"},
        {"codigoIbge": "1506138", "nome": "Redenção", "uf": "PA"},
        {"codigoIbge": "1506161", "nome": "Rio Maria", "uf": "PA"},
        {"codigoIbge": "1506187", "nome": "Rondon do Pará", "uf": "PA"},
        {"codigoIbge": "1506583", "nome": "Santa Maria das Barreiras", "uf": "PA"},
        {"codigoIbge": "1506708", "nome": "Santana do Araguaia", "uf": "PA"},
        {"codigoIbge": "1507151", "nome": "São Domingos do Araguaia", "uf": "PA"},
        {"codigoIbge": "1507300", "nome": "São Félix do Xingu", "uf": "PA"},
        {"codigoIbge": "1507458", "nome": "São Geraldo do Araguaia", "uf": "PA"},
        {"codigoIbge": "1507508", "nome": "São João do Araguaia", "uf": "PA"},
        {"codigoIbge": "1507755", "nome": "Sapucaia", "uf": "PA"},
        {"codigoIbge": "1508084", "nome": "Tucumã", "uf": "PA"},
        {"codigoIbge": "1508100", "nome": "Tucuruí", "uf": "PA"},
        {"codigoIbge": "1508407", "nome": "Xinguara", "uf": "PA"}
]
JSON
    )
    BASE_LEGAL_FADA_ID=$(post_tolerante "/api/configuracao/admin/base-legal-bonus-regional" "$(jq -nc \
        --arg identificacao "$fada_identificacao" --argjson municipios "$municipios_fada" \
        '{tipoInstrumento: "EDITAL", identificacao: $identificacao,
          descricao: "FADA", municipios: $municipios}')")
    if [ -z "$BASE_LEGAL_FADA_ID" ]; then
        base_legal_fada=$(api_get "/api/configuracao/base-legal-bonus-regional?limit=100" | jq -c --arg i "$fada_identificacao" '[.[] | select(.identificacao == $i)] | if length == 1 then .[0] else null end')
        BASE_LEGAL_FADA_ID=$(jq -r '.id // empty' <<< "$base_legal_fada")
        exigir_id "BASE_LEGAL_FADA_ID" "$BASE_LEGAL_FADA_ID"
        if ! municipios_da_base_legal_conferem "$base_legal_fada" "$municipios_fada"; then
            log "Atualizando os municípios da base legal do FADA conforme o Anexo V"
            if ! (api PUT "/api/configuracao/admin/base-legal-bonus-regional/$BASE_LEGAL_FADA_ID" "$(jq -c \
                --argjson municipios "$municipios_fada" \
                '{id, tipoInstrumento: .tipoInstrumento.codigo, identificacao, descricao,
                  municipios: [$municipios[] | {codigoIbge, nome, uf}]}' <<< "$base_legal_fada")" >/dev/null); then
                fail "A atualização dos municípios da base legal do FADA foi recusada."
            fi
            base_legal_fada=$(api_get "/api/configuracao/base-legal-bonus-regional/$BASE_LEGAL_FADA_ID")
            if ! municipios_da_base_legal_conferem "$base_legal_fada" "$municipios_fada"; then
                fail "Os municípios da base legal do FADA continuam diferentes dos previstos no Anexo V após a atualização."
            fi
        fi
    fi
    exigir_id "BASE_LEGAL_FADA_ID" "$BASE_LEGAL_FADA_ID"

    # Pré-requisito explícito do roteiro: calendário de dias úteis vigente.
    # Sem fase com regraRecurso o agregado não o exige para publicar, mas o
    # roteiro o declara de qualquer forma — é isso que a próxima mudança de
    # envelope (edição do ENEM) e o teste ponta a ponta vão precisar
    # encontrar já configurado.
    #
    # Nunca cria nem troca o calendário vigente do ambiente se já existe um —
    # "vigente" é singleton do realm inteiro, não do processo deste roteiro, e
    # sobrescrevê-lo apagaria o calendário real de feriados que outro
    # desenvolvimento esteja usando. Só cria (e só então marca vigente) quando
    # o ambiente está limpo.
    local calendario_id
    calendario_id=$(api_get "/api/configuracao/calendarios-dias-uteis?limit=100" | jq -r '.[] | select(.vigente==true) | .id' | head -1)
    if [ -z "$calendario_id" ]; then
        calendario_id=$(post_tolerante "/api/configuracao/admin/calendarios-dias-uteis" '{
            "versaoDoCalendario": "roteiro-base",
            "diasNaoUteis": [
                {"abrangencia": "NACIONAL", "municipioIbge": null, "municipioNome": null,
                 "municipioUf": null, "data": "2026-12-25", "descricao": "Natal", "uf": null}
            ]
        }')
        [ -z "$calendario_id" ] && calendario_id=$(api_get "/api/configuracao/calendarios-dias-uteis?limit=100" | jq -r '.[] | select(.versaoDoCalendario=="roteiro-base") | .id' | head -1)
        [ -n "$calendario_id" ] || fail "não consegui criar nem localizar o calendário 'roteiro-base' — pré-requisito explícito do roteiro"
        api POST "/api/configuracao/admin/calendarios-dias-uteis/$calendario_id/vigente" "" >/dev/null
    fi

    ok "fase_inscricao=$FASE_INSCRICAO_ID fase_resultado=$FASE_RESULTADO_ID reserva_demografica=$RESERVA_DEMOGRAFICA_ID tipo_processo=$TIPO_PROCESSO_ID condicoes_atendimento=$CONDICAO_ATENDIMENTO_IDS recursos_acessibilidade=$RECURSO_ACESSIBILIDADE_IDS tipos_deficiencia=$TIPO_DEFICIENCIA_IDS base_legal_fada=$BASE_LEGAL_FADA_ID calendario_dias_uteis=$calendario_id"
}

# A Seleção reconhece cidade de prova pela fonte dos valores do fato, tanto
# nos itens quanto nos subitens dos grupos, independentemente do código do fato.
conferir_modelo_inscricao() {
    log "Conferir os fatos do modelo de inscrição"
    local modelo codigos codigo fato
    modelo=$(api_get "/api/configuracao/admin/modelos-formulario/$MODELO_INSCRICAO_ID")
    jq -e '(.conteudo.itens | type == "array") and (.conteudo.grupos | type == "array") and
        all(.conteudo.grupos[]; .subitens | type == "array") and
        all(.conteudo.itens[], .conteudo.grupos[].subitens[]; .fatoCodigo | type == "string" and length > 0)' \
        <<< "$modelo" >/dev/null || fail "modelo de inscrição sem itens ou grupos válidos"
    codigos=$(jq -r '[.conteudo.itens[], .conteudo.grupos[].subitens[] | .fatoCodigo] | unique | .[]' <<< "$modelo")
    MODELO_PEDE_CIDADE_DE_PROVA="false"
    FATO_CIDADE_DE_PROVA=""
    while IFS= read -r codigo; do
        [ -n "$codigo" ] || continue
        fato=$(api_get "/api/configuracao/fatos-candidato/$codigo")
        jq -e '.fato.dominio.codigo != null' <<< "$fato" >/dev/null || fail "não foi possível ler o fato $codigo do modelo"
        if jq -e '.fato.dominio.codigo == "OPCAO" and .fato.fonteValores.codigo == "MUNICIPIOS_PROVA_PRESENCIAL"' <<< "$fato" >/dev/null; then
            MODELO_PEDE_CIDADE_DE_PROVA="true"
            # Usado pelo roteiro irmão ao preencher as inscrições.
            # shellcheck disable=SC2034
            FATO_CIDADE_DE_PROVA="$codigo"
            break
        fi
    done <<< "$codigos"
    ok "modelo pede cidade de prova: $MODELO_PEDE_CIDADE_DE_PROVA"
}

# A listagem é paginada e seu resumo não traz o identificador; é preciso ler
# cada processo para encontrar o identificador exato, inclusive em rascunho.
buscar_processo_por_identificador() {
    local caminho='/api/selecao/processos-seletivos?limit=100' cabecalhos resposta codigo corpo id detalhe proxima
    cabecalhos=$(mktemp)
    while [ -n "$caminho" ]; do
        resposta=$(curl -sS -D "$cabecalhos" -w '\n%{http_code}' "$API_URL$caminho" -H "Authorization: Bearer $TOKEN") || { rm -f "$cabecalhos"; return 1; }
        codigo=$(tail -n1 <<< "$resposta")
        corpo=$(sed '$d' <<< "$resposta")
        [ "$codigo" = "200" ] || { rm -f "$cabecalhos"; fail "não foi possível listar os processos (HTTP $codigo)"; }
        while IFS= read -r id; do
            [ -n "$id" ] || continue
            detalhe=$(api_get "/api/selecao/processos-seletivos/$id")
            if jq -e --arg identificador "$IDENTIFICADOR_PROCESSO" '.identificadorLegivel == $identificador' <<< "$detalhe" >/dev/null; then
                rm -f "$cabecalhos"
                printf '%s\n' "$detalhe"
                return
            fi
        done < <(jq -r '.[].id' <<< "$corpo")
        proxima=$(tr -d '\r' < "$cabecalhos" | grep -i '^link:' | tr ',' '\n' | grep 'rel="next"' | sed -n 's/.*<[^>?]*?\([^>]*\)>.*/\1/p' || true)
        caminho="${proxima:+/api/selecao/processos-seletivos?$proxima}"
    done
    rm -f "$cabecalhos"
}

validar_municipios_de_prova() {
    jq -e 'type == "array" and length > 0 and all(.[]; type == "object" and (.codigoIbge | type == "string" and test("^[0-9]{7}$")) and (.uf | type == "string" and test("^[A-Z]{2}$")))' <<< "$MUNICIPIOS_DE_PROVA" >/dev/null 2>&1 \
        || fail "MUNICIPIOS_DE_PROVA deve ser uma lista JSON não vazia de código IBGE e UF"
}

# Normaliza o instante sem limitar a precisão da fração ao nanossegundo do date.
normalizar_instante() {
    local valor="$1" segundos fracao
    [[ "$valor" =~ ^([0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2})(\.([0-9]+))?(Z|[+-][0-9]{2}:[0-9]{2})$ ]] || return 1
    fracao="${BASH_REMATCH[3]}"
    segundos=$(date -d "${BASH_REMATCH[1]}${BASH_REMATCH[4]}" +%s 2>/dev/null) || return 1
    fracao="${fracao%%"${fracao##*[!0]}"}"
    printf '%s.%s\n' "$segundos" "$fracao"
}

conferir_processo_publicado() {
    log "Conferir a configuração do processo publicado"
    local processo="$1" municipios='[]'
    if [ "$MODELO_PEDE_CIDADE_DE_PROVA" = "true" ]; then
        validar_municipios_de_prova
        municipios="$MUNICIPIOS_DE_PROVA"
    fi
    # O GET do processo traz o modelo de origem e a declaração de prova;
    # a ordem dos municípios é significativa, como na declaração enviada.
    jq -e --arg modelo "$MODELO_INSCRICAO_ID" --argjson tem_prova "$MODELO_PEDE_CIDADE_DE_PROVA" \
        --argjson municipios "$municipios" '
        ([.formularios[] | select(.finalidade.codigo == "INSCRICAO")] | length == 1 and
            (.[0].modeloOrigem.id | type == "string" and ascii_downcase == ($modelo | ascii_downcase))) and
        (.provaPresencial.temProvaPresencial == $tem_prova) and
        ((.provaPresencial.municipios | type) == "array") and
        ([.provaPresencial.municipios[] | {codigoIbge, uf}] == [$municipios[] | {codigoIbge, uf}])' \
        <<< "$processo" >/dev/null 2>&1 \
        || fail "o processo já publicado com este identificador tem outra configuração de modelo de inscrição ou de prova presencial, ou ela não pôde ser conferida; use outro IDENTIFICADOR_PROCESSO"
    ok "modelo de inscrição e declaração de prova presencial conferem"
    if [ -n "${INICIO_INSCRICAO:-}" ] || [ -n "${FIM_INSCRICAO:-}" ]; then
        local snapshot campo nome informado gravado instante_informado instante_gravado
        snapshot=$(api_get "/api/selecao/processos-seletivos/$(jq -r '.id' <<< "$processo")/snapshot-vigente")
        for nome in INICIO_INSCRICAO FIM_INSCRICAO; do
            if [ "$nome" = INICIO_INSCRICAO ]; then
                informado="${INICIO_INSCRICAO:-}"; campo=inicio
            else
                informado="${FIM_INSCRICAO:-}"; campo=fim
            fi
            [ -n "$informado" ] || continue
            gravado=$(jq -r --arg campo "$campo" '.configuracao.periodo[$campo] // empty' <<< "$snapshot")
            instante_informado=$(normalizar_instante "$informado") \
                && instante_gravado=$(normalizar_instante "$gravado") \
                && [ "$instante_informado" = "$instante_gravado" ] \
                || fail "$nome difere do período gravado ou não pôde ser conferido; processo publicado não muda de período pelo roteiro; use outro IDENTIFICADOR_PROCESSO"
        done
        ok "período de inscrição informado confere"
    fi
}

criar_processo() {
    log "Criar ProcessoSeletivo"
    local resp existente situacao
    PROCESSO_JA_PUBLICADO="false"
    existente=$(buscar_processo_por_identificador)
    if [ -n "$existente" ]; then
        PROCESSO_ID=$(jq -r '.id' <<< "$existente")
        situacao=$(jq -r '.situacao.codigo' <<< "$existente")
        case "$situacao" in
            PUBLICADO) conferir_processo_publicado "$existente"; PROCESSO_JA_PUBLICADO="true" ;;
            RASCUNHO) ;;
            *) fail "o identificador já pertence a um processo com situação $situacao" ;;
        esac
        ok "processo reaproveitado ($PROCESSO_ID; $situacao)"
        return
    fi
    resp=$(api POST "/api/selecao/processos-seletivos" "$(jq -n \
        --arg nome "$NOME_PROCESSO" --arg tipo "$TIPO_PROCESSO_ID" \
        --arg unidade "$UNIDADE_ID" --arg identificador "$IDENTIFICADOR_PROCESSO" \
        '{nome: $nome, tipoProcessoOrigemId: $tipo, origemCandidatos: "INSCRICAO_PROPRIA",
          unidadeAdministradoraOrigemId: $unidade, localidadeCodigoIbge: "1504208",
          localidadeNome: "Marabá", localidadeUf: "PA", identificadorLegivel: $identificador}')")
    PROCESSO_ID=$(jq -r 'if type == "object" then .id else . end' <<< "$resp")
    [ -n "$PROCESSO_ID" ] && [ "$PROCESSO_ID" != "null" ] || fail "Criar ProcessoSeletivo falhou: $resp"
    ok "processo=$PROCESSO_ID"
}

definir_algoritmo_contagem_prazo() {
    log "Definir Algoritmo de Contagem de Prazo (CONTAGEM_PRAZO_EXCLUI_DIA_INICIAL)"
    api PUT "/api/selecao/processos-seletivos/$PROCESSO_ID/algoritmo-contagem-prazo" '{
        "codigo": "CONTAGEM_PRAZO_EXCLUI_DIA_INICIAL", "versao": "v1"
    }' >/dev/null
    ok "algoritmo de contagem de prazo definido"
}

definir_atendimento_especializado() {
    log "Definir Atendimento Especializado"
    # Sem prova não há atendimento especializado a ofertar (UNI-REQ-0012), mas a
    # publicação cobra a oferta declarada: o processo sem prova a declara vazia.
    local condicoes="$CONDICAO_ATENDIMENTO_IDS" recursos="$RECURSO_ACESSIBILIDADE_IDS" tipos="$TIPO_DEFICIENCIA_IDS"
    if [ "$MODELO_PEDE_CIDADE_DE_PROVA" != "true" ]; then
        condicoes="[]" recursos="[]" tipos="[]"
    fi
    api PUT "/api/selecao/processos-seletivos/$PROCESSO_ID/atendimento-especializado" "{
        \"condicaoIds\": $condicoes, \"recursoIds\": $recursos, \"tipoDeficienciaIds\": $tipos
    }" >/dev/null
    ok "atendimento especializado definido ($(jq length <<< "$condicoes") condições, $(jq length <<< "$recursos") recursos, $(jq length <<< "$tipos") tipos)"
}

definir_distribuicao_vagas() {
    log "Definir Distribuição de Vagas (Anexo II: 30 vagas, 2 delas V)"
    # AC e as oito cotas da Lei 12.711, mais a V (AC_PCD, Res. CONSEPE 532/2021),
    # retirada da ampla concorrência com quantidade fixa (item 3.3). Com a
    # reserva de 50% do item 3.2, o arredondamento do art. 11 dá as 18 vagas
    # de cota do quadro.
    local modalidades='[
        "70da1000-0000-7000-8000-000000000001",
        "70da1000-0000-7000-8000-000000000002",
        "70da1000-0000-7000-8000-000000000003",
        "70da1000-0000-7000-8000-000000000004",
        "70da1000-0000-7000-8000-000000000005",
        "70da1000-0000-7000-8000-000000000006",
        "70da1000-0000-7000-8000-000000000007",
        "70da1000-0000-7000-8000-000000000008",
        "70da1000-0000-7000-8000-000000000009",
        "70da1000-0000-7000-8000-000000000010"
    ]'
    local ofertas=("${OFERTAS_DISTRIBUIDAS[@]:-$OFERTA_CURSO_ID}")
    local distribuicao oferta_id
    distribuicao=$(api PUT "/api/selecao/processos-seletivos/$PROCESSO_ID/distribuicao-vagas" "$(jq -nc \
        --argjson ofertas "$(printf '%s\n' "${ofertas[@]}" | jq -R . | jq -sc .)" \
        --arg reserva "$RESERVA_DEMOGRAFICA_ID" --argjson modalidades "$modalidades" '
        [$ofertas[] | {
            ofertaCursoId: ., vagasOfertadasBase: 30, percentualDaReservaLegal: 0.5,
            regraDistribuicaoCodigo: "DISTRIBUICAO_LEI_12711_COM_AMPLA_CONCORRENCIA_E_PESSOA_COM_DEFICIENCIA", regraDistribuicaoVersao: "v1",
            regraAjusteCodigo: "AJUSTE_VAGAS_ARTIGO_11_PARAGRAFO_UNICO", regraAjusteVersao: "v1",
            referenciaReservaDemograficaId: $reserva,
            modalidadeIds: $modalidades,
            quadro: [{modalidadeId: "70da1000-0000-7000-8000-000000000010", quantidade: 2}]
        }]')")
    for oferta_id in "${ofertas[@]}"; do
        jq -e --arg id "$oferta_id" '
            [.[] | select(.ofertaCursoOrigemId == $id)] |
            length == 1 and (.[0].quadro | type == "array" and length > 0 and
                all(.[]; (.quantidade | type) == "number") and (map(.quantidade) | add) == 30)
        ' <<< "$distribuicao" >/dev/null \
            || fail "O quadro de vagas da oferta $oferta_id não soma 30 vagas, como exige o Anexo II."
    done
    ok "distribuição de vagas definida"
}

definir_opcoes_de_curso() {
    log "Definir o Número de Opções de Curso ($NUMERO_OPCOES_DE_CURSO)"
    # Uma opção por padrão, porque este processo tem um só curso (Medicina): a
    # segunda exigiria ao menos dois cursos na distribuição de vagas. O roteiro das
    # notas do ENEM, com duas ofertas, define duas.
    api PUT "/api/selecao/processos-seletivos/$PROCESSO_ID/opcoes-de-curso" "{ \"numero\": $NUMERO_OPCOES_DE_CURSO }" >/dev/null
    ok "número de opções de curso definido ($NUMERO_OPCOES_DE_CURSO)"
}

definir_cascata_remanejamento() {
    log "Definir Cascata de Remanejamento (REMANEJAMENTO_ORDEM_LEI_12711)"
    # A matriz tem de coincidir com a declaração da regra congelada
    # REMANEJAMENTO_ORDEM_LEI_12711/v1 (Portaria MEC 704/2025, art. 2º e Anexo) —
    # ver RegraCatalogoSeed.cs, SeedId(19), e é a do Quadro 3 da minuta. Oito
    # origens, sete destinos cada, ordem fixa, terminal sempre AC (fallback,
    # não listado nos destinos).
    local destinos
    destinos=$(jq -nc '
        {
            "LB_PPI": ["LB_Q","LB_PCD","LB_EP","LI_PPI","LI_Q","LI_PCD","LI_EP"],
            "LB_Q": ["LB_PPI","LB_PCD","LB_EP","LI_PPI","LI_Q","LI_PCD","LI_EP"],
            "LB_PCD": ["LB_PPI","LB_Q","LB_EP","LI_PPI","LI_Q","LI_PCD","LI_EP"],
            "LB_EP": ["LB_PPI","LB_Q","LB_PCD","LI_PPI","LI_Q","LI_PCD","LI_EP"],
            "LI_PPI": ["LB_PPI","LB_Q","LB_PCD","LB_EP","LI_Q","LI_PCD","LI_EP"],
            "LI_Q": ["LB_PPI","LB_Q","LB_PCD","LB_EP","LI_PPI","LI_PCD","LI_EP"],
            "LI_PCD": ["LB_PPI","LB_Q","LB_PCD","LB_EP","LI_PPI","LI_Q","LI_EP"],
            "LI_EP": ["LB_PPI","LB_Q","LB_PCD","LB_EP","LI_PPI","LI_Q","LI_PCD"]
        }
        | to_entries
        | map(.key as $origem | .value | to_entries | map({
              modalidadeOrigemCodigo: $origem, ordem: (.key + 1), modalidadeDestinoCodigo: .value
          }))
        | flatten
    ')

    api PUT "/api/selecao/processos-seletivos/$PROCESSO_ID/cascata-remanejamento" "{
        \"regraCodigo\": \"REMANEJAMENTO_ORDEM_LEI_12711\", \"regraVersao\": \"v1\",
        \"fallbackCodigo\": \"AC\", \"destinos\": $destinos
    }" >/dev/null
    ok "cascata de remanejamento definida (Quadro 3)"
}

definir_fada() {
    log "Definir o FADA (FADA_MULTIPLICATIVO)"
    # O modelo da Medicina pede a quem pede o FADA o município do ensino
    # médio ou, a quem concluiu por certificação e escolhe esse caminho, o da
    # residência no período, com as opções da base legal do acréscimo. Recebe
    # o acréscimo quem o pede no formulário (item 1.6): a configuração cita o
    # pedido.
    api PUT "/api/selecao/processos-seletivos/$PROCESSO_ID/bonus-regional" "{
        \"aplica\": true, \"regra\": {\"codigo\": \"FADA_MULTIPLICATIVO\", \"versao\": \"v1\"},
        \"fator\": 1.15, \"teto\": null, \"fatoElegibilidadeCodigo\": \"SOLICITA_FATOR_ATENUACAO\",
        \"baseLegal\": {\"id\": \"$BASE_LEGAL_FADA_ID\"}
    }" >/dev/null
    ok "FADA definido (acréscimo de 15% sobre a média ponderada, itens 1.6 e 5.1)"
}

definir_criterios_desempate() {
    log "Definir Critérios de Desempate"
    # O critério de desempate configurado é a maior idade.
    api PUT "/api/selecao/processos-seletivos/$PROCESSO_ID/criterios-desempate" '[
        {
            "ordem": 1, "regra": {"codigo": "DESEMPATE_MAIOR_IDADE", "versao": "v1"},
            "parametros": {
                "etapaRef": null, "idadeMinima": null, "fato": null, "operador": null, "valor": null,
                "areas": null
            }
        }
    ]' >/dev/null
    ok "critério de desempate definido (maior idade)"
}

definir_classificacao() {
    log "Definir Classificação"
    # A minuta remete os pesos à Resolução nº 966/2026 e não os traz; a nota
    # do ENEM entra importada.
    api PUT "/api/selecao/processos-seletivos/$PROCESSO_ID/classificacao" '{
        "regraCalculoCodigo": "CLASSIFICACAO_IMPORTADA", "regraCalculoVersao": "v1",
        "regraArredondamentoCodigo": null, "regraArredondamentoVersao": null,
        "casasArredondamento": null,
        "regraOrdemAlocacaoCodigo": "ALOCACAO_PRIMEIRA_OPCAO_PRIORITARIA", "regraOrdemAlocacaoVersao": "v1",
        "regrasEliminacao": [], "baseadoEmEnem": true
    }' >/dev/null
    ok "classificação definida com nota do ENEM importada"
}

definir_edicoes_enem() {
    log "Declarar as edições do ENEM admitidas (2024, 2025 e 2026)"
    # Item 1.2: o candidato escolhe a edição entre as três, sem troca depois do
    # fim da inscrição (itens 5.2 e 5.3).
    api PUT "/api/selecao/processos-seletivos/$PROCESSO_ID/fatos/EDICAO_ENEM/opcoes" '{
        "opcoes": [
            {"codigo": "2024", "rotulo": "ENEM 2024"},
            {"codigo": "2025", "rotulo": "ENEM 2025"},
            {"codigo": "2026", "rotulo": "ENEM 2026"}
        ]
    }' >/dev/null
    ok "edições do ENEM declaradas"
}

definir_taxa_inscricao() {
    log "Definir Taxa de Inscrição"
    api PUT "/api/selecao/processos-seletivos/$PROCESSO_ID/taxa-inscricao" \
        '{"cobra": false, "valor": null, "fundamentos": null}' >/dev/null
    ok "sem taxa de inscrição"
}

# A banca que a publicação exige, no formato de uma banca requerida da fase: o tipo é lido do cadastro pelo código (nunca por id
# fixo), sem categorias de documento e com a deliberação colegiada com votação (3 titulares, quórum 2, maioria simples).
banca_requerida() {
    local codigo="$1" tipo_id
    tipo_id=$(api_get "/api/configuracao/tipos-banca?limit=100" \
        | jq -r --arg c "$codigo" '.[] | select((.codigo | if type == "object" then .codigo else . end) == $c) | .id')
    exigir_id "tipo de banca $codigo" "$tipo_id"
    jq -nc --arg t "$tipo_id" '{tipoBancaId: $t, categoriasDocumentoIds: [], regraDeDeliberacao: {
        modo: "COLEGIADA_COM_VOTACAO", integrantes: 3, suplentes: 0, quorum: 2, criterio: "MAIORIA_SIMPLES",
        desempate: "VOTO_DA_PRESIDENCIA", encerramento: "PRESIDENCIA_NO_QUORUM", semUnanimidade: null}}'
}

# Calcula e confere o período antes de qualquer chamada à API. Os valores
# informados são preservados, inclusive o fuso e a precisão da fração.
conferir_periodo_da_inscricao() {
    local hoje nome valor sem_fracao instante fracao fuso inicio_epoch fim_epoch inicio_fracao fim_fracao
    hoje=$(TZ=America/Belem date +%Y-%m-%d)
    inicio_inscricao="${INICIO_INSCRICAO:-$(TZ=America/Belem date -d "$hoje -1 day" +%Y-%m-%dT00:00:00-03:00)}"
    for nome in INICIO_INSCRICAO FIM_INSCRICAO; do
        if [ "$nome" = INICIO_INSCRICAO ]; then
            valor="$inicio_inscricao"
        else
            valor="${FIM_INSCRICAO:-}"
            [ -n "$valor" ] || continue
        fi
        [[ "$valor" =~ ^([0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2})(\.([0-9]+))?(Z|[+-][0-9]{2}:[0-9]{2})$ ]] \
            || fail "$nome deve estar em ISO 8601 com fuso Z ou ±HH:MM."
        [[ "$valor" != 0000-* ]] || fail "$nome contém uma data ou hora inválida."
        sem_fracao="${BASH_REMATCH[1]}${BASH_REMATCH[4]}"
        fracao="${BASH_REMATCH[3]}"
        fuso="${BASH_REMATCH[4]}"
        if [ "$fuso" != Z ]; then
            [ "${fuso:1:2}" -le 23 ] && [ "${fuso:4:2}" -le 59 ] \
                || fail "$nome contém um fuso inválido."
        fi
        instante=$(date -d "$sem_fracao" +%s 2>/dev/null) \
            || fail "$nome contém uma data, hora ou fuso inválido."
        if [ "$nome" = INICIO_INSCRICAO ]; then
            inicio_epoch="$instante"
            inicio_fracao="$fracao"
        else
            fim_epoch="$instante"
            fim_fracao="$fracao"
        fi
    done
    data_inicio=$(TZ=America/Belem date -d "@$inicio_epoch" +%Y-%m-%d)
    deslocamento=$(( ($(date -u -d "$data_inicio" +%s) - $(date -u -d 2026-12-16 +%s)) / 86400 ))
    if [ -z "${FIM_INSCRICAO:-}" ]; then
        fim_inscricao=$(TZ=America/Belem date -d "2027-01-16 $deslocamento days" +%Y-%m-%dT23:59:59-03:00)
        fim_epoch=$(date -d "$fim_inscricao" +%s)
        fim_fracao=""
    else
        fim_inscricao="$FIM_INSCRICAO"
    fi
    # Frações sem zeros à direita podem ser comparadas lexicograficamente,
    # sem perder precisão mesmo quando têm mais de nove casas.
    inicio_fracao="${inicio_fracao%%"${inicio_fracao##*[!0]}"}"
    fim_fracao="${fim_fracao%%"${fim_fracao##*[!0]}"}"
    local periodo_completo=false
    if [ "${1:-}" != parcial ] || { [ -n "${INICIO_INSCRICAO:-}" ] && [ -n "${FIM_INSCRICAO:-}" ]; } || \
        { [ -z "${INICIO_INSCRICAO:-}" ] && [ -z "${FIM_INSCRICAO:-}" ]; }; then
        periodo_completo=true
    fi
    if [ "$periodo_completo" = true ] && \
        { [ "$fim_epoch" -lt "$inicio_epoch" ] || \
        { [ "$fim_epoch" -eq "$inicio_epoch" ] && [[ ! "$fim_fracao" > "$inicio_fracao" ]]; }; }; then
        fail "FIM_INSCRICAO deve ser posterior a INICIO_INSCRICAO."
    fi
    local agora_epoch
    agora_epoch=$(date +%s)
    if [ "$periodo_completo" = true ] && \
        { [ "$agora_epoch" -lt "$inicio_epoch" ] || [ "$agora_epoch" -gt "$fim_epoch" ]; }; then
        warn "A inscrição não está aberta agora; a API só aceita inscrições durante o período informado."
    fi
}

definir_cronograma_fases() {
    log "Definir Cronograma de Fases (Anexo I deslocado, fuso America/Belem)"
    local inicio_resultado fim_resultado inicio_habilitacao fim_habilitacao
    na_data() { TZ=America/Belem date -d "$1 $deslocamento days" +%Y-%m-%dT"$2"-03:00; }
    inicio_resultado=$(na_data 2027-01-26 00:00:00)
    fim_resultado=$(na_data 2027-01-29 00:00:00)
    inicio_habilitacao=$(na_data 2027-01-29 00:00:00)
    fim_habilitacao=$(na_data 2027-02-02 23:59:59)

    local fases banca_homologacao
    banca_homologacao=$(banca_requerida BANCA_HOMOLOGACAO_INSCRICOES)
    fases="[
        {
            \"ordem\": 1, \"faseCanonicaId\": \"$FASE_INSCRICAO_ID\",
            \"inicio\": \"$inicio_inscricao\", \"fim\": \"$fim_inscricao\",
            \"produtos\": [],
            \"emiteParecerIndividual\": false, \"bancasRequeridas\": [], \"regraRecurso\": null
        },
        {
            \"ordem\": 2, \"faseCanonicaId\": \"$FASE_RESULTADO_ID\",
            \"inicio\": \"$inicio_resultado\", \"fim\": \"$fim_resultado\",
            \"produtos\": [{\"atoCodigo\": \"RESULTADO_CLASSIFICACAO\", \"papel\": \"DEFINITIVO\"}],
            \"emiteParecerIndividual\": false,
            \"bancasRequeridas\": [$banca_homologacao], \"regraRecurso\": null, \"tiposDeDecisao\": [\"HOMOLOGACAO_DAS_INSCRICOES\"],
            \"regraDeHomologacao\": {
                \"resultadoAtoCodigo\": \"RESULTADO_CLASSIFICACAO\",
                \"alternativas\": [
                    {\"condicoes\": [{\"tipo\": \"RESULTADO_NO_RETORNO_DO_INEP\", \"edicaoDoExame\": 2024, \"motivoDesfavoravelCodigo\": \"SEM_RESULTADO_INEP\"}]},
                    {\"condicoes\": [{\"tipo\": \"RESULTADO_NO_RETORNO_DO_INEP\", \"edicaoDoExame\": 2025, \"motivoDesfavoravelCodigo\": \"SEM_RESULTADO_INEP\"}]},
                    {\"condicoes\": [{\"tipo\": \"RESULTADO_NO_RETORNO_DO_INEP\", \"edicaoDoExame\": 2026, \"motivoDesfavoravelCodigo\": \"SEM_RESULTADO_INEP\"}]}
                ]
            }
        },
        {
            \"ordem\": 3, \"faseCanonicaId\": \"$FASE_HABILITACAO_ID\",
            \"inicio\": \"$inicio_habilitacao\", \"fim\": \"$fim_habilitacao\",
            \"produtos\": [],
            \"emiteParecerIndividual\": false, \"bancasRequeridas\": [], \"regraRecurso\": null
        },
        {
            \"ordem\": 4, \"faseCanonicaId\": \"$FASE_CHAMADA_ID\",
            \"inicio\": \"$(na_data 2027-02-18 00:00:00)\", \"fim\": \"$(na_data 2027-03-05 23:59:59)\",
            \"produtos\": [], \"emiteParecerIndividual\": false, \"bancasRequeridas\": [], \"regraRecurso\": null
        },
        {
            \"ordem\": 5, \"faseCanonicaId\": \"$FASE_MATRICULA_ID\",
            \"inicio\": \"$(na_data 2027-02-19 00:00:00)\", \"fim\": \"$(na_data 2027-03-11 23:59:59)\",
            \"produtos\": [], \"emiteParecerIndividual\": false, \"bancasRequeridas\": [], \"regraRecurso\": null
        }
    ]"
    # A fase de classificação, quando o roteiro declara uma etapa nela, ocupa o
    # intervalo entre o fim da inscrição e o resultado.
    if [ -n "${FASE_CLASSIFICACAO_ID:-}" ]; then
        fases=$(jq -c --arg fase "$FASE_CLASSIFICACAO_ID" --arg inicio "$(na_data 2027-01-17 00:00:00)" --arg fim "$(na_data 2027-01-25 23:59:59)" \
            --argjson banca_notas "$(banca_requerida BANCA_IMPORTACAO_NOTAS_INEP)" '
            [.[0], {ordem: 2, faseCanonicaId: $fase, inicio: $inicio, fim: $fim, produtos: [],
                emiteParecerIndividual: false, bancasRequeridas: [$banca_notas], regraRecurso: null}]
            + (.[1:] | map(.ordem += 1))' <<< "$fases")
    fi
    # Toda fase precisa de unidade responsável para o processo publicar; no roteiro, é a
    # unidade do processo.
    fases=$(jq -c --arg unidade "$UNIDADE_ID" 'map(. + {unidadeResponsavelId: $unidade})' <<< "$fases")
    api PUT "/api/selecao/processos-seletivos/$PROCESSO_ID/cronograma-fases" "$fases" >/dev/null
    ok "cronograma definido — inscrição de $inicio_inscricao a $fim_inscricao"
}

buscar_motivo_da_homologacao() {
    local caminho='/api/selecao/motivos-de-decisao?tipo=HOMOLOGACAO_DAS_INSCRICOES&limit=100'
    local cabecalhos resposta codigo corpo motivo proxima
    cabecalhos=$(mktemp)
    while [ -n "$caminho" ]; do
        resposta=$(curl -sS -D "$cabecalhos" -w '\n%{http_code}' "$API_URL$caminho" \
            -H "Authorization: Bearer $TOKEN") || { rm -f "$cabecalhos"; return 1; }
        codigo=$(tail -n1 <<< "$resposta")
        corpo=$(sed '$d' <<< "$resposta")
        if [ "$codigo" != "200" ]; then
            rm -f "$cabecalhos"
            fail "GET $caminho → $codigo: $corpo"
        fi
        motivo=$(jq -r '[.[] | select(.codigo == "SEM_RESULTADO_INEP" and .sentido.codigo == "DESFAVORAVEL") | .id][0] // empty' <<< "$corpo")
        if [ -n "$motivo" ]; then
            rm -f "$cabecalhos"
            printf '%s\n' "$motivo"
            return
        fi
        proxima=$(tr -d '\r' < "$cabecalhos" | grep -i '^link:' | tr ',' '\n' \
            | grep 'rel="next"' | sed -n 's/.*<[^>?]*?\([^>]*\)>.*/\1/p' || true)
        caminho="${proxima:+/api/selecao/motivos-de-decisao?$proxima}"
    done
    rm -f "$cabecalhos"
}

escolher_motivo_da_homologacao() {
    log "Escolher o motivo desfavorável da homologação das inscrições"
    local motivo_id
    motivo_id=$(post_tolerante "/api/selecao/admin/motivos-de-decisao" '{
        "tipo": "HOMOLOGACAO_DAS_INSCRICOES", "codigo": "SEM_RESULTADO_INEP",
        "descricao": "Sem resultado no retorno do INEP para a edição indicada",
        "subclassificacao": null, "sentido": "DESFAVORAVEL"
    }')
    if [ -z "$motivo_id" ]; then
        motivo_id=$(buscar_motivo_da_homologacao)
    fi
    exigir_id "motivo da homologação" "$motivo_id"
    api PUT "/api/selecao/processos-seletivos/$PROCESSO_ID/fases/$FASE_RESULTADO_ID/motivos-de-decisao/$motivo_id" >/dev/null
    ok "motivo da homologação escolhido"
}

aplicar_modelo_inscricao() {
    log "Aplicar o modelo de inscrição ($MODELO_INSCRICAO_ID)"
    api POST "/api/selecao/admin/processos-seletivos/$PROCESSO_ID/formularios/aplicacoes-de-modelo" \
        "{\"modeloId\": \"$MODELO_INSCRICAO_ID\"}" >/dev/null
    ok "modelo aplicado"
}

# Vincula o formulário de inscrição (já criado pela aplicação do modelo) à fase
# INSCRICAO do cronograma DESTE processo. "Aplicar modelo" não fixa a fase —
# ela é por definição processo-específica, e só existe depois do cronograma.
# As páginas replicam exatamente as seções/blocos de ConteudoDaInscricao()
# (SementePsrMedicina2027) — reenviar a mesma estrutura, com o FaseId
# acrescentado, não recria nem descarta os itens já aplicados (o handler só
# substitui os itens quando o formulário nasce agora, não quando já existe).
vincular_fase_do_formulario() {
    log "Vincular o formulário de inscrição à fase do cronograma"
    local processo fase_inscricao_processo_id apresentado titulo paginas

    processo=$(api_get "/api/selecao/processos-seletivos/$PROCESSO_ID")
    fase_inscricao_processo_id=$(jq -r '.cronogramaFases[] | select(.codigo=="INSCRICAO") | .id' <<< "$processo")
    [ -n "$fase_inscricao_processo_id" ] || fail "fase INSCRICAO não encontrada no cronograma do processo $PROCESSO_ID"

    # Lê a estrutura (título + páginas) que a aplicação do modelo já criou, em
    # vez de copiá-la à mão do seed — sobrevive a qualquer mudança no modelo
    # da Medicina. DADOS_BASICOS é excluída: o handler do PUT já a repõe por
    # conta própria (ConjuntoBasicoDaInscricao.MesclarPaginas) a partir da
    # referência do processo — reenviá-la é redundante, e arriscaria recusa
    # por SecaoReservadaAlterada se a apresentação um dia divergir, por
    # pouco que seja, da referência que o handler usa para comparar.
    # A projeção do formulário apresentado
    # não traz a exibição condicional de cada página (a projeção não
    # tem esse campo) — hoje o modelo da Medicina não usa exibição condicional
    # em nenhuma seção/bloco, então "exibicao: null" aqui reflete o estado
    # real; um modelo futuro com seção condicional exigiria uma fonte própria
    # para esse dado, não esta leitura.
    apresentado=$(curl -sS -w '\n%{http_code}' -H "Authorization: Bearer $TOKEN" -H "Accept: application/vnd.uniplus.formulario.v2+json" \
        "$API_URL/api/selecao/admin/processos-seletivos/$PROCESSO_ID/formularios/INSCRICAO/apresentacao")
    [ "$(tail -n1 <<< "$apresentado")" = "200" ] || fail "GET formulário apresentado falhou: $(sed '$d' <<< "$apresentado")"
    apresentado=$(sed '$d' <<< "$apresentado")
    titulo=$(jq -r '.formulario.titulo' <<< "$apresentado")
    [ -n "$titulo" ] && [ "$titulo" != "null" ] || fail "formulário INSCRICAO sem título — a aplicação do modelo falhou em silêncio?"
    paginas=$(jq -c '[.formulario.paginas[] | select(.codigo != "DADOS_BASICOS") | {codigo, ordem, tipo, bloco, apresentacao, exibicao: null}]' <<< "$apresentado")
    [ "$(jq 'length' <<< "$paginas")" -gt 0 ] || fail "formulário INSCRICAO sem páginas — a aplicação do modelo falhou em silêncio?"

    api PUT "/api/selecao/admin/processos-seletivos/$PROCESSO_ID/formularios/INSCRICAO" "$(jq -nc \
        --arg fase "$fase_inscricao_processo_id" --arg titulo "$titulo" --argjson paginas "$paginas" \
        '{faseId: $fase, titulo: $titulo, paginas: $paginas}')" >/dev/null
    ok "formulário vinculado à fase $fase_inscricao_processo_id"
}

aplicar_modelo_habilitacao() {
    log "Aplicar e vincular o modelo de habilitação"
    local modelo_id processo fase_id apresentado titulo paginas
    modelo_id="5e3d0006-0000-7000-8000-000000000002"
    processo=$(api_get "/api/selecao/processos-seletivos/$PROCESSO_ID")
    fase_id=$(jq -r '.cronogramaFases[] | select(.codigo == "HABILITACAO") | .id' <<< "$processo")
    exigir_id "fase HABILITACAO" "$fase_id"
    api POST "/api/selecao/admin/processos-seletivos/$PROCESSO_ID/formularios/aplicacoes-de-modelo" \
        "{\"modeloId\": \"$modelo_id\"}" >/dev/null
    apresentado=$(curl -sS -w '\n%{http_code}' -H "Authorization: Bearer $TOKEN" -H "Accept: application/vnd.uniplus.formulario.v2+json" \
        "$API_URL/api/selecao/admin/processos-seletivos/$PROCESSO_ID/formularios/HABILITACAO/apresentacao")
    [ "$(tail -n1 <<< "$apresentado")" = "200" ] || fail "não foi possível ler o formulário de habilitação: $(sed '$d' <<< "$apresentado")"
    apresentado=$(sed '$d' <<< "$apresentado")
    jq -e '[.formulario.paginas[] | select((.codigo == "CONCLUSAO_ENSINO_MEDIO" and .tipo == "SECAO") or (.codigo == "COMPOSICAO_FAMILIAR" and .tipo == "SECAO") or (.codigo == "DOCUMENTOS" and .tipo == "BLOCO" and .bloco == "COMPROVACAO_DOCUMENTAL") or (.codigo == "REVISAO" and .tipo == "BLOCO" and .bloco == "REVISAO_E_ACEITE")) | .codigo] | unique | length == 4' <<< "$apresentado" >/dev/null \
        || fail "formulário de habilitação sem as seções ou blocos esperados"
    titulo=$(jq -r '.formulario.titulo // empty' <<< "$apresentado")
    [ -n "$titulo" ] || fail "formulário de habilitação sem título"
    paginas=$(jq -c '[.formulario.paginas[] | {codigo, ordem, tipo, bloco, apresentacao, exibicao: null}]' <<< "$apresentado")
    api PUT "/api/selecao/admin/processos-seletivos/$PROCESSO_ID/formularios/HABILITACAO" "$(jq -nc \
        --arg fase "$fase_id" --arg titulo "$titulo" --argjson paginas "$paginas" \
        '{faseId: $fase, titulo: $titulo, paginas: $paginas}')" >/dev/null
    apresentado=$(curl -sS -w '\n%{http_code}' -H "Authorization: Bearer $TOKEN" -H "Accept: application/vnd.uniplus.formulario.v2+json" \
        "$API_URL/api/selecao/admin/processos-seletivos/$PROCESSO_ID/formularios/HABILITACAO/apresentacao")
    [ "$(tail -n1 <<< "$apresentado")" = "200" ] || fail "não foi possível conferir o formulário de habilitação"
    sed '$d' <<< "$apresentado" | jq -e '[.formulario.paginas[] | select((.codigo == "CONCLUSAO_ENSINO_MEDIO" and .tipo == "SECAO") or (.codigo == "COMPOSICAO_FAMILIAR" and .tipo == "SECAO") or (.codigo == "DOCUMENTOS" and .tipo == "BLOCO" and .bloco == "COMPROVACAO_DOCUMENTAL") or (.codigo == "REVISAO" and .tipo == "BLOCO" and .bloco == "REVISAO_E_ACEITE")) | .codigo] | unique | length == 4' >/dev/null \
        || fail "formulário de habilitação vinculado sem as seções ou blocos esperados"
    ok "modelo de habilitação vinculado à fase $fase_id"
}

documentos_comuns_da_habilitacao() {
    local fase="$1" bases="$2"
    jq -nc --arg fase "$fase" --argjson tipos "$TIPOS_DOCUMENTO" --argjson bases "$bases" '
        def id($codigo): ([$tipos[] | select(.codigo == $codigo) | .id][0] // error("tipo de documento ausente: " + $codigo));
        def documento($codigo; $prazo): {forma: "DOCUMENTO", documento: {
            tipoDocumento: {id: id($codigo)}, momento: {faseId: $fase, finalidade: "HABILITACAO"},
            aplicabilidade: {tipo: "GERAL", condicoes: []},
            indeferimento: {obrigatorio: true, consequencia: "ELIMINA", basesLegais: $bases},
            requisitos: {formatosPermitidos: "QUALQUER", tamanhoMaximoBytes: null, prazoDeEmissao: $prazo,
                validade: {escolha: "NAO_EXIGE_DOCUMENTO_VALIDO"}, modelo: null}, fatosComprovados: []}};
        def sem_prazo: {escolha: "SEM_PRAZO"};
        (["FOTO_3X4", "RG", "HISTORICO_ENSINO_MEDIO"] | map(documento(.; sem_prazo))) +
        [documento("COMPROVANTE_RESIDENCIA"; {escolha: "COM_PRAZO", valor: 90, unidade: "DIAS", referencia: {tipo: "DATA_DA_APRESENTACAO"}, efeitoForaDoPrazo: "RECUSAR_DOCUMENTO"}),
         {forma: "QUANTIDADE_MINIMA_DE_ITENS", cardinalidade: {quantidadeMinima: 1}, indeferimento: {consequencia: "ELIMINA", basesLegais: $bases},
          itens: [documento("CERTIFICADO_ENSINO_MEDIO"; sem_prazo),
                  (documento("DECLARACAO_CONCLUSAO_ENSINO_MEDIO"; sem_prazo) |
                   .documento.indeferimento.basesLegais |= map(.observacao = "Aceita quando o certificado ainda não tiver sido recebido, se atualizada e emitida há no máximo 90 dias ou dentro do prazo de validade da instituição de origem. A pessoa candidata ou responsável legal deve assinar termo de responsabilidade e entregar o certificado em até 60 dias após a matrícula."))] |
                 map(.documento.indeferimento.consequencia = null)},
         {forma: "QUANTIDADE_MINIMA_DE_ITENS", cardinalidade: {quantidadeMinima: 1}, indeferimento: {consequencia: "ELIMINA", basesLegais: $bases},
          itens: (["CERTIDAO_NASCIMENTO", "CERTIDAO_CASAMENTO"] | map(documento(.; sem_prazo) | .documento.indeferimento.consequencia = null))}]'
}

documentos_do_fada() {
    local fase="$1" bases="$2"
    jq -nc --arg fase "$fase" --arg historico "$TIPO_DOCUMENTO_HISTORICO_ID" --arg residencia "$TIPO_DOCUMENTO_RESIDENCIA_ID" --argjson bases "$bases" '
        def regra($tipo; $condicoes; $observacao): {forma: "DOCUMENTO", documento: {
            tipoDocumento: {id: $tipo}, momento: {faseId: $fase, finalidade: "HABILITACAO"},
            aplicabilidade: {tipo: "CONDICIONAL", condicoes: $condicoes},
            indeferimento: {obrigatorio: true, consequencia: "REMOVE_VANTAGEM", basesLegais: ($bases | map(.observacao = $observacao))},
            requisitos: {formatosPermitidos: "QUALQUER", tamanhoMaximoBytes: null, prazoDeEmissao: {escolha: "SEM_PRAZO"}, validade: {escolha: "NAO_EXIGE_DOCUMENTO_VALIDO"}, modelo: null}, fatosComprovados: []}};
        def pedido($clausula): {clausula: $clausula, fato: "SOLICITA_FATOR_ATENUACAO", operador: "IGUAL", valor: "true"};
        [regra($historico; [pedido(0), {clausula: 0, fato: "FORMA_CONCLUSAO_EM", operador: "ALGUM_DE", valor: "[\"REGULAR\",\"EJA\"]"}, pedido(1), {clausula: 1, fato: "CAMINHO_FATOR_ATENUACAO", operador: "IGUAL", valor: "\"ENSINO_MEDIO\""}]; null),
         regra($residencia; [pedido(0), {clausula: 0, fato: "CAMINHO_FATOR_ATENUACAO", operador: "IGUAL", valor: "\"RESIDENCIA\""}]; "Contas de água ou luz que comprovem residência por pelo menos um ano ininterrupto entre 20/11/2024 e 20/11/2026, em nome da pessoa candidata, de seus pais, cônjuge ou irmãos, com documentos pessoais que comprovem o vínculo com o titular.")]'
}

definir_documentos_exigidos() {
    log "Definir os documentos comuns e do FADA na habilitação"
    local fase_habilitacao_processo_id base_legal processo documentos_do_modelo documentos_comuns documentos_fada
    processo=$(api_get "/api/selecao/processos-seletivos/$PROCESSO_ID")
    fase_habilitacao_processo_id=$(jq -r '.cronogramaFases[] | select(.codigo=="HABILITACAO") | .id' <<< "$processo")
    [ -n "$fase_habilitacao_processo_id" ] || fail "fase HABILITACAO não encontrada no cronograma do processo $PROCESSO_ID"
    base_legal='[{"referencia": "Edital nº XX/2026-CEPS/Unifesspa, itens 1.5 a 1.9 e 9", "abrangencia": "INTERNA_EDITAL", "situacao": "RESOLVIDO", "observacao": null}]'

    # O PUT substitui todas as regras: os documentos que o modelo de inscrição trouxer voltam na
    # entrada, com a base legal resolvida — o modelo a deixa pendente até o CEPS indicar a norma, e
    # a publicação a exige resolvida. O modelo sem prova não traz nenhum.
    documentos_do_modelo=$(jq -c --argjson bases "$base_legal" '
    def codigo: if type == "object" then .codigo else . end;
    def referencia: if . == null then null else {tipo: (.tipo | codigo), data, faseId} end;
    [(.regrasDeComprovacao // [])[] | {
        forma: "DOCUMENTO",
        documento: (.documento | {
            tipoDocumento: {id: .tipoDocumento.id},
            momento: (.momento | {faseId, etapaId, finalidade: (.finalidade | codigo)}),
            aplicabilidade: (.aplicabilidade | {
                tipo: (.tipo | codigo),
                condicoes: [.condicoes[] | {clausula, fato, operador, valor}]
            }),
            indeferimento: (.indeferimento | {obrigatorio, consequencia: (.consequencia | codigo), basesLegais: $bases}),
            requisitos: (.requisitos | {
                formatosPermitidos: (.formatosPermitidos | if type == "array" then
                    map({formato: (.formato | codigo), tamanhoMaximoBytesMax}) else . end), tamanhoMaximoBytes,
                prazoDeEmissao: (if .prazoDeEmissao == null then {escolha: "SEM_PRAZO"} else
                    .prazoDeEmissao | {escolha: (.escolha | codigo), valor, unidade: (.unidade | codigo),
                        referencia: (.referencia | referencia), efeitoForaDoPrazo: (.efeitoForaDoPrazo | codigo)} end),
                validade: (if .validade == null then {escolha: "NAO_EXIGE_DOCUMENTO_VALIDO"} else
                    .validade | {escolha: (.escolha | codigo), referencia: (.referencia | referencia),
                        efeitoDoVencimento: (.efeitoDoVencimento | codigo)} end),
                modelo: (if .modelo == null then null else .modelo | {modeloId} end)
            }),
            fatosComprovados, chave
        })
    }]' <<< "$processo")
    # O modelo com prova sempre traz os documentos do atendimento especializado; sem eles, a
    # aplicação regrediu e o processo sairia publicado sem exigir o laudo.
    if [ "$MODELO_PEDE_CIDADE_DE_PROVA" = "true" ] && [ "$(jq length <<< "$documentos_do_modelo")" -eq 0 ]; then
        fail "o modelo com prova não trouxe os documentos do atendimento especializado"
    fi

    documentos_comuns=$(documentos_comuns_da_habilitacao "$fase_habilitacao_processo_id" "$base_legal")
    documentos_fada=$(documentos_do_fada "$fase_habilitacao_processo_id" "$base_legal")
    api PUT "/api/selecao/processos-seletivos/$PROCESSO_ID/documentos-exigidos" "$(jq -nc \
        --argjson modelo "$documentos_do_modelo" --argjson comuns "$documentos_comuns" --argjson fada "$documentos_fada" \
        '$modelo + $comuns + $fada')" >/dev/null
    ok "documentos comuns e do FADA definidos na fase $fase_habilitacao_processo_id"
}

documento_do_edital() {
    log "Documento do Edital — iniciar upload"
    local resp url_upload content_type
    resp=$(api POST "/api/selecao/processos-seletivos/$PROCESSO_ID/documentos-a-publicar" '{"finalidade":"DOCUMENTO_DO_EDITAL","nomeArquivo":"edital.pdf","tipoDeMidia":"application/pdf"}')
    DOCUMENTO_EDITAL_ID=$(jq -r '.documentoAPublicarId' <<< "$resp")
    url_upload=$(jq -r '.urlUpload' <<< "$resp")
    content_type=$(jq -r '.contentTypeExigido' <<< "$resp")
    [ -n "$DOCUMENTO_EDITAL_ID" ] && [ "$DOCUMENTO_EDITAL_ID" != "null" ] || fail "Iniciar upload do edital falhou: $resp"

    log "Documento do Edital — PUT direto no MinIO"
    local upload_http_code
    local -a redirecionamento=()
    [ -z "${ARMAZENAMENTO_CONNECT_TO:-}" ] || redirecionamento=(--connect-to "$ARMAZENAMENTO_CONNECT_TO")
    [ -f "$EDITAL_PDF" ] || fail "PDF do edital não encontrado: $EDITAL_PDF"
    upload_http_code=$(curl "${redirecionamento[@]}" -sS -o /dev/null -w '%{http_code}' -X PUT "$url_upload" -H "Content-Type: $content_type" --data-binary "@$EDITAL_PDF")
    [ "$upload_http_code" = "200" ] || fail "PUT no MinIO falhou (http=$upload_http_code) — url presumivelmente expirada ou host interno (minio:9000) não resolvido pelo cliente"

    log "Documento do Edital — confirmar upload"
    api POST "/api/selecao/processos-seletivos/$PROCESSO_ID/documentos-a-publicar/$DOCUMENTO_EDITAL_ID/confirmacao" "" >/dev/null
    ok "documento do edital confirmado ($DOCUMENTO_EDITAL_ID)"
}

declarar_prova_presencial() {
    log "Declarar prova presencial"
    local corpo municipio codigo uf resposta http_code
    if [ "$MODELO_PEDE_CIDADE_DE_PROVA" = "true" ]; then
        validar_municipios_de_prova
        while IFS= read -r municipio; do
            codigo=$(jq -r '.codigoIbge' <<< "$municipio")
            uf=$(jq -r '.uf' <<< "$municipio")
            resposta=$(curl -sS -w '\n%{http_code}' "$GEO_URL/api/cidades/$codigo" -H "Authorization: Bearer $TOKEN") \
                || fail "não foi possível consultar o cadastro geográfico"
            http_code=$(tail -n1 <<< "$resposta")
            [ "$http_code" != "404" ] || fail "município de prova $codigo não encontrado no cadastro geográfico"
            [ "$http_code" = "200" ] || fail "consulta do município de prova ao cadastro geográfico falhou (HTTP $http_code)"
            jq -e --arg uf "$uf" '.uf == $uf and (.nome | type == "string" and length > 0)' <<< "$(sed '$d' <<< "$resposta")" >/dev/null \
                || fail "município de prova $codigo não pertence à UF $uf ou não tem nome no cadastro geográfico"
        done < <(jq -c '.[]' <<< "$MUNICIPIOS_DE_PROVA")
        corpo=$(jq -nc --argjson municipios "$MUNICIPIOS_DE_PROVA" '{temProvaPresencial: true, municipios: [$municipios[] | {codigoIbge, uf}]}')
    else
        corpo='{"temProvaPresencial": false, "municipios": []}'
    fi
    api PUT "/api/selecao/processos-seletivos/$PROCESSO_ID/prova-presencial" "$corpo" >/dev/null
    ok "prova presencial declarada: $MODELO_PEDE_CIDADE_DE_PROVA"
}

publicar() {
    log "Publicar ProcessoSeletivo"
    # Data e ano no fuso America/Belem, como o cronograma — date -u (UTC)
    # já virou o dia seguinte entre 21h e 23h59 em Belém.
    local ano data_publicacao
    ano=$(TZ=America/Belem date +%Y)
    data_publicacao=$(TZ=America/Belem date +%Y-%m-%d)
    api POST "/api/selecao/processos-seletivos/$PROCESSO_ID/publicacao" "{
        \"numero\": \"XX/$ano\",
        \"documentoEditalId\": \"$DOCUMENTO_EDITAL_ID\",
        \"ato\": {
            \"orgao\": \"CEPS\", \"serie\": \"EDITAL\", \"ano\": $ano,
            \"dataDoAto\": \"$data_publicacao\",
            \"assinante\": \"Reitor da Unifesspa\", \"tipoAtoCodigo\": \"EDITAL_ABERTURA\"
        }
    }" >/dev/null
    ok "processo publicado"
}

divulgar() {
    log "Divulgação pública do processo"
    api PUT "/api/selecao/processos-seletivos/$PROCESSO_ID/divulgacao" \
        '{"camposPublicos": ["NUMERO_INSCRICAO", "NOME"],
          "justificativa": "Item 1.9 do edital: a inscrição autoriza divulgar nome, número de inscrição, pontuação, colocação e resultado."}' >/dev/null
    ok "processo divulgado"
}

conferir_resultado() {
    log "Conferindo o resultado"
    local snapshot formulario tentativa
    snapshot=$(api_get "/api/selecao/processos-seletivos/$PROCESSO_ID/snapshot-vigente")
    echo "$snapshot" | jq -r '{identificadorLegivel: .configuracao.identificadorLegivel, divulgacao: .configuracao.divulgacao}' >&2

    # A projeção da vitrine é assíncrona — alguns segundos depois do 204.
    for tentativa in 1 2 3 4 5; do
        formulario=$(curl -sS "$API_URL/api/selecao/processos-seletivos/$PROCESSO_ID/formularios/INSCRICAO")
        if jq -e '.formulario.paginas' <<< "$formulario" >/dev/null 2>&1; then
            ok "formulário público de inscrição responde (tentativa $tentativa)"
            break
        fi
        sleep 5
    done
    jq -e '.formulario.paginas' <<< "$formulario" >/dev/null 2>&1 || fail "formulário público não respondeu como esperado: $(head -c 300 <<< "$formulario")"

    # GET certames/{id} aceita o Guid direto — evita depender da página em que
    # o processo cai em GET /certames (paginado por cursor).
    local certame
    for tentativa in 1 2 3 4 5; do
        certame=$(curl -sS -w '\n%{http_code}' "$API_URL/api/selecao/certames/$PROCESSO_ID")
        [ "$(tail -n1 <<< "$certame")" = "200" ] && break
        sleep 5
    done
    if [ "$(tail -n1 <<< "$certame")" = "200" ]; then
        ok "processo aparece na vitrine (GET /api/selecao/certames/{id})"
    else
        fail "processo ainda não aparece na vitrine (tentativa $tentativa)"
    fi

    ok "configuração publicada conferida"
}

main() {
    for cmd in jq curl date; do command -v "$cmd" >/dev/null 2>&1 || fail "comando ausente: $cmd"; done
    # GNU date (-d, --version) e /proc (idem()) — como o resto do repositório,
    # roda em Linux; não há suporte a macOS/BSD.
    date --version >/dev/null 2>&1 || fail "precisa do GNU date (-d), não do date do BSD/macOS"
    [ -r /proc/sys/kernel/random/uuid ] || fail "precisa de /proc/sys/kernel/random/uuid (Linux)"
    conferir_periodo_da_inscricao parcial
    obter_token
    garantir_acesso_institucional
    garantir_representantes
    garantir_protecao_do_catalogo
    setup_publicacoes
    setup_organizacao
    garantir_acesso_na_unidade "$UNIDADE_ID"
    setup_configuracao
    conferir_modelo_inscricao
    criar_processo
    if [ "$PROCESSO_JA_PUBLICADO" = "true" ]; then
        conferir_resultado
        ok "PROCESSO_ID=$PROCESSO_ID — roteiro concluído"
        return
    fi
    conferir_periodo_da_inscricao
    definir_algoritmo_contagem_prazo
    definir_atendimento_especializado
    definir_distribuicao_vagas
    definir_opcoes_de_curso
    definir_cascata_remanejamento
    definir_fada
    definir_criterios_desempate
    definir_classificacao
    definir_edicoes_enem
    definir_taxa_inscricao
    definir_cronograma_fases
    escolher_motivo_da_homologacao
    aplicar_modelo_inscricao
    vincular_fase_do_formulario
    aplicar_modelo_habilitacao
    definir_documentos_exigidos
    documento_do_edital
    divulgar
    declarar_prova_presencial
    publicar
    conferir_resultado
    ok "PROCESSO_ID=$PROCESSO_ID — roteiro concluído"
}

# Executado direto, roda o roteiro; lido com `source`, só expõe as funções.
if [[ "${BASH_SOURCE[0]}" == "$0" ]]; then
    main "$@"
else
    # O roteiro irmão chama definir_cronograma_fases sem passar pelo main acima.
    conferir_periodo_da_inscricao parcial
fi
