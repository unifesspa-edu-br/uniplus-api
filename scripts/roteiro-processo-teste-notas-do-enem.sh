#!/usr/bin/env bash
# roteiro-processo-teste-notas-do-enem.sh — cria o processo local de validação da
# tela de envio ao INEP e importação das notas do ENEM: publicado e divulgado,
# com a etapa de nota do ENEM, o quadro de pesos por área congelado, as edições
# 2024 a 2026, duas ofertas em grupos de área diferentes e inscrições enviadas
# das personas fictícias do portal, com 1ª e 2ª opção, distribuídas entre as
# edições. Ao final imprime o PROCESSO_ID (a variável E2E_PROCESSO_ID do web).
#
# É irmão de roteiro-processo-teste-inscricao.sh, que ele lê com `source`: o
# processo, o edital, o cronograma e o formulário são os mesmos; este roteiro
# troca a classificação (média ponderada pelos pesos por área, com a eliminação
# e o quadro da Resolução nº 805/2024/Consepe, como em HML), a distribuição
# (Medicina e Pedagogia, em grupos de área diferentes) e o número de opções
# (duas), e acrescenta a fase de classificação, a etapa de nota do ENEM e as
# inscrições. Tem identificador e nome próprios, então convive com o processo
# da Medicina na mesma base.
#
# Divulgação antes da publicação, e a publicação só termina quando o ato da
# abertura é registrado: a inscrição só abre depois disso.
#
# As personas são as de uniplus-developers/src/data/fake-people.json, criadas
# por setup-keycloak-dev.sh; o login delas é o do admin-cli, uma por vez. Das 57
# candidatas, 56 se inscrevem: uma fica sem inscrição (a linha "sem inscrito" do
# arquivo de retorno) e uma inscrita deve ficar fora do arquivo ("inscrito sem
# nota"). O roteiro indica as duas ao final.
#
# Pré-requisitos: os do roteiro original, as 60 personas no realm, o Geo local
# (a conferência do CEP) e o checkout do uniplus-developers ao lado deste.
#
# Uso:
#   ADMIN_USER=diogo.souza scripts/roteiro-processo-teste-notas-do-enem.sh
#
# Variáveis de ambiente opcionais: as do roteiro original, mais
#   RESOLUCAO_PESOS (default: Resolução nº 805/2024/Consepe)
#   SENHA_PERSONAS  (default: $ADMIN_PASS)
#   FAKE_PEOPLE     (default: ../uniplus-developers/src/data/fake-people.json)
#   GEO_URL         (default: http://localhost:5400)
#   PERSONA_NAO_INSCRITA, PERSONA_FORA_DO_ARQUIVO (usernames; ver abaixo)

set -euo pipefail

# O nome e o identificador são lidos pelo roteiro original ao ser carregado.
NOME_PROCESSO="${NOME_PROCESSO:-Processo Seletivo para o Curso de Medicina Unifesspa 2027 (teste das notas do ENEM)}"
IDENTIFICADOR_PROCESSO="${IDENTIFICADOR_PROCESSO:-psm-2027-notas-do-enem}"

# shellcheck source=roteiro-processo-teste-inscricao.sh
source "$(dirname "${BASH_SOURCE[0]}")/roteiro-processo-teste-inscricao.sh"

RESOLUCAO_PESOS="${RESOLUCAO_PESOS:-Resolução nº 805/2024/Consepe}"
SENHA_PERSONAS="${SENHA_PERSONAS:-$ADMIN_PASS}"
NUMERO_OPCOES_DE_CURSO=2

# Dados das personas (uniplus-developers/src/data/fake-people.json): o roteiro lê o
# arquivo do checkout vizinho, e a conferência do CEP é a do Geo local.
FAKE_PEOPLE="${FAKE_PEOPLE:-$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)/uniplus-developers/src/data/fake-people.json}"
GEO_URL="${GEO_URL:-http://localhost:5400}"
# Uma persona candidata fica sem inscrição (vira a linha "sem inscrito" do arquivo
# de retorno) e uma inscrita fica sem linha no arquivo (vira "inscrito sem nota").
PERSONA_NAO_INSCRITA="${PERSONA_NAO_INSCRITA:-leandro.viana}"
PERSONA_FORA_DO_ARQUIVO="${PERSONA_FORA_DO_ARQUIVO:-helena.rezende}"
declare -A OFERTA_DO_GRUPO=()

# Os dois cursos do processo, um por grupo de área do ENEM, e a oferta de cada um.
# A Medicina (cadastro do roteiro original) é do grupo Saúde e Biológicas; a
# Pedagogia entra como segunda oferta, em outro grupo.
setup_ofertas_por_grupo() {
    log "Setup — Medicina e Pedagogia em dois grupos de área do ENEM"
    local curso_id oferta_id
    api PUT "/api/configuracao/admin/cursos/$CURSO_ID" "{
        \"id\": \"$CURSO_ID\", \"codigo\": \"MEDICINA\", \"nome\": \"Medicina\", \"grau\": \"Bacharelado\",
        \"nivelEnsino\": \"Graduação\", \"grupoAreaEnem\": \"SAUDE_E_BIOLOGICAS\"
    }" >/dev/null
    OFERTA_DO_GRUPO[SAUDE_E_BIOLOGICAS]="$OFERTA_CURSO_ID"

    curso_id=$(post_tolerante "/api/configuracao/admin/cursos" '{
        "codigo": "PEDAGOGIA", "nome": "Pedagogia", "grau": "Licenciatura",
        "nivelEnsino": "Graduação", "grupoAreaEnem": "HUMANISTICA_I"
    }')
    [ -z "$curso_id" ] && curso_id=$(api_get "/api/configuracao/cursos?limit=100" | jq -r '.[] | select(.codigo=="PEDAGOGIA") | .id' | head -1)
    exigir_id "curso PEDAGOGIA" "$curso_id"
    oferta_id=$(api_get "/api/configuracao/ofertas-curso?limit=100" | jq -r --arg c "$curso_id" '.[] | select(.cursoId==$c) | .id' | head -1)
    if [ -z "$oferta_id" ]; then
        oferta_id=$(post_tolerante "/api/configuracao/admin/ofertas-curso" "{
            \"cursoId\": \"$curso_id\", \"localOfertaId\": \"$LOCAL_OFERTA_ID\",
            \"unidadeOfertanteOrigemId\": \"$UNIDADE_OFERTANTE_ID\", \"programaDeOferta\": \"REGULAR\",
            \"regimeDeFuncionamento\": \"EXTENSIVO\", \"formatoPedagogico\": \"PRESENCIAL\",
            \"regimeDeTurno\": \"REGULAR\", \"turnos\": [\"NOTURNO\"],
            \"eMecCodigo\": null, \"codigoSga\": null, \"vagasAnuaisAutorizadas\": null,
            \"baseLegal\": null, \"atoAutorizacaoMec\": null
        }")
    fi
    exigir_id "oferta de PEDAGOGIA" "$oferta_id"
    OFERTA_DO_GRUPO[HUMANISTICA_I]="$oferta_id"
    OFERTAS_DISTRIBUIDAS=("${OFERTA_DO_GRUPO[SAUDE_E_BIOLOGICAS]}" "${OFERTA_DO_GRUPO[HUMANISTICA_I]}")
    ok "ofertas: ${!OFERTA_DO_GRUPO[*]}"
}

# O quadro de pesos por área da Resolução nº 805/2024/Consepe (Anexo I), igual ao
# de HML: o corte de 400 na redação vale em todos os grupos.
cadastrar_pesos_por_area() {
    log "Setup — quadro de pesos por área do ENEM ($RESOLUCAO_PESOS)"
    local quadro grupo
    quadro='{
        "HUMANISTICA_I":      {"CIENCIAS_DA_NATUREZA": 2.0, "CIENCIAS_HUMANAS": 2.0, "LINGUAGENS": 2.0, "MATEMATICA": 2.0, "REDACAO": 2.0},
        "HUMANISTICA_II":     {"CIENCIAS_DA_NATUREZA": 1.5, "CIENCIAS_HUMANAS": 2.5, "LINGUAGENS": 2.5, "MATEMATICA": 1.5, "REDACAO": 2.0},
        "SAUDE_E_BIOLOGICAS": {"CIENCIAS_DA_NATUREZA": 2.0, "CIENCIAS_HUMANAS": 2.0, "LINGUAGENS": 2.0, "MATEMATICA": 2.0, "REDACAO": 2.0},
        "TECNOLOGICA":        {"CIENCIAS_DA_NATUREZA": 2.5, "CIENCIAS_HUMANAS": 1.5, "LINGUAGENS": 1.5, "MATEMATICA": 2.5, "REDACAO": 2.0}
    }'
    for grupo in $(jq -r 'keys[]' <<< "$quadro"); do
        post_tolerante "/api/configuracao/admin/pesos-area-enem" "$(jq -nc \
            --arg r "$RESOLUCAO_PESOS" --arg g "$grupo" --argjson q "$quadro" '{
            resolucao: $r, grupoCurso: $g, baseLegal: "Resolução nº 805/2024/Consepe - Anexo I",
            areas: ($q[$g] | to_entries | map({codigo: .key, peso: .value, corte: (if .key == "REDACAO" then 400 else null end)}))
        }')" >/dev/null
    done
    ok "pesos cadastrados para os quatro grupos"
}

# Sobrescreve a do roteiro original: a nota do ENEM é calculada aqui, pelos pesos
# por área congelados, e não importada. Eliminação como em HML: redação abaixo
# de 400 e zero em qualquer área.
definir_classificacao() {
    log "Definir Classificação (média ponderada pelos pesos por área, truncada em 2 casas)"
    api PUT "/api/selecao/processos-seletivos/$PROCESSO_ID/classificacao" "$(jq -nc --arg r "$RESOLUCAO_PESOS" '{
        regraCalculoCodigo: "FORMULA-MEDIA-PONDERADA", regraCalculoVersao: "v1",
        regraArredondamentoCodigo: "PRECISAO-TRUNCAR", regraArredondamentoVersao: "v1",
        casasArredondamento: 2,
        regraOrdemAlocacaoCodigo: "ALOCACAO-PRIMEIRA-OPCAO-PRIORITARIA", regraOrdemAlocacaoVersao: "v1",
        regrasEliminacao: [
            {regraCodigo: "ELIM-CORTE-EM-AREA", regraVersao: "v1", etapaRef: null, notaMinima: null, minimo: 400, areaCodigo: "REDACAO"},
            {regraCodigo: "ELIM-ZERO-EM-AREA", regraVersao: "v1", etapaRef: null, notaMinima: null, minimo: null, areaCodigo: null}
        ],
        baseadoEmEnem: true, resolucaoPesoAreaEnem: $r
    }')" >/dev/null
    ok "classificação definida, com o quadro de pesos congelado"
}

obter_fase_classificacao() {
    FASE_CLASSIFICACAO_ID=$(api_get "/api/configuracao/fases-canonicas?limit=100" | jq -r '.[] | select(.codigo=="CLASSIFICACAO") | .id')
    exigir_id "FASE_CLASSIFICACAO_ID" "$FASE_CLASSIFICACAO_ID"
}

definir_etapa_nota_do_enem() {
    log "Definir a etapa de nota do ENEM"
    local tipo_id
    tipo_id=$(api_get "/api/configuracao/tipos-etapa?limit=100" | jq -r '.[] | select(.notaDeOrigemNoEnem==true and .ativo==true) | .id' | head -1)
    exigir_id "tipo de etapa com nota de origem no ENEM" "$tipo_id"
    api PUT "/api/selecao/processos-seletivos/$PROCESSO_ID/etapas" "$(jq -nc --arg t "$tipo_id" '[{
        nome: "Nota do ENEM", carater: "ambas", tipoEtapaOrigemId: $t, peso: 1, notaMinima: null,
        ordem: 1, faseCodigo: "CLASSIFICACAO", produtos: [], inicio: null, fim: null,
        emiteParecerIndividual: false, bancas: [], recursos: []
    }]')" >/dev/null
    ETAPA_ID=$(api_get "/api/selecao/processos-seletivos/$PROCESSO_ID" | jq -r '.etapas[0].id')
    exigir_id "ETAPA_ID" "$ETAPA_ID"
    ok "etapa=$ETAPA_ID"
}

# Espera o Publicações registrar o ato de abertura: a inscrição só abre depois. O
# atoId do snapshot vigente existe desde a publicação, antes do registro; o que
# muda com o registro é o ato passar a ser encontrado no Publicações.
aguardar_ato_registrado() {
    log "Aguardando o registro do ato de abertura"
    local tentativa ato
    ato=$(api_get "/api/selecao/processos-seletivos/$PROCESSO_ID/snapshot-vigente" | jq -r '.atoId // empty')
    exigir_id "atoId do snapshot vigente" "$ato"
    for tentativa in $(seq 1 12); do
        if [ "$(curl -sS -o /dev/null -w '%{http_code}' "$API_URL/api/publicacoes/atos/$ato" \
            -H "Accept: application/vnd.uniplus.ato-normativo.v1+json")" = "200" ]; then
            ok "ato registrado ($ato)"
            return
        fi
        sleep 5
    done
    fail "o ato de abertura não foi registrado a tempo"
}

# Token da persona pelo admin-cli (ROPC). Um login por vez: logins paralelos da
# mesma persona disparam o bloqueio por força bruta do Keycloak.
token_da_persona() {
    curl -sS -X POST "$KC_URL/realms/$KC_REALM/protocol/openid-connect/token" \
        --data-urlencode "grant_type=password" --data-urlencode "client_id=admin-cli" \
        --data-urlencode "username=$1" --data-urlencode "password=$SENHA_PERSONAS" \
        | jq -r '.access_token // empty'
}

# As respostas do formulário de uma persona: dados do fake-people.json, o endereço
# resolvido pelo CEP no Geo, a edição do ENEM escolhida e nenhuma reserva de vagas.
respostas_da_persona() {
    local pessoa="$1" edicao="$2" token="$3" cep geo
    cep=$(jq -r '.cep | gsub("\\D"; "")' <<< "$pessoa")
    geo=$(curl -sS "$GEO_URL/api/cep/$cep" -H "Authorization: Bearer $token")
    jq -e '.codigoIbge' <<< "$geo" >/dev/null || fail "o Geo não resolveu o CEP $cep: $geo"
    jq -nc --argjson p "$pessoa" --argjson g "$geo" --arg edicao "$edicao" '
        ({"Feminino": "FEMININO", "Masculino": "MASCULINO"}[$p.sexo]) as $sexo | {
        NOME: $p.nome, DESEJA_NOME_SOCIAL: ($p.nome_social != null), NACIONALIDADE: "NATO",
        CPF: ($p.cpf | gsub("\\D"; "")), DATA_NASCIMENTO: ($p.data_nasc | split("/") | reverse | join("-")),
        RG_NUMERO: $p.rg, RG_ORGAO_EMISSOR: "SSP", RG_UF: $g.uf,
        RG_DATA_EMISSAO: ((($p.data_nasc | split("/")[2] | tonumber) + 18 | tostring) + "-01-01"),
        NATURALIDADE_UF: $g.uf, NATURALIDADE_MUNICIPIO: $g.codigoIbge,
        NOME_MAE: $p.mae, NOME_PAI: $p.pai, ESTADO_CIVIL: "SOLTEIRO", SEXO: $sexo, SEXO_CIVIL: $sexo,
        IDENTIDADE_GENERO: "PREFIRO_NAO_INFORMAR", ORIENTACAO_SEXUAL: "PREFIRO_NAO_INFORMAR",
        EMAIL: (($p.email | split("@")[0]) + "@uniplus-test.local"), TELEFONE: $p.celular, TIPO_ENDERECO: "URBANO",
        ENDERECO_RESIDENCIAL: {
            cep: $p.cep, logradouro: $p.endereco, numero: ($p.numero | tostring), complemento: null,
            bairro: $p.bairro, distrito: null, cidade: {codigoIbge: $g.codigoIbge, nome: $g.cidade, uf: $g.uf},
            latitude: null, longitude: null, nivelResolucao: null, origem: null
        },
        COR_RACA: "BRANCA", EDICAO_ENEM: $edicao, FORMA_CONCLUSAO_EM: "REGULAR", ONDE_CURSOU_EM: "PRIVADA",
        SOLICITA_FATOR_ATENUACAO: false, PCD: false, CONCORRER_PCD: false, CONCORRER_EP: false,
        INDIGENA: false, QUILOMBOLA: false, CONCORRER_PPI: false, CONCORRER_Q: false,
        BAIXA_RENDA: false, CONCORRER_RENDA: false
    } + (if $p.nome_social != null then {NOME_SOCIAL: $p.nome_social} else {} end)'
}

# Cria o rascunho, salva as respostas e envia a inscrição de uma persona.
inscrever_persona() {
    local pessoa="$1" edicao="$2" oferta1="$3" oferta2="$4" username token accept corpo rascunho resposta http_code
    username=$(jq -r '.username' <<< "$pessoa")
    token=$(token_da_persona "$username")
    [ -n "$token" ] || fail "não obtive token da persona $username"
    local url="$API_URL/api/selecao/processos-seletivos/$PROCESSO_ID/inscricao"
    accept="Accept: application/vnd.uniplus.inscricao.v1+json"

    rascunho=$(curl -sS -w '\n%{http_code}' "$url" -H "Authorization: Bearer $token" -H "$accept")
    if [ "$(tail -n1 <<< "$rascunho")" = "404" ]; then
        rascunho=$(curl -sS -w '\n%{http_code}' -X POST "$url" -H "Authorization: Bearer $token" -H "Idempotency-Key: $(idem)" -H "$accept")
    fi
    [ "$(tail -n1 <<< "$rascunho")" = "200" ] || fail "rascunho de $username: $(sed '$d' <<< "$rascunho")"

    corpo=$(jq -nc --arg o1 "$oferta1" --arg o2 "$oferta2" --argjson a "$ACEITES" \
        --argjson r "$(respostas_da_persona "$pessoa" "$edicao" "$token")" \
        '{ofertaCursoId: $o1, ofertaCursoSegundaOpcaoId: $o2, respostas: $r, grupos: {}, aceites: $a}')
    rascunho=$(curl -sS -X PUT "$url" -H "Authorization: Bearer $token" -H "Idempotency-Key: $(idem)" \
        -H "If-Match: $(jq -r '.eTag' <<< "$(sed '$d' <<< "$rascunho")")" -H "Content-Type: application/json" -H "$accept" -d "$corpo")
    jq -e '.eTag' <<< "$rascunho" >/dev/null || fail "salvar as respostas de $username: $(head -c 400 <<< "$rascunho")"

    resposta=$(curl -sS -w '\n%{http_code}' -X POST "$url/submissao" -H "Authorization: Bearer $token" -H "Idempotency-Key: $(idem)" \
        -H "If-Match: $(jq -r '.eTag' <<< "$rascunho")" -H "Content-Type: application/json" \
        -H "Accept: application/vnd.uniplus.comprovante-inscricao.v1+json" -d "$corpo")
    http_code=$(tail -n1 <<< "$resposta")
    [ "$http_code" = "200" ] || fail "enviar a inscrição de $username → $http_code: $(sed '$d' <<< "$resposta" | head -c 600)"
}

# As 56 personas candidatas (menos a não inscrita), distribuídas entre as edições
# (metade em 2025, o resto em 2024 e 2026) e entre as duas ofertas na 1ª opção,
# de modo que cada oferta recebe as três edições; a 2ª opção é sempre a outra
# oferta, de outro grupo de área.
inscrever_personas() {
    log "Inscrever as personas candidatas (sem $PERSONA_NAO_INSCRITA)"
    local edicoes=(2025 2024 2025 2026) ofertas=("${OFERTAS_DISTRIBUIDAS[@]}") formulario candidatas pessoa i=0
    formulario=$(curl -sS "$API_URL/api/selecao/processos-seletivos/$PROCESSO_ID/formularios/INSCRICAO")
    ACEITES=$(jq -c '[.termos[] | {codigo, versaoId, hashVersao}]' <<< "$formulario")
    # Lido antes do laço para que uma falha do jq derrube o roteiro em vez de
    # terminar com zero inscrições.
    candidatas=$(jq -c --arg fora "$PERSONA_NAO_INSCRITA" '.[] | select(.perfil == "candidato" and .username != $fora)' "$FAKE_PEOPLE")
    [ -n "$candidatas" ] || fail "nenhuma persona candidata em $FAKE_PEOPLE"
    while IFS= read -r pessoa; do
        inscrever_persona "$pessoa" "${edicoes[$((i % 4))]}" "${ofertas[$(((i / 4) % 2))]}" "${ofertas[$((((i / 4) + 1) % 2))]}"
        i=$((i + 1))
    done <<< "$candidatas"
    ok "$i inscrições enviadas"
    ok "sem inscrição: $PERSONA_NAO_INSCRITA — inscrita e fora do arquivo de retorno: $PERSONA_FORA_DO_ARQUIVO"
}

# Confere as personas antes de criar qualquer coisa, para não deixar um processo
# publicado sem inscrições na base.
conferir_personas() {
    [ -r "$FAKE_PEOPLE" ] || fail "fake-people.json não encontrado em $FAKE_PEOPLE"
    [ "$PERSONA_NAO_INSCRITA" != "$PERSONA_FORA_DO_ARQUIVO" ] \
        || fail "PERSONA_NAO_INSCRITA e PERSONA_FORA_DO_ARQUIVO precisam ser pessoas diferentes"
    local username
    for username in "$PERSONA_NAO_INSCRITA" "$PERSONA_FORA_DO_ARQUIVO"; do
        jq -e --arg u "$username" 'any(.[]; .username == $u and .perfil == "candidato")' "$FAKE_PEOPLE" >/dev/null \
            || fail "$username não é persona candidata em $FAKE_PEOPLE"
    done
}

main() {
    for cmd in jq curl date; do command -v "$cmd" >/dev/null 2>&1 || fail "comando ausente: $cmd"; done
    date --version >/dev/null 2>&1 || fail "precisa do GNU date (-d), não do date do BSD/macOS"
    [ -r /proc/sys/kernel/random/uuid ] || fail "precisa de /proc/sys/kernel/random/uuid (Linux)"
    conferir_personas
    obter_token
    setup_publicacoes
    setup_organizacao
    setup_configuracao
    obter_fase_classificacao
    setup_ofertas_por_grupo
    cadastrar_pesos_por_area
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
    definir_etapa_nota_do_enem
    aplicar_modelo_inscricao
    vincular_fase_do_formulario
    definir_documentos_exigidos
    documento_do_edital
    divulgar
    publicar
    aguardar_ato_registrado
    conferir_resultado
    inscrever_personas
    echo "PROCESSO_ID=$PROCESSO_ID"
}

if [[ "${BASH_SOURCE[0]}" == "$0" ]]; then
    main "$@"
fi
