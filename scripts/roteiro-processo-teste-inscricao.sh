#!/usr/bin/env bash
# roteiro-processo-teste-inscricao.sh — cria, do zero, um ProcessoSeletivo de
# teste com o modelo de inscrição da Medicina aplicado, publicado e divulgado,
# com período de inscrição aberto na data de hoje (api#1861).
#
# Sequência (tolera 409 nos catálogos verdadeiramente compartilhados — tipos de
# ato, calendário vigente, fases canônicas; nesses, reaproveita o que já existe.
# Unidade, campus, local de oferta, curso e oferta de curso são CRIADOS DE NOVO
# a cada execução, com sufixo de dia-do-ano+horário — ficam acumulando no
# catálogo administrativo, aceitável para uma ferramenta de desenvolvimento
# descartável. O ProcessoSeletivo também é sempre NOVO a cada execução):
#   Setup (Publicações/Organização/Configuração) →
#   Criar ProcessoSeletivo → Etapas → Oferta de Atendimento → Distribuição de
#   Vagas (DISTRIB-VAGAS-LEI-12711, rol fechado) → Cascata de Remanejamento
#   (matriz da Portaria MEC 704/2025) → Bônus Regional → Critérios de
#   Desempate → Classificação → Taxa de Inscrição → Cronograma de Fases
#   (INSCRICAO aberta hoje + AVALIACAO depois) → aplicar o modelo de
#   inscrição da Medicina → vincular a fase do formulário → Documento do
#   Edital → Divulgação (sempre ANTES de publicar: depois da publicação o
#   processo só aceita mutação por retificação) → Publicar.
#
# Nada aqui referencia código de fato nem regra específica da Medicina — o
# modelo aplicado é dado de teste (SementePsrMedicina2027), não um caso
# especial desta rotina.
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
#   MODELO_MEDICINA_ID (default: 5e3d0006-0000-7000-8000-000000000001 —
#                 PSR_MEDICINA_2027_INSCRICAO, SementePsrMedicina2027)

set -euo pipefail

KC_URL="${KC_URL:-http://localhost:8080}"
KC_REALM="${KC_REALM:-unifesspa}"
API_URL="${API_URL:-http://localhost:5200}"
ADMIN_USER="${ADMIN_USER:-admin}"
# Mesmo default de setup-keycloak-dev.sh — se o setup rodou com TEST_PASSWORD
# diferente, o admin está com esse valor, não com Uni+Teste26.
ADMIN_PASS="${ADMIN_PASS:-${TEST_PASSWORD:-Uni+Teste26}}"
MODELO_MEDICINA_ID="${MODELO_MEDICINA_ID:-5e3d0006-0000-7000-8000-000000000001}"

# Dia do ano (001-366) + hora — mais curto que a data completa, porque
# "SEDE-RTI-$RUN_SUFFIX" tem de caber no limite de 20 caracteres da sigla do
# Campus (reduz, mas não zera, a chance de colisão entre execuções).
RUN_SUFFIX="$(date +%j%H%M%S)"
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
        "efeitoIrreversivel": false, "vigenciaInicio": "2020-01-01",
        "vigenciaFim": null, "baseLegal": null
    }' >/dev/null
    post_tolerante "/api/publicacoes/admin/tipos-ato" '{
        "codigo": "RESULTADO_FINAL", "nome": "Resultado final",
        "congelaConfiguracao": false, "unicoPorObjeto": false,
        "efeitoIrreversivel": false, "vigenciaInicio": "2020-01-01",
        "vigenciaFim": null, "baseLegal": null, "ehResultado": true
    }' >/dev/null

    # Catálogo pode já existir de uma rodada anterior sem `ehResultado=true` —
    # o cronograma de fases recusa um produto com papel PRELIMINAR/DEFINITIVO
    # referenciando um tipo de ato que não é resultado
    # (uniplus.selecao.produto_da_fase.papel_em_ato_que_nao_eh_resultado) —
    # upsert explícito via PUT.
    local atual eh_resultado id
    atual=$(api_get "/api/publicacoes/tipos-ato/RESULTADO_FINAL/vigente")
    eh_resultado=$(jq -r '.ehResultado' <<< "$atual")
    if [ "$eh_resultado" != "true" ]; then
        id=$(jq -r '.id' <<< "$atual")
        api PUT "/api/publicacoes/admin/tipos-ato/$id" "{
            \"id\": \"$id\", \"codigo\": \"RESULTADO_FINAL\", \"nome\": \"Resultado final\",
            \"congelaConfiguracao\": false, \"unicoPorObjeto\": false,
            \"efeitoIrreversivel\": false, \"ehResultado\": true,
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

    local unidade_codigo="CEPS-RTI-$RUN_SUFFIX"
    UNIDADE_ID=$(post_tolerante "/api/organizacao/admin/unidades" "{
        \"nome\": \"Comissão Especial de Processos Seletivos\", \"alias\": \"CEPS\",
        \"slug\": \"ceps-$RUN_SUFFIX\", \"sigla\": \"CEPS-$RUN_SUFFIX\",
        \"codigo\": \"$unidade_codigo\", \"unidadeSuperiorId\": null,
        \"tipo\": \"coordenacao\", \"unidadeAcademica\": false,
        \"vigenciaInicio\": \"2020-01-01\", \"vigenciaFim\": null,
        \"cidadeCodigoIbge\": \"1504208\", \"cidadeNome\": \"Marabá\", \"cidadeUf\": \"PA\",
        \"origem\": \"criadoNoUniPlus\"
    }")
    [ -z "$UNIDADE_ID" ] && UNIDADE_ID=$(api_get "/api/organizacao/unidades?limit=100" | jq -r --arg c "$unidade_codigo" '.[] | select(.codigo==$c) | .id' | head -1)
    exigir_id "UNIDADE_ID" "$UNIDADE_ID"
    ok "unidade=$UNIDADE_ID"
}

setup_configuracao() {
    log "Setup — Configuração (campus, local de oferta, curso, oferta de curso)"
    local campus_sigla="SEDE-RTI-$RUN_SUFFIX" curso_codigo="MED-RTI-$RUN_SUFFIX"
    CAMPUS_ID=$(post_tolerante "/api/configuracao/admin/campi" "{
        \"sigla\": \"$campus_sigla\", \"nome\": \"Campus Sede (roteiro de teste)\",
        \"cidadeCodigoIbge\": \"1504208\", \"cidadeNome\": \"Marabá\", \"cidadeUf\": \"PA\",
        \"endereco\": null, \"codigoEmec\": null
    }")
    [ -z "$CAMPUS_ID" ] && CAMPUS_ID=$(api_get "/api/configuracao/campi?limit=100" | jq -r --arg s "$campus_sigla" '.[] | select(.sigla==$s) | .id' | head -1)
    exigir_id "CAMPUS_ID" "$CAMPUS_ID"
    LOCAL_OFERTA_ID=$(post_tolerante "/api/configuracao/admin/locais-oferta" "{
        \"tipo\": \"campusSede\", \"campusResponsavelId\": \"$CAMPUS_ID\",
        \"cidadeCodigoIbge\": \"1504208\", \"cidadeNome\": \"Marabá\", \"cidadeUf\": \"PA\",
        \"endereco\": null, \"codigoEmec\": null
    }")
    # Sem campo próprio de unicidade (não é por codigo/sigla) — sem fallback
    # por busca; se um 409 um dia acontecer aqui, falhar alto é melhor que um
    # id vazio descendo em silêncio para o PUT de distribuição de vagas.
    [ -n "$LOCAL_OFERTA_ID" ] || fail "criar local de oferta falhou sem id (409 sem entrada equivalente a reaproveitar)"
    CURSO_ID=$(post_tolerante "/api/configuracao/admin/cursos" "{
        \"codigo\": \"$curso_codigo\", \"nome\": \"Medicina (roteiro de teste)\",
        \"grau\": \"Bacharelado\", \"nivelEnsino\": \"Graduação\", \"grupoAreaEnem\": null
    }")
    [ -z "$CURSO_ID" ] && CURSO_ID=$(api_get "/api/configuracao/cursos?limit=100" | jq -r --arg c "$curso_codigo" '.[] | select(.codigo==$c) | .id' | head -1)
    exigir_id "CURSO_ID" "$CURSO_ID"
    OFERTA_CURSO_ID=$(post_tolerante "/api/configuracao/admin/ofertas-curso" "{
        \"cursoId\": \"$CURSO_ID\", \"localOfertaId\": \"$LOCAL_OFERTA_ID\",
        \"unidadeOfertanteOrigemId\": \"$UNIDADE_ID\", \"programaDeOferta\": \"REGULAR\",
        \"regimeDeFuncionamento\": \"EXTENSIVO\", \"formatoPedagogico\": \"PRESENCIAL\",
        \"regimeDeTurno\": \"REGULAR\", \"turnos\": [\"MATUTINO\"],
        \"eMecCodigo\": null, \"codigoSga\": null, \"vagasAnuaisAutorizadas\": null,
        \"baseLegal\": null, \"atoAutorizacaoMec\": null
    }")
    [ -n "$OFERTA_CURSO_ID" ] || fail "criar oferta de curso falhou sem id (409 sem entrada equivalente a reaproveitar)"
    ok "campus=$CAMPUS_ID local_oferta=$LOCAL_OFERTA_ID curso=$CURSO_ID oferta_curso=$OFERTA_CURSO_ID"

    log "Setup — fases canônicas e referência de reserva demográfica"
    FASE_INSCRICAO_ID=$(post_tolerante "/api/configuracao/admin/fases-canonicas" '{
        "codigo": "INSCRICAO", "nome": "Inscrição", "descricao": null,
        "donoTipico": "CEPS", "agrupaEtapas": false, "permiteComplementacao": false,
        "baseLegal": null, "produzResultado": false, "resultadoDefinitivo": false,
        "coletaInscricao": true, "origemData": "PROPRIA"
    }')
    if [ -z "$FASE_INSCRICAO_ID" ]; then
        local fase_inscricao_existente
        fase_inscricao_existente=$(api_get "/api/configuracao/fases-canonicas?limit=100" | jq -c '.[] | select(.codigo=="INSCRICAO")')
        [ "$(jq -r '.coletaInscricao' <<< "$fase_inscricao_existente")" = "true" ] \
            || fail "fase canônica INSCRICAO já existe sem coletaInscricao=true — o roteiro não pode fixar o período de inscrição nela"
        FASE_INSCRICAO_ID=$(jq -r '.id' <<< "$fase_inscricao_existente")
    fi

    FASE_AVALIACAO_ID=$(post_tolerante "/api/configuracao/admin/fases-canonicas" '{
        "codigo": "AVALIACAO", "nome": "Avaliação", "descricao": null,
        "donoTipico": "CEPS", "agrupaEtapas": true, "permiteComplementacao": false,
        "baseLegal": null, "produzResultado": true, "resultadoDefinitivo": true,
        "coletaInscricao": false, "origemData": "PROPRIA"
    }')
    if [ -z "$FASE_AVALIACAO_ID" ]; then
        local fase_avaliacao_existente
        fase_avaliacao_existente=$(api_get "/api/configuracao/fases-canonicas?limit=100" | jq -c '.[] | select(.codigo=="AVALIACAO")')
        # GET fases-canonicas não devolve produzResultado (só o PUT/POST o
        # recebem) — a única propriedade conferível por aqui é agrupaEtapas.
        [ "$(jq -r '.agrupaEtapas' <<< "$fase_avaliacao_existente")" = "true" ] \
            || fail "fase canônica AVALIACAO já existe sem agrupaEtapas=true — o cronograma não pode agrupar a etapa pontuada nela"
        FASE_AVALIACAO_ID=$(jq -r '.id' <<< "$fase_avaliacao_existente")
    fi

    RESERVA_DEMOGRAFICA_ID=$(post_tolerante "/api/configuracao/admin/referencias-reserva-demografica" '{
        "censoReferencia": "IBGE 2022",
        "ppiPercentual": 50, "quilombolaPercentual": 2, "pcdPercentual": 1,
        "baseLegal": "IBGE Censo 2022"
    }')
    [ -z "$RESERVA_DEMOGRAFICA_ID" ] && RESERVA_DEMOGRAFICA_ID=$(api_get "/api/configuracao/referencias-reserva-demografica?limit=100" | jq -r '.[] | select(.censoReferencia=="IBGE 2022") | .id' | head -1)
    exigir_id "RESERVA_DEMOGRAFICA_ID" "$RESERVA_DEMOGRAFICA_ID"

    # PSR (Processo Seletivo Regular) é o tipo de processo do modelo de
    # inscrição semeado da Medicina (SementePsrMedicina2027) — aplicar o
    # modelo exige que o processo seja do mesmo tipo.
    TIPO_PROCESSO_ID=$(api_get "/api/configuracao/tipos-processo?limit=100" | jq -r '.[] | select(.codigo=="PSR") | .id')
    [ -n "$TIPO_PROCESSO_ID" ] || fail "tipo de processo 'PSR' não encontrado no catálogo semeado"
    TIPO_ETAPA_PROVA_ID=$(api_get "/api/configuracao/tipos-etapa?limit=100" | jq -r '.[] | select(.codigo=="PROVA_OBJETIVA") | .id')
    [ -n "$TIPO_ETAPA_PROVA_ID" ] || fail "tipo de etapa 'PROVA_OBJETIVA' não encontrado no catálogo semeado"

    # O modelo de inscrição da Medicina coleta CONDICAO_ATENDIMENTO (seleção múltipla,
    # opções do próprio processo) e TIPO_DEFICIENCIA (opções = o atendimento
    # especializado declarado na oferta) — a conformidade recusa publicar os dois
    # fatos coletáveis sem ao menos uma opção.
    # "PCD" é o código reconhecido por OfertaAtendimentoEspecializado.CodigoCondicaoPcd
    # (ADR-0067) — tipo de deficiência só é aceito sob essa condição, por esse código.
    CONDICAO_ATENDIMENTO_CODIGO="PCD"
    CONDICAO_ATENDIMENTO_ID=$(post_tolerante "/api/configuracao/admin/condicoes-atendimento" "{
        \"codigo\": \"$CONDICAO_ATENDIMENTO_CODIGO\", \"nome\": \"Pessoa com deficiência\", \"descricao\": null
    }")
    [ -z "$CONDICAO_ATENDIMENTO_ID" ] && CONDICAO_ATENDIMENTO_ID=$(api_get "/api/configuracao/condicoes-atendimento?limit=100" | jq -r ".[] | select(.codigo==\"$CONDICAO_ATENDIMENTO_CODIGO\") | .id")
    exigir_id "CONDICAO_ATENDIMENTO_ID" "$CONDICAO_ATENDIMENTO_ID"

    TIPO_DEFICIENCIA_ID=$(post_tolerante "/api/configuracao/admin/tipos-deficiencia" '{
        "codigo": "BAIXA_VISAO", "nome": "Baixa visão",
        "descricao": "Acuidade visual reduzida, conforme laudo.", "permanente": true
    }')
    [ -z "$TIPO_DEFICIENCIA_ID" ] && TIPO_DEFICIENCIA_ID=$(api_get "/api/configuracao/tipos-deficiencia?limit=100" | jq -r '.[] | select(.codigo=="BAIXA_VISAO") | .id')
    exigir_id "TIPO_DEFICIENCIA_ID" "$TIPO_DEFICIENCIA_ID"

    BASE_LEGAL_BONUS_ID=$(post_tolerante "/api/configuracao/admin/base-legal-bonus-regional" '{
        "tipoInstrumento": "PORTARIA", "identificacao": "Edital Medicina 2027 (roteiro de teste)",
        "descricao": "Bônus regional do roteiro de teste",
        "municipios": [{"codigoIbge": "1504208", "nome": "Marabá", "uf": "PA"}]
    }')
    [ -z "$BASE_LEGAL_BONUS_ID" ] && BASE_LEGAL_BONUS_ID=$(api_get "/api/configuracao/base-legal-bonus-regional?limit=100" | jq -r '.[] | select(.identificacao=="Edital Medicina 2027 (roteiro de teste)") | .id' | head -1)
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

    ok "fase_inscricao=$FASE_INSCRICAO_ID fase_avaliacao=$FASE_AVALIACAO_ID reserva_demografica=$RESERVA_DEMOGRAFICA_ID tipo_processo=$TIPO_PROCESSO_ID condicao_atendimento=$CONDICAO_ATENDIMENTO_ID tipo_deficiencia=$TIPO_DEFICIENCIA_ID base_legal_bonus=$BASE_LEGAL_BONUS_ID calendario_dias_uteis=$calendario_id"
}

criar_processo() {
    log "Criar ProcessoSeletivo"
    local resp
    resp=$(api POST "/api/selecao/processos-seletivos" "{
        \"nome\": \"Processo de teste da inscrição (roteiro $RUN_SUFFIX)\",
        \"tipoProcessoOrigemId\": \"$TIPO_PROCESSO_ID\",
        \"origemCandidatos\": \"inscricaoPropria\",
        \"unidadeAdministradoraOrigemId\": \"$UNIDADE_ID\",
        \"localidadeCodigoIbge\": \"1504208\", \"localidadeNome\": \"Marabá\", \"localidadeUf\": \"PA\",
        \"identificadorLegivel\": \"roteiro-inscricao-$RUN_SUFFIX\"
    }")
    PROCESSO_ID=$(jq -r 'if type == "object" then .id else . end' <<< "$resp")
    [ -n "$PROCESSO_ID" ] && [ "$PROCESSO_ID" != "null" ] || fail "Criar ProcessoSeletivo falhou: $resp"
    ok "processo=$PROCESSO_ID"
}

definir_etapas() {
    log "Definir Etapas (uma etapa pontuada, para a fase AVALIACAO agrupar)"
    api PUT "/api/selecao/processos-seletivos/$PROCESSO_ID/etapas" "[
        {
            \"nome\": \"Prova Objetiva\", \"tipoEtapaOrigemId\": \"$TIPO_ETAPA_PROVA_ID\",
            \"carater\": \"classificatoria\", \"peso\": 1.0, \"notaMinima\": null,
            \"ordem\": 1, \"produtos\": [], \"bancas\": [], \"recursos\": []
        }
    ]" >/dev/null
    ok "etapas definidas"
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
    api PUT "/api/selecao/processos-seletivos/$PROCESSO_ID/oferta-atendimento" "{
        \"condicaoIds\": [\"$CONDICAO_ATENDIMENTO_ID\"], \"recursoIds\": [],
        \"tipoDeficienciaIds\": [\"$TIPO_DEFICIENCIA_ID\"]
    }" >/dev/null
    ok "oferta de atendimento definida"
}

definir_distribuicao_vagas() {
    log "Definir Distribuição de Vagas (DISTRIB-VAGAS-LEI-12711)"
    local modalidades='[
        "70da1000-0000-7000-8000-000000000001",
        "70da1000-0000-7000-8000-000000000002",
        "70da1000-0000-7000-8000-000000000003",
        "70da1000-0000-7000-8000-000000000004",
        "70da1000-0000-7000-8000-000000000005",
        "70da1000-0000-7000-8000-000000000006",
        "70da1000-0000-7000-8000-000000000007",
        "70da1000-0000-7000-8000-000000000008",
        "70da1000-0000-7000-8000-000000000009"
    ]'
    api PUT "/api/selecao/processos-seletivos/$PROCESSO_ID/distribuicao-vagas" "[{
        \"ofertaCursoId\": \"$OFERTA_CURSO_ID\", \"voBase\": 40, \"pr\": 0.5,
        \"regraDistribuicaoCodigo\": \"DISTRIB-VAGAS-LEI-12711\", \"regraDistribuicaoVersao\": \"v1\",
        \"regraAjusteCodigo\": \"RECONCILIACAO-VAGAS-ART11-PU\", \"regraAjusteVersao\": \"v1\",
        \"referenciaReservaDemograficaId\": \"$RESERVA_DEMOGRAFICA_ID\",
        \"modalidadeIds\": $modalidades,
        \"quadro\": []
    }]" >/dev/null
    ok "distribuição de vagas definida"
}

definir_cascata_remanejamento() {
    log "Definir Cascata de Remanejamento (REMANEJ-CASCATA-LEI-12711)"
    # A matriz tem de bater exatamente com o esquema_args congelado da regra
    # REMANEJ-CASCATA-LEI-12711/v1 (Portaria MEC 704/2025, art. 2º e Anexo) —
    # ver RegraCatalogoSeed.cs, SeedId(19). Oito origens, sete destinos cada,
    # ordem fixa, terminal sempre AC (fallback, não listado nos destinos).
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
    ok "cascata de remanejamento definida (matriz da Portaria MEC 704/2025)"
}

definir_bonus_regional() {
    log "Definir Bônus Regional (BONUS-MULTIPLICATIVO)"
    # O modelo da Medicina coleta MUNICIPIO_EM_AREA_BONUS, cujas opções são os
    # municípios da área do bônus — a conformidade exige bônus regional
    # declarado para que esse fato tenha de onde tirar valores.
    api PUT "/api/selecao/processos-seletivos/$PROCESSO_ID/bonus-regional" "{
        \"aplica\": true, \"regraCodigo\": \"BONUS-MULTIPLICATIVO\", \"regraVersao\": \"v1\",
        \"fator\": 1.1, \"teto\": null, \"baseLegalBonusRegionalId\": \"$BASE_LEGAL_BONUS_ID\"
    }" >/dev/null
    ok "bônus regional definido (fator 1.1)"
}

definir_criterios_desempate() {
    log "Definir Critérios de Desempate"
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
    api PUT "/api/selecao/processos-seletivos/$PROCESSO_ID/classificacao" '{
        "regraCalculoCodigo": "CLASSIFICACAO-IMPORTADA", "regraCalculoVersao": "v1",
        "regraArredondamentoCodigo": null, "regraArredondamentoVersao": null,
        "casasArredondamento": null,
        "regraOrdemAlocacaoCodigo": "ALOCACAO-PRIMEIRA-OPCAO-PRIORITARIA", "regraOrdemAlocacaoVersao": "v1",
        "nOpcoesAlocacao": 1, "regrasEliminacao": [], "baseadoEmEnem": false
    }' >/dev/null
    ok "classificação definida"
}

definir_taxa_inscricao() {
    log "Definir Taxa de Inscrição"
    api PUT "/api/selecao/processos-seletivos/$PROCESSO_ID/taxa-inscricao" \
        '{"cobra": false, "valor": null, "fundamentos": null}' >/dev/null
    ok "sem taxa de inscrição"
}

definir_cronograma_fases() {
    log "Definir Cronograma de Fases (INSCRICAO aberta hoje, fuso America/Belem)"
    # America/Belém é UTC-3 fixo (sem horário de verão) — as datas são o
    # horário local de Belém, com o offset -03:00 explícito, não meia-noite UTC.
    local hoje inicio_inscricao fim_inscricao inicio_avaliacao fim_avaliacao
    hoje=$(TZ=America/Belem date +%Y-%m-%d)
    inicio_inscricao="$(TZ=America/Belem date -d "$hoje -7 days" +%Y-%m-%dT00:00:00-03:00)"
    fim_inscricao="$(TZ=America/Belem date -d "$hoje +60 days" +%Y-%m-%dT23:59:59-03:00)"
    inicio_avaliacao="$(TZ=America/Belem date -d "$hoje +90 days" +%Y-%m-%dT00:00:00-03:00)"
    fim_avaliacao="$(TZ=America/Belem date -d "$hoje +120 days" +%Y-%m-%dT00:00:00-03:00)"

    api PUT "/api/selecao/processos-seletivos/$PROCESSO_ID/cronograma-fases" "[
        {
            \"ordem\": 1, \"faseCanonicaId\": \"$FASE_INSCRICAO_ID\",
            \"inicio\": \"$inicio_inscricao\", \"fim\": \"$fim_inscricao\",
            \"produtos\": [], \"faseConcluinteCodigo\": null,
            \"emiteParecerIndividual\": false, \"bancasRequeridas\": [], \"regraRecurso\": null
        },
        {
            \"ordem\": 2, \"faseCanonicaId\": \"$FASE_AVALIACAO_ID\",
            \"inicio\": \"$inicio_avaliacao\", \"fim\": \"$fim_avaliacao\",
            \"produtos\": [{\"atoCodigo\": \"RESULTADO_FINAL\", \"papel\": \"DEFINITIVO\"}],
            \"faseConcluinteCodigo\": null, \"emiteParecerIndividual\": false,
            \"bancasRequeridas\": [], \"regraRecurso\": null
        }
    ]" >/dev/null
    ok "cronograma definido — inscrição de $inicio_inscricao a $fim_inscricao"
}

aplicar_modelo_medicina() {
    log "Aplicar o modelo de inscrição da Medicina ($MODELO_MEDICINA_ID)"
    api POST "/api/selecao/admin/processos-seletivos/$PROCESSO_ID/formularios/aplicacoes-de-modelo" \
        "{\"modeloId\": \"$MODELO_MEDICINA_ID\"}" >/dev/null
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


documento_do_edital() {
    log "Documento do Edital — iniciar upload"
    local resp url_upload content_type
    resp=$(api POST "/api/selecao/processos-seletivos/$PROCESSO_ID/documentos-edital" "")
    DOCUMENTO_EDITAL_ID=$(jq -r '.documentoEditalId' <<< "$resp")
    url_upload=$(jq -r '.urlUpload' <<< "$resp")
    content_type=$(jq -r '.contentTypeExigido' <<< "$resp")
    [ -n "$DOCUMENTO_EDITAL_ID" ] && [ "$DOCUMENTO_EDITAL_ID" != "null" ] || fail "Iniciar upload do edital falhou: $resp"

    log "Documento do Edital — PUT direto no MinIO"
    local upload_http_code
    upload_http_code=$(curl -sS -o /dev/null -w '%{http_code}' -X PUT "$url_upload" -H "Content-Type: $content_type" \
        --data-binary '%PDF-1.4 roteiro de teste da inscrição (api#1861).')
    [ "$upload_http_code" = "200" ] || fail "PUT no MinIO falhou (http=$upload_http_code) — url presumivelmente expirada ou host interno (minio:9000) não resolvido pelo cliente"

    log "Documento do Edital — confirmar upload"
    api POST "/api/selecao/processos-seletivos/$PROCESSO_ID/documentos-edital/$DOCUMENTO_EDITAL_ID/confirmacao" "" >/dev/null
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
        \"numero\": \"ROTEIRO-$RUN_SUFFIX/$ano\",
        \"documentoEditalId\": \"$DOCUMENTO_EDITAL_ID\",
        \"ato\": {
            \"orgao\": \"CEPS\", \"serie\": \"EDITAL\", \"ano\": $ano,
            \"dataPublicacao\": \"$data_publicacao\",
            \"assinante\": \"Diretor do CEPS\", \"tipoAtoCodigo\": \"EDITAL_ABERTURA\"
        }
    }" >/dev/null
    ok "processo publicado"
}

divulgar() {
    log "Divulgação pública do processo"
    api PUT "/api/selecao/processos-seletivos/$PROCESSO_ID/divulgacao" \
        '{"camposPublicos": ["numero_inscricao", "nome_abreviado"], "justificativa": null}' >/dev/null
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
    definir_etapas
    definir_oferta_atendimento
    definir_distribuicao_vagas
    definir_cascata_remanejamento
    definir_bonus_regional
    definir_criterios_desempate
    definir_classificacao
    definir_taxa_inscricao
    definir_cronograma_fases
    aplicar_modelo_medicina
    vincular_fase_do_formulario
    documento_do_edital
    divulgar
    publicar
    conferir_resultado
}

main "$@"
