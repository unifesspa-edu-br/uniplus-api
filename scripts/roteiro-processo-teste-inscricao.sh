#!/usr/bin/env bash
# roteiro-processo-teste-inscricao.sh — cria o processo local de validação: o
# Processo Seletivo para o Curso de Medicina Unifesspa 2027, pela minuta do
# edital nº XX/2026-CEPS/Unifesspa, com o modelo de inscrição sem prova
# aplicado, publicado e divulgado, e a inscrição aberta no dia da execução.
#
# Os cadastros (unidades, campus, curso, oferta) têm os códigos da minuta e são
# reaproveitados numa segunda execução. O processo é um só: com o identificador
# legível já em uso, a criação recusa e o roteiro para — limpe a base antes.
#
# O que a minuta não define fica sem valor inventado:
#   - os pesos por área da Resolução 805/2024 não constam dela, então a nota do
#     ENEM entra importada (CLASSIFICACAO-IMPORTADA) até o cadastro dos pesos;
#   - o percentual quilombola da reserva demográfica;
#   - a lista completa das regiões de influência do bônus (item 1.5): entram os
#     oito municípios-polo que a minuta nomeia;
#   - as datas em branco do Anexo I: o cronograma tem a inscrição, o
#     resultado final e a habilitação, que abre na 1ª chamada e dura cinco
#     dias.
#
# Sequência:
#   Setup (Publicações/Organização/Configuração) →
#   Criar ProcessoSeletivo → Oferta de Atendimento → Distribuição de Vagas
#   (Anexo II) → Cascata de Remanejamento (Quadro 3) → Bônus Regional (item
#   1.6) → Critérios de Desempate (item 6.3) → Classificação e eliminação
#   (itens 5.6 e 8.1) → Taxa de Inscrição (gratuita, item 2.2) → Cronograma de
#   Fases → aplicar o modelo de inscrição da Medicina → vincular a fase do
#   formulário → documentos que comprovam o Fator na habilitação (os do modelo, se
#   houver, são mantidos) → Documento do
#   Edital → Divulgação (sempre ANTES de publicar: depois da publicação o
#   processo só aceita mutação por retificação) → Publicar.
#
# Pré-requisitos: stack local do docker compose no ar, com uniplus-api,
# keycloak e os demais serviços de infraestrutura (ver docker/README.md ou
# CONTRIBUTING.md — não precisa dos *-web nem do openldap), jq, curl. Roda
# contra o realm `unifesspa`.
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
#   EDITAL_PDF    (default: scripts/edital-de-teste.pdf) — PDF enviado como
#                 documento do edital, por exemplo a minuta
#   NOME_PROCESSO (default: o nome do processo da Medicina 2027)
#   IDENTIFICADOR_PROCESSO (default: psm-2027) — o identificador legível é único:
#                 outro processo na mesma base precisa de outro valor
#
# O arquivo também pode ser lido com `source` por um roteiro irmão (ver
# roteiro-processo-teste-notas-do-enem.sh): as funções ficam disponíveis e o
# `main` só roda quando o arquivo é executado.

set -euo pipefail

KC_URL="${KC_URL:-http://localhost:8080}"
KC_REALM="${KC_REALM:-unifesspa}"
API_URL="${API_URL:-http://localhost:5200}"
ADMIN_USER="${ADMIN_USER:-admin}"
# Mesmo default de setup-keycloak-dev.sh — se o setup rodou com TEST_PASSWORD
# diferente, o admin está com esse valor, não com Uni+Teste26.
ADMIN_PASS="${ADMIN_PASS:-${TEST_PASSWORD:-Uni+Teste26}}"
MODELO_INSCRICAO_ID="${MODELO_INSCRICAO_ID:-5e3d0006-0000-7000-8000-000000000004}"
MODELO_COM_PROVA_ID="5e3d0006-0000-7000-8000-000000000003"
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

# Falha alto quando um id de catálogo (criado ou reaproveitado via fallback
# de busca) sai vazio, em vez de deixar "" descer em silêncio para o próximo
# PUT/POST e estourar um 400/422 longe da causa real.
exigir_id() { [ -n "$2" ] || fail "$1 ficou vazio (409 sem entrada equivalente encontrada no fallback de busca?)"; }

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
    else
        fail "POST $path → $http_code: $payload"
    fi
}

setup_publicacoes() {
    log "Setup — Publicações (tipos de ato)"
    post_tolerante "/api/publicacoes/admin/tipos-ato" '{
        "codigo": "EDITAL_ABERTURA", "nome": "Edital de abertura",
        "congelaConfiguracao": true, "unicoPorObjeto": true,
        "efeitoIrreversivel": false, "publicavelComoAvulso": false, "exigeAtoCorrigido": false, "vigenciaInicio": "2020-01-01",
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
    UNIDADE_ID=$(criar_unidade "CEPS" "Centro de Processos Seletivos" "centro" false)
    UNIDADE_OFERTANTE_ID=$(criar_unidade "IESB" "Instituto de Estudos em Saúde e Biológicas" "instituto" true)
    ok "unidade=$UNIDADE_ID unidade_ofertante=$UNIDADE_OFERTANTE_ID"
}

# $1=sigla $2=nome $3=tipo $4=unidadeAcademica — devolve o id, criado ou reaproveitado pelo código.
criar_unidade() {
    local sigla="$1" nome="$2" tipo="$3" academica="$4" id
    id=$(post_tolerante "/api/organizacao/admin/unidades" "{
        \"nome\": \"$nome\", \"alias\": \"$sigla\",
        \"slug\": \"${sigla,,}\", \"sigla\": \"$sigla\",
        \"codigo\": \"$sigla\", \"unidadeSuperiorId\": null,
        \"tipo\": \"$tipo\", \"unidadeAcademica\": $academica,
        \"vigenciaInicio\": \"2020-01-01\", \"vigenciaFim\": null,
        \"cidadeCodigoIbge\": \"1504208\", \"cidadeNome\": \"Marabá\", \"cidadeUf\": \"PA\",
        \"origem\": \"criadoNoUniPlus\"
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
    # O local de oferta e a oferta de curso não têm código próprio: reaproveita
    # pelo campus e pelo curso antes de criar.
    LOCAL_OFERTA_ID=$(api_get "/api/configuracao/locais-oferta?limit=100" | jq -r --arg c "$CAMPUS_ID" '.[] | select(.campusResponsavelId==$c) | .id' | head -1)
    if [ -z "$LOCAL_OFERTA_ID" ]; then
        LOCAL_OFERTA_ID=$(post_tolerante "/api/configuracao/admin/locais-oferta" "{
            \"tipo\": \"campusSede\", \"campusResponsavelId\": \"$CAMPUS_ID\",
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
    OFERTA_CURSO_ID=$(api_get "/api/configuracao/ofertas-curso?limit=100" | jq -r --arg c "$CURSO_ID" '.[] | select(.cursoId==$c) | .id' | head -1)
    if [ -z "$OFERTA_CURSO_ID" ]; then
        OFERTA_CURSO_ID=$(post_tolerante "/api/configuracao/admin/ofertas-curso" "{
            \"cursoId\": \"$CURSO_ID\", \"localOfertaId\": \"$LOCAL_OFERTA_ID\",
            \"unidadeOfertanteOrigemId\": \"$UNIDADE_OFERTANTE_ID\", \"programaDeOferta\": \"REGULAR\",
            \"regimeDeFuncionamento\": \"EXTENSIVO\", \"formatoPedagogico\": \"PRESENCIAL\",
            \"regimeDeTurno\": \"REGULAR\", \"turnos\": [\"MATUTINO\"],
            \"eMecCodigo\": null, \"codigoSga\": null, \"vagasAnuaisAutorizadas\": null,
            \"baseLegal\": null, \"atoAutorizacaoMec\": null
        }")
    fi
    exigir_id "OFERTA_CURSO_ID" "$OFERTA_CURSO_ID"
    ok "campus=$CAMPUS_ID local_oferta=$LOCAL_OFERTA_ID curso=$CURSO_ID oferta_curso=$OFERTA_CURSO_ID"

    log "Setup — fases canônicas e referência de reserva demográfica"
    # As fases vêm do catálogo semeado (FaseCanonicaSeed).
    local fases
    fases=$(api_get "/api/configuracao/fases-canonicas?limit=100")
    FASE_INSCRICAO_ID=$(jq -r '.[] | select(.codigo=="INSCRICAO") | .id' <<< "$fases")
    FASE_RESULTADO_ID=$(jq -r '.[] | select(.codigo=="RESULTADO_FINAL") | .id' <<< "$fases")
    FASE_HABILITACAO_ID=$(jq -r '.[] | select(.codigo=="HABILITACAO") | .id' <<< "$fases")
    exigir_id "FASE_INSCRICAO_ID" "$FASE_INSCRICAO_ID"
    exigir_id "FASE_RESULTADO_ID" "$FASE_RESULTADO_ID"
    exigir_id "FASE_HABILITACAO_ID" "$FASE_HABILITACAO_ID"

    # Os tipos de documento vêm do catálogo semeado (TipoDocumentoSeed): o
    # histórico prova o ensino médio cursado na área, e o comprovante de
    # residência, a residência no período.
    local tipos_documento
    tipos_documento=$(api_get "/api/configuracao/tipos-documento?limit=100")
    TIPO_DOCUMENTO_HISTORICO_ID=$(jq -r '.[] | select(.codigo=="HISTORICO_ENSINO_MEDIO") | .id' <<< "$tipos_documento")
    TIPO_DOCUMENTO_RESIDENCIA_ID=$(jq -r '.[] | select(.codigo=="COMPROVANTE_RESIDENCIA") | .id' <<< "$tipos_documento")
    exigir_id "TIPO_DOCUMENTO_HISTORICO_ID" "$TIPO_DOCUMENTO_HISTORICO_ID"
    exigir_id "TIPO_DOCUMENTO_RESIDENCIA_ID" "$TIPO_DOCUMENTO_RESIDENCIA_ID"

    # Nota 2 do Anexo II: PPI do Pará pelo Censo 2022 (9,77 + 69,87 + 0,85) e
    # PcD do Pará pelo Censo 2010. A minuta não dá o percentual quilombola: vai
    # o do Pará no Censo 2022 (135.033 pessoas, 1,66%), conferido pelo quadro.
    local censo="IBGE 2022/2010 PA"
    RESERVA_DEMOGRAFICA_ID=$(post_tolerante "/api/configuracao/admin/referencias-reserva-demografica" "{
        \"censoReferencia\": \"$censo\",
        \"ppiPercentual\": 80.49, \"quilombolaPercentual\": 1.66, \"pcdPercentual\": 6.65,
        \"baseLegal\": \"Edital Medicina 2027, Anexo II, nota 2\"
    }")
    [ -z "$RESERVA_DEMOGRAFICA_ID" ] && RESERVA_DEMOGRAFICA_ID=$(api_get "/api/configuracao/referencias-reserva-demografica?limit=100" | jq -r --arg c "$censo" '.[] | select(.censoReferencia==$c) | .id' | head -1)
    exigir_id "RESERVA_DEMOGRAFICA_ID" "$RESERVA_DEMOGRAFICA_ID"

    # PSR (Processo Seletivo Regular) é o tipo de processo do modelo de
    # inscrição semeado da Medicina (SementePsrMedicina2027) — aplicar o
    # modelo exige que o processo seja do mesmo tipo.
    TIPO_PROCESSO_ID=$(api_get "/api/configuracao/tipos-processo?limit=100" | jq -r '.[] | select(.codigo=="PSR") | .id')
    [ -n "$TIPO_PROCESSO_ID" ] || fail "tipo de processo 'PSR' não encontrado no catálogo semeado"

    # O modelo com prova coleta CONDICAO_ATENDIMENTO e RECURSO_ACESSIBILIDADE, cujas
    # opções são as que o processo oferta em atendimento especializado; a conformidade
    # recusa publicar o fato coletável sem ao menos uma opção. O processo sem prova
    # não oferta atendimento: o TIPO_DEFICIENCIA da reserva de vagas oferece os tipos
    # do cadastro, em qualquer processo (UNI-REQ-0012).
    # As condições são as do atendimento especializado do INEP (Edital ENEM
    # 52/2025): atendimento especializado não é só de quem tem deficiência.
    # "PCD" é o código reconhecido por OfertaAtendimentoEspecializado.CodigoCondicaoPcd
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

    # O próprio edital institui o fator (itens 1.5 e 1.6), com os municípios das
    # regiões de influência destes oito polos. A minuta não lista os demais.
    local bonus_identificacao="Edital nº XX/2026-CEPS/Unifesspa, itens 1.5 e 1.6"
    BASE_LEGAL_BONUS_ID=$(post_tolerante "/api/configuracao/admin/base-legal-bonus-regional" "{
        \"tipoInstrumento\": \"EDITAL\", \"identificacao\": \"$bonus_identificacao\",
        \"descricao\": \"Fator de Atenuação das Desigualdades de Acesso\",
        \"municipios\": [
            {\"codigoIbge\": \"1504208\", \"nome\": \"Marabá\", \"uf\": \"PA\"},
            {\"codigoIbge\": \"1506187\", \"nome\": \"Rondon do Pará\", \"uf\": \"PA\"},
            {\"codigoIbge\": \"1508407\", \"nome\": \"Xinguara\", \"uf\": \"PA\"},
            {\"codigoIbge\": \"1507300\", \"nome\": \"São Félix do Xingu\", \"uf\": \"PA\"},
            {\"codigoIbge\": \"1506708\", \"nome\": \"Santana do Araguaia\", \"uf\": \"PA\"},
            {\"codigoIbge\": \"1502152\", \"nome\": \"Canaã dos Carajás\", \"uf\": \"PA\"},
            {\"codigoIbge\": \"2105302\", \"nome\": \"Imperatriz\", \"uf\": \"MA\"},
            {\"codigoIbge\": \"1702109\", \"nome\": \"Araguaína\", \"uf\": \"TO\"}
        ]
    }")
    [ -z "$BASE_LEGAL_BONUS_ID" ] && BASE_LEGAL_BONUS_ID=$(api_get "/api/configuracao/base-legal-bonus-regional?limit=100" | jq -r --arg i "$bonus_identificacao" '.[] | select(.identificacao==$i) | .id' | head -1)
    exigir_id "BASE_LEGAL_BONUS_ID" "$BASE_LEGAL_BONUS_ID"

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
            "versaoDataset": "roteiro-base",
            "diasNaoUteis": [
                {"abrangencia": "NACIONAL", "municipioIbge": null, "municipioNome": null,
                 "municipioUf": null, "data": "2026-12-25", "descricao": "Natal", "uf": null}
            ]
        }')
        [ -z "$calendario_id" ] && calendario_id=$(api_get "/api/configuracao/calendarios-dias-uteis?limit=100" | jq -r '.[] | select(.versaoDataset=="roteiro-base") | .id' | head -1)
        [ -n "$calendario_id" ] || fail "não consegui criar nem localizar o calendário 'roteiro-base' — pré-requisito explícito do roteiro"
        api POST "/api/configuracao/admin/calendarios-dias-uteis/$calendario_id/vigente" "" >/dev/null
    fi

    ok "fase_inscricao=$FASE_INSCRICAO_ID fase_resultado=$FASE_RESULTADO_ID reserva_demografica=$RESERVA_DEMOGRAFICA_ID tipo_processo=$TIPO_PROCESSO_ID condicoes_atendimento=$CONDICAO_ATENDIMENTO_IDS recursos_acessibilidade=$RECURSO_ACESSIBILIDADE_IDS tipos_deficiencia=$TIPO_DEFICIENCIA_IDS base_legal_bonus=$BASE_LEGAL_BONUS_ID calendario_dias_uteis=$calendario_id"
}

criar_processo() {
    log "Criar ProcessoSeletivo"
    local resp
    resp=$(api POST "/api/selecao/processos-seletivos" "{
        \"nome\": \"$NOME_PROCESSO\",
        \"tipoProcessoOrigemId\": \"$TIPO_PROCESSO_ID\",
        \"origemCandidatos\": \"inscricaoPropria\",
        \"unidadeAdministradoraOrigemId\": \"$UNIDADE_ID\",
        \"localidadeCodigoIbge\": \"1504208\", \"localidadeNome\": \"Marabá\", \"localidadeUf\": \"PA\",
        \"identificadorLegivel\": \"$IDENTIFICADOR_PROCESSO\"
    }")
    PROCESSO_ID=$(jq -r 'if type == "object" then .id else . end' <<< "$resp")
    [ -n "$PROCESSO_ID" ] && [ "$PROCESSO_ID" != "null" ] || fail "Criar ProcessoSeletivo falhou: $resp"
    ok "processo=$PROCESSO_ID"
}

definir_algoritmo_contagem_prazo() {
    log "Definir Algoritmo de Contagem de Prazo (CONTAGEM-PRAZO-EXCLUI-DIA-INICIAL)"
    api PUT "/api/selecao/processos-seletivos/$PROCESSO_ID/algoritmo-contagem-prazo" '{
        "codigo": "CONTAGEM-PRAZO-EXCLUI-DIA-INICIAL", "versao": "v1"
    }' >/dev/null
    ok "algoritmo de contagem de prazo definido"
}

definir_oferta_atendimento() {
    log "Definir Oferta de Atendimento"
    # Sem prova não há atendimento especializado a ofertar (UNI-REQ-0012), mas a
    # publicação cobra a oferta declarada: o processo sem prova a declara vazia.
    local condicoes="$CONDICAO_ATENDIMENTO_IDS" recursos="$RECURSO_ACESSIBILIDADE_IDS" tipos="$TIPO_DEFICIENCIA_IDS"
    if [ "$MODELO_INSCRICAO_ID" != "$MODELO_COM_PROVA_ID" ]; then
        condicoes="[]" recursos="[]" tipos="[]"
    fi
    api PUT "/api/selecao/processos-seletivos/$PROCESSO_ID/oferta-atendimento" "{
        \"condicaoIds\": $condicoes, \"recursoIds\": $recursos, \"tipoDeficienciaIds\": $tipos
    }" >/dev/null
    ok "oferta de atendimento definida ($(jq length <<< "$condicoes") condições, $(jq length <<< "$recursos") recursos, $(jq length <<< "$tipos") tipos)"
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
    api PUT "/api/selecao/processos-seletivos/$PROCESSO_ID/distribuicao-vagas" "$(jq -nc \
        --argjson ofertas "$(printf '%s\n' "${ofertas[@]}" | jq -R . | jq -sc .)" \
        --arg reserva "$RESERVA_DEMOGRAFICA_ID" --argjson modalidades "$modalidades" '
        [$ofertas[] | {
            ofertaCursoId: ., voBase: 30, pr: 0.5,
            regraDistribuicaoCodigo: "DISTRIB-VAGAS-LEI-12711-COM-AC-PCD", regraDistribuicaoVersao: "v1",
            regraAjusteCodigo: "RECONCILIACAO-VAGAS-ART11-PU", regraAjusteVersao: "v1",
            referenciaReservaDemograficaId: $reserva,
            modalidadeIds: $modalidades,
            quadro: [{modalidadeId: "70da1000-0000-7000-8000-000000000010", quantidade: 2}]
        }]')" >/dev/null
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
    log "Definir Cascata de Remanejamento (REMANEJ-CASCATA-LEI-12711)"
    # A matriz tem de bater exatamente com o esquema_args congelado da regra
    # REMANEJ-CASCATA-LEI-12711/v1 (Portaria MEC 704/2025, art. 2º e Anexo) —
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
        \"regraCodigo\": \"REMANEJ-CASCATA-LEI-12711\", \"regraVersao\": \"v1\",
        \"fallbackCodigo\": \"AC\", \"destinos\": $destinos
    }" >/dev/null
    ok "cascata de remanejamento definida (Quadro 3)"
}

definir_bonus_regional() {
    log "Definir o Fator de Atenuação das Desigualdades de Acesso (BONUS-MULTIPLICATIVO)"
    # O modelo da Medicina pede a quem pede o Fator o município do ensino
    # médio ou, a quem concluiu por certificação e escolhe esse caminho, o da
    # residência no período, com as opções da base legal do acréscimo. Recebe
    # o acréscimo quem o pede no formulário (item 1.6): a configuração cita o
    # pedido.
    api PUT "/api/selecao/processos-seletivos/$PROCESSO_ID/bonus-regional" "{
        \"aplica\": true, \"regraCodigo\": \"BONUS-MULTIPLICATIVO\", \"regraVersao\": \"v1\",
        \"fator\": 1.2, \"teto\": null, \"fatoElegibilidadeCodigo\": \"SOLICITA_FATOR_ATENUACAO\",
        \"baseLegalBonusRegionalId\": \"$BASE_LEGAL_BONUS_ID\"
    }" >/dev/null
    ok "Fator de Atenuação definido (acréscimo de 20%, itens 1.6 e 5.1)"
}

definir_criterios_desempate() {
    log "Definir Critérios de Desempate"
    # O item 6.3 desempata pela nota da Redação, de Linguagens, de Matemática,
    # de Ciências da Natureza e de Ciências Humanas, e por último pela maior
    # idade. O desempate por área só cita área do quadro de pesos da
    # Resolução 805/2024, que a minuta não traz: fica a maior idade.
    api PUT "/api/selecao/processos-seletivos/$PROCESSO_ID/criterios-desempate" '[
        {
            "ordem": 1, "regraCodigo": "DESEMPATE-MAIOR-IDADE", "regraVersao": "v1",
            "etapaRef": null, "idadeMinima": null, "fato": null, "operador": null, "valor": null,
            "areas": null
        }
    ]' >/dev/null
    ok "critério de desempate definido (maior idade)"
}

definir_classificacao() {
    log "Definir Classificação"
    # A minuta classifica pela média ponderada do ENEM, truncada na segunda
    # casa (item 5.6), com eliminação (item 8.1). Os pesos da Resolução
    # 805/2024 não constam dela, e sem eles não há cálculo local: a nota entra
    # importada, e truncamento e eliminação só se aplicam ao cálculo local.
    api PUT "/api/selecao/processos-seletivos/$PROCESSO_ID/classificacao" '{
        "regraCalculoCodigo": "CLASSIFICACAO-IMPORTADA", "regraCalculoVersao": "v1",
        "regraArredondamentoCodigo": null, "regraArredondamentoVersao": null,
        "casasArredondamento": null,
        "regraOrdemAlocacaoCodigo": "ALOCACAO-PRIMEIRA-OPCAO-PRIORITARIA", "regraOrdemAlocacaoVersao": "v1",
        "regrasEliminacao": [], "baseadoEmEnem": true
    }' >/dev/null
    ok "classificação definida"
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

definir_cronograma_fases() {
    log "Definir Cronograma de Fases (Anexo I deslocado, fuso America/Belem)"
    # Anexo I: inscrição de 10/12/2026 a 10/01/2027 e 1ª chamada em 25/01/2027.
    # As datas andam juntas, de modo que a inscrição tenha aberto ontem; o
    # resultado final, em branco na minuta, fica entre o fim da inscrição e a
    # 1ª chamada. America/Belém é UTC-3 fixo, sem horário de verão.
    local hoje deslocamento inicio_inscricao fim_inscricao inicio_resultado fim_resultado inicio_habilitacao fim_habilitacao
    hoje=$(TZ=America/Belem date +%Y-%m-%d)
    deslocamento=$(( ($(date -d "$hoje -1 day" +%s) - $(date -d 2026-12-10 +%s)) / 86400 ))
    na_data() { TZ=America/Belem date -d "$1 $deslocamento days" +%Y-%m-%dT"$2"-03:00; }
    inicio_inscricao=$(na_data 2026-12-10 00:00:00)
    fim_inscricao=$(na_data 2027-01-10 23:59:59)
    inicio_resultado=$(na_data 2027-01-11 00:00:00)
    fim_resultado=$(na_data 2027-01-24 23:59:59)
    inicio_habilitacao=$(na_data 2027-01-25 00:00:00)
    fim_habilitacao=$(na_data 2027-01-29 23:59:59)

    local fases
    fases="[
        {
            \"ordem\": 1, \"faseCanonicaId\": \"$FASE_INSCRICAO_ID\",
            \"inicio\": \"$inicio_inscricao\", \"fim\": \"$fim_inscricao\",
            \"produtos\": [], \"faseConcluinteCodigo\": null,
            \"emiteParecerIndividual\": false, \"bancasRequeridas\": [], \"regraRecurso\": null
        },
        {
            \"ordem\": 2, \"faseCanonicaId\": \"$FASE_RESULTADO_ID\",
            \"inicio\": \"$inicio_resultado\", \"fim\": \"$fim_resultado\",
            \"produtos\": [{\"atoCodigo\": \"RESULTADO_CLASSIFICACAO\", \"papel\": \"DEFINITIVO\"}],
            \"faseConcluinteCodigo\": null, \"emiteParecerIndividual\": false,
            \"bancasRequeridas\": [], \"regraRecurso\": null
        },
        {
            \"ordem\": 3, \"faseCanonicaId\": \"$FASE_HABILITACAO_ID\",
            \"inicio\": \"$inicio_habilitacao\", \"fim\": \"$fim_habilitacao\",
            \"produtos\": [], \"faseConcluinteCodigo\": null,
            \"emiteParecerIndividual\": false, \"bancasRequeridas\": [], \"regraRecurso\": null
        }
    ]"
    # A fase de classificação, quando o roteiro declara uma etapa nela, ocupa o
    # intervalo entre o fim da inscrição e o resultado.
    if [ -n "${FASE_CLASSIFICACAO_ID:-}" ]; then
        fases=$(jq -c --arg fase "$FASE_CLASSIFICACAO_ID" --arg inicio "$(na_data 2027-01-11 00:00:00)" --arg fim "$(na_data 2027-01-12 23:59:59)" '
            [.[0], {ordem: 2, faseCanonicaId: $fase, inicio: $inicio, fim: $fim, produtos: [], faseConcluinteCodigo: null,
                emiteParecerIndividual: false, bancasRequeridas: [], regraRecurso: null}]
            + (.[1:] | map(.ordem += 1))' <<< "$fases")
        fases=$(jq -c --arg inicio "$(na_data 2027-01-13 00:00:00)" '.[2].inicio = $inicio' <<< "$fases")
    fi
    # Toda fase precisa de unidade responsável para o processo publicar; no roteiro, é a
    # unidade do processo.
    fases=$(jq -c --arg unidade "$UNIDADE_ID" 'map(. + {unidadeResponsavelId: $unidade})' <<< "$fases")
    api PUT "/api/selecao/processos-seletivos/$PROCESSO_ID/cronograma-fases" "$fases" >/dev/null
    ok "cronograma definido — inscrição de $inicio_inscricao a $fim_inscricao"
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
# As etapas replicam exatamente as seções/blocos de ConteudoDaInscricao()
# (SementePsrMedicina2027) — reenviar a mesma estrutura, com o FaseId
# acrescentado, não recria nem descarta os itens já aplicados (o handler só
# substitui os itens quando o formulário nasce agora, não quando já existe).
vincular_fase_do_formulario() {
    log "Vincular o formulário de inscrição à fase do cronograma"
    local processo fase_inscricao_processo_id renderizavel titulo etapas

    processo=$(api_get "/api/selecao/processos-seletivos/$PROCESSO_ID")
    fase_inscricao_processo_id=$(jq -r '.cronogramaFases[] | select(.codigo=="INSCRICAO") | .id' <<< "$processo")
    [ -n "$fase_inscricao_processo_id" ] || fail "fase INSCRICAO não encontrada no cronograma do processo $PROCESSO_ID"

    # Lê a estrutura (título + etapas) que a aplicação do modelo já criou, em
    # vez de copiá-la à mão do seed — sobrevive a qualquer mudança no modelo
    # da Medicina. DADOS_BASICOS é excluída: o handler do PUT já a repõe por
    # conta própria (ConjuntoBasicoDaInscricao.MesclarEtapas) a partir da
    # referência do processo — reenviá-la é redundante, e arriscaria recusa
    # por SecaoReservadaAlterada se a renderização um dia divergir, por
    # pouco que seja, da referência que o handler usa para comparar.
    # A projeção renderizável
    # não traz a exibição condicional de cada etapa (SecaoRenderizavel não
    # tem esse campo) — hoje o modelo da Medicina não usa exibição condicional
    # em nenhuma seção/bloco, então "exibicao: null" aqui reflete o estado
    # real; um modelo futuro com seção condicional exigiria uma fonte própria
    # para esse dado, não esta leitura.
    renderizavel=$(curl -sS -w '\n%{http_code}' -H "Authorization: Bearer $TOKEN" -H "Accept: application/vnd.uniplus.formulario.v2+json" \
        "$API_URL/api/selecao/admin/processos-seletivos/$PROCESSO_ID/formularios/INSCRICAO/renderizavel")
    [ "$(tail -n1 <<< "$renderizavel")" = "200" ] || fail "GET formulário renderizável falhou: $(sed '$d' <<< "$renderizavel")"
    renderizavel=$(sed '$d' <<< "$renderizavel")
    titulo=$(jq -r '.titulo' <<< "$renderizavel")
    [ -n "$titulo" ] && [ "$titulo" != "null" ] || fail "formulário INSCRICAO sem título — a aplicação do modelo falhou em silêncio?"
    etapas=$(jq -c '[.etapas[] | select(.codigo != "DADOS_BASICOS") | {codigo, ordem, tipo, bloco, titulo, descricao, aviso, exibicao: null}]' <<< "$renderizavel")
    [ "$(jq 'length' <<< "$etapas")" -gt 0 ] || fail "formulário INSCRICAO sem etapas — a aplicação do modelo falhou em silêncio?"

    api PUT "/api/selecao/admin/processos-seletivos/$PROCESSO_ID/formularios/INSCRICAO" "$(jq -nc \
        --arg fase "$fase_inscricao_processo_id" --arg titulo "$titulo" --argjson etapas "$etapas" \
        '{faseId: $fase, titulo: $titulo, etapas: $etapas}')" >/dev/null
    ok "formulário vinculado à fase $fase_inscricao_processo_id"
}

# Cadastra, na fase de habilitação, o documento que comprova cada caminho do
# Fator (itens 1.6 e 13): o histórico escolar para quem pede pelo ensino médio
# cursado na área — quem concluiu no regular ou na EJA, ou quem concluiu por
# certificação e escolheu esse caminho — e a conta de energia, água ou
# telefone do período para quem escolheu a residência. A comprovação negada
# remove o acréscimo, não elimina. Vem depois do modelo: os gatilhos citam
# fatos que o formulário coleta.
definir_documentos_exigidos() {
    log "Definir os documentos que comprovam o Fator na habilitação, mantendo os do modelo"
    local fase_habilitacao_processo_id base_legal processo documentos_do_modelo
    processo=$(api_get "/api/selecao/processos-seletivos/$PROCESSO_ID")
    fase_habilitacao_processo_id=$(jq -r '.cronogramaFases[] | select(.codigo=="HABILITACAO") | .id' <<< "$processo")
    [ -n "$fase_habilitacao_processo_id" ] || fail "fase HABILITACAO não encontrada no cronograma do processo $PROCESSO_ID"
    base_legal='[{"referencia": "Edital nº XX/2026-CEPS/Unifesspa, itens 1.6 e 13", "abrangencia": "INTERNA_EDITAL", "status": "RESOLVIDO", "observacao": null}]'

    # O PUT troca a árvore inteira: os documentos que o modelo de inscrição trouxer voltam na
    # entrada, com a base legal resolvida — o modelo a deixa pendente até o CEPS indicar a norma, e
    # a publicação a exige resolvida. O modelo sem prova não traz nenhum.
    documentos_do_modelo=$(jq -c --argjson bases "$base_legal" '[(.raizesExigencia // [])[] | {
        tipo: "FOLHA",
        documento: (.documento | {
            exigidoNaFaseId, tipoDocumentoId: .tipoDocumentoOrigemId, aplicabilidade, obrigatorio, consequenciaIndeferimento,
            condicoes: [.condicoes[] | {clausula, fato, operador, valor}], basesLegais: $bases,
            formatosPermitidos, finalidade
        })
    }]' <<< "$processo")
    # O modelo com prova sempre traz os documentos do atendimento especializado; sem eles, a
    # aplicação regrediu e o processo sairia publicado sem exigir o laudo.
    if [ "$MODELO_INSCRICAO_ID" = "$MODELO_COM_PROVA_ID" ] && [ "$(jq length <<< "$documentos_do_modelo")" -eq 0 ]; then
        fail "o modelo com prova não trouxe os documentos do atendimento especializado"
    fi

    api PUT "/api/selecao/processos-seletivos/$PROCESSO_ID/documentos-exigidos" "$(jq -nc \
        --argjson doModelo "$documentos_do_modelo" --arg fase "$fase_habilitacao_processo_id" --arg historico "$TIPO_DOCUMENTO_HISTORICO_ID" \
        --arg residencia "$TIPO_DOCUMENTO_RESIDENCIA_ID" --argjson basesLegais "$base_legal" '
        def folha($tipo; $condicoes): {
            tipo: "FOLHA",
            documento: {
                exigidoNaFaseId: $fase, finalidade: "HABILITACAO", tipoDocumentoId: $tipo, aplicabilidade: "CONDICIONAL",
                obrigatorio: true, consequenciaIndeferimento: "REMOVE_VANTAGEM",
                condicoes: $condicoes, basesLegais: $basesLegais, formatosPermitidos: "QUALQUER"
            }
        };
        def pedido($clausula): {clausula: $clausula, fato: "SOLICITA_FATOR_ATENUACAO", operador: "IGUAL", valor: "true"};
        $doModelo + [
            folha($historico; [
                pedido(0), {clausula: 0, fato: "FORMA_CONCLUSAO_EM", operador: "EM", valor: "[\"REGULAR\",\"EJA\"]"},
                pedido(1), {clausula: 1, fato: "CAMINHO_FATOR_ATENUACAO", operador: "IGUAL", valor: "\"ENSINO_MEDIO\""}
            ]),
            folha($residencia; [
                pedido(0), {clausula: 0, fato: "CAMINHO_FATOR_ATENUACAO", operador: "IGUAL", valor: "\"RESIDENCIA\""}
            ])
        ]')" >/dev/null
    ok "documentos do modelo mantidos e documentos do Fator definidos na fase $fase_habilitacao_processo_id"
}

documento_do_edital() {
    log "Documento do Edital — iniciar upload"
    local resp url_upload content_type
    resp=$(api POST "/api/selecao/processos-seletivos/$PROCESSO_ID/documentos-a-publicar" '{"finalidade":"documentoDoEdital","nomeArquivo":"edital.pdf","tipoDeMidia":"application/pdf"}')
    DOCUMENTO_EDITAL_ID=$(jq -r '.documentoAPublicarId' <<< "$resp")
    url_upload=$(jq -r '.urlUpload' <<< "$resp")
    content_type=$(jq -r '.contentTypeExigido' <<< "$resp")
    [ -n "$DOCUMENTO_EDITAL_ID" ] && [ "$DOCUMENTO_EDITAL_ID" != "null" ] || fail "Iniciar upload do edital falhou: $resp"

    log "Documento do Edital — PUT direto no MinIO"
    local upload_http_code
    [ -f "$EDITAL_PDF" ] || fail "PDF do edital não encontrado: $EDITAL_PDF"
    upload_http_code=$(curl -sS -o /dev/null -w '%{http_code}' -X PUT "$url_upload" -H "Content-Type: $content_type" --data-binary "@$EDITAL_PDF")
    [ "$upload_http_code" = "200" ] || fail "PUT no MinIO falhou (http=$upload_http_code) — url presumivelmente expirada ou host interno (minio:9000) não resolvido pelo cliente"

    log "Documento do Edital — confirmar upload"
    api POST "/api/selecao/processos-seletivos/$PROCESSO_ID/documentos-a-publicar/$DOCUMENTO_EDITAL_ID/confirmacao" "" >/dev/null
    ok "documento do edital confirmado ($DOCUMENTO_EDITAL_ID)"
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
            \"dataPublicacao\": \"$data_publicacao\",
            \"assinante\": \"Reitor da Unifesspa\", \"tipoAtoCodigo\": \"EDITAL_ABERTURA\"
        }
    }" >/dev/null
    ok "processo publicado"
}

divulgar() {
    log "Divulgação pública do processo"
    api PUT "/api/selecao/processos-seletivos/$PROCESSO_ID/divulgacao" \
        '{"camposPublicos": ["numero_inscricao", "nome"],
          "justificativa": "Item 1.9 do edital: a inscrição autoriza divulgar nome, número de inscrição, pontuação, colocação e resultado."}' >/dev/null
    ok "processo divulgado"
}

conferir_resultado() {
    log "Conferindo o resultado"
    local snapshot formulario tentativa
    snapshot=$(api_get "/api/selecao/processos-seletivos/$PROCESSO_ID/snapshot-vigente")
    echo "$snapshot" | jq -r '{numero, divulgacao: .configuracao.divulgacao} // .' >&2

    # A projeção da vitrine é assíncrona — alguns segundos depois do 204.
    for tentativa in 1 2 3 4 5; do
        formulario=$(curl -sS "$API_URL/api/selecao/processos-seletivos/$PROCESSO_ID/formularios/INSCRICAO")
        if jq -e '.etapas' <<< "$formulario" >/dev/null 2>&1; then
            ok "formulário público de inscrição responde (tentativa $tentativa)"
            break
        fi
        sleep 5
    done
    jq -e '.etapas' <<< "$formulario" >/dev/null 2>&1 || fail "formulário público não respondeu como esperado: $(head -c 300 <<< "$formulario")"

    # GET certames/{id} aceita o Guid direto — evita depender da página em que
    # o processo cai em GET /certames (paginado por cursor).
    local certame
    for tentativa in 1 2 3 4 5; do
        certame=$(curl -sS -w '\n%{http_code}' "$API_URL/api/selecao/certames/$PROCESSO_ID")
        [ "$(tail -n1 <<< "$certame")" = "200" ] && break
        sleep 5
    done
    [ "$(tail -n1 <<< "$certame")" = "200" ] \
        && ok "processo aparece na vitrine (GET /api/selecao/certames/{id})" \
        || fail "processo ainda não aparece na vitrine (tentativa $tentativa)"

    ok "PROCESSO_ID=$PROCESSO_ID — roteiro concluído"
}

main() {
    for cmd in jq curl date; do command -v "$cmd" >/dev/null 2>&1 || fail "comando ausente: $cmd"; done
    # GNU date (-d, --version) e /proc (idem()) — como o resto do repositório,
    # roda em Linux; não há suporte a macOS/BSD.
    date --version >/dev/null 2>&1 || fail "precisa do GNU date (-d), não do date do BSD/macOS"
    [ -r /proc/sys/kernel/random/uuid ] || fail "precisa de /proc/sys/kernel/random/uuid (Linux)"
    obter_token
    setup_publicacoes
    setup_organizacao
    setup_configuracao
    criar_processo
    definir_algoritmo_contagem_prazo
    definir_oferta_atendimento
    definir_distribuicao_vagas
    definir_opcoes_de_curso
    definir_cascata_remanejamento
    definir_bonus_regional
    definir_criterios_desempate
    definir_classificacao
    definir_edicoes_enem
    definir_taxa_inscricao
    definir_cronograma_fases
    aplicar_modelo_inscricao
    vincular_fase_do_formulario
    definir_documentos_exigidos
    documento_do_edital
    divulgar
    publicar
    conferir_resultado
}

# Executado direto, roda o roteiro; lido com `source`, só expõe as funções.
if [[ "${BASH_SOURCE[0]}" == "$0" ]]; then
    main "$@"
fi
