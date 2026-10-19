#!/usr/bin/env bash
# consolida-tipos-ato-por-materia.sh — leva o catálogo de tipos de ato de um
# ambiente já semeado ao catálogo por matéria, pela API de Publicações
# (`/api/publicacoes/...`), sem tocar no banco.
#
# O que faz, a partir de um mapa "código antigo -> código novo" (arquivo TSV, por padrão
# `scripts/consolida-tipos-ato-por-materia.mapa-hml.tsv`):
#   1. cria o tipo novo, a partir da data de corte, quando ele ainda não tem versão vigente
#      nela: não único, resultado, sem congelar a configuração. Quando o código consta em
#      `seeds/seed-tipos-ato.json`, os atributos vêm de lá; senão, o nome vem da terceira
#      coluna do mapa. Se o código já tem versão agendada para depois do corte, a nova termina
#      onde ela começa. Toda versão do código novo que vale no corte ou depois precisa ser
#      resultado, não única, sem congelamento e sem efeito irreversível; se não for, o roteiro
#      para antes de escrever qualquer coisa;
#   2. só então retira cada tipo antigo do mapa, que repetia o papel no código: a versão que
#      atravessa a data de corte termina nela, e a versão que começa no corte ou depois, já
#      agendada, é removida. Se a criação falhar, nada é retirado: nunca fica um estado sem
#      tipo de resultado vigente. Ao encerrar a versão antiga que seja resultado e irreversível
#      (o antigo resultado final), envia-a sem a marca de irreversível, que o cadastro recusaria;
#      se houver versão assim e o corte for no futuro, para antes de escrever, porque os atos
#      publicados até o corte copiariam a marca já retirada;
#   3. define, nos tipos AVISO, COMUNICADO e ERRATA, os atributos `publicavelComoAvulso` e
#      `exigeAtoCorrigido` com o valor de `seeds/seed-tipos-ato.json`. As colunas nasceram `false`
#      em toda linha existente só para poder existir; sem este passo nenhum tipo é aceito como
#      documento avulso do processo e a errata não exige o ato corrigido. O passo atualiza a
#      versão vigente pelo PUT, só quando o valor difere, e pula o tipo sem versão vigente.
#
# O mapa de HML é uma PROPOSTA a validar com o CEPS antes de rodar, porque o catálogo de HML
# não é o seed.
#
# Não converte dado: o processo cujo cronograma cita um código antigo é
# recadastrado, e o ato já registrado guarda o tipo por valor e continua
# legível. O catálogo novo só vale para os cronogramas que o citam.
#
# Idempotente: o tipo antigo sem versão que atravesse o corte nem comece depois dele é
# pulado, e o novo que já tem versão vigente no corte também; os atributos do documento avulso
# só são escritos quando diferem do seed. Rodar de novo não escreve nada.
#
# Uso:
#   API_URL=https://uniplus-api-hml.example TOKEN=<access token plataforma-admin> \
#     bash scripts/consolida-tipos-ato-por-materia.sh
#
# Variáveis:
#   API_URL       base da API, sem barra no fim (obrigatória)
#   TOKEN         access token com o papel `plataforma-admin` (obrigatória; a
#                 leitura é pública, a escrita não)
#   DATA_CORTE    dia em que os tipos antigos deixam de valer, AAAA-MM-DD
#                 (padrão: hoje). É o primeiro dia sem a versão antiga, como em
#                 toda janela de vigência semiaberta do catálogo
#   MAPA          arquivo TSV do mapa (padrão: o mapa de HML ao lado do roteiro)
#   DRY_RUN=1     só informa o que faria, sem escrever
#
# Requer curl e jq. Nunca imprime o token.
set -euo pipefail

RAIZ="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
readonly RAIZ
readonly SEED="${RAIZ}/seeds/seed-tipos-ato.json"
readonly MAPA="${MAPA:-${RAIZ}/scripts/consolida-tipos-ato-por-materia.mapa-hml.tsv}"

: "${API_URL:?informe API_URL (base da API, sem barra no fim)}"
: "${TOKEN:?informe TOKEN (access token com o papel plataforma-admin)}"
readonly DATA_CORTE="${DATA_CORTE:-$(date +%F)}"
readonly DRY_RUN="${DRY_RUN:-0}"

log()  { printf '\033[1;36m==> %s\033[0m\n' "$*" >&2; }
ok()   { printf '\033[1;32m    OK %s\033[0m\n' "$*" >&2; }
fail() { printf '\033[1;31m✗ %s\033[0m\n' "$*" >&2; exit 1; }

[[ "${DATA_CORTE}" =~ ^[0-9]{4}-[0-9]{2}-[0-9]{2}$ ]] || fail "DATA_CORTE deve ser AAAA-MM-DD: ${DATA_CORTE}"
[[ -f "${MAPA}" ]] || fail "mapa não encontrado: ${MAPA}"
[[ -f "${SEED}" ]] || fail "seed não encontrado: ${SEED}"
command -v jq >/dev/null || fail "jq não está instalado"

# Chave de idempotência, sem depender de /proc (Linux).
nova_chave() {
    if command -v uuidgen >/dev/null; then
        uuidgen
    elif [[ -r /proc/sys/kernel/random/uuid ]]; then
        cat /proc/sys/kernel/random/uuid
    else
        python3 -c 'import uuid; print(uuid.uuid4())'
    fi
}

# Versão vigente do tipo hoje, ou na data informada (leitura pública), ou vazio quando não há.
vigente() {
    local codigo="$1" data="${2:-}" resposta http_code
    resposta="$(curl -sS -w '\n%{http_code}' "${API_URL}/api/publicacoes/tipos-ato/${codigo}/vigente${data:+?data=${data}}" \
        -H 'Accept: application/vnd.uniplus.tipo-ato.v1+json')"
    http_code="$(tail -n1 <<< "${resposta}")"
    case "${http_code}" in
        200) sed '$d' <<< "${resposta}" ;;
        404) echo "" ;;
        *) fail "GET tipos-ato/${codigo}/vigente → ${http_code}: $(sed '$d' <<< "${resposta}")" ;;
    esac
}

# Série completa de versões do catálogo (leitura pública, todas as páginas), lida uma vez.
# A página seguinte é pedida a API_URL com a query do rel="next": o host do Link é o que a API
# enxerga, que atrás do proxy pode não ser o endereço público.
catalogo_completo() {
    local query="vigentes=false&limit=100" cabecalhos resposta http_code todas="[]" proximo
    cabecalhos="$(mktemp)"
    while [[ -n "${query}" ]]; do
        resposta="$(curl -sS -D "${cabecalhos}" -w '\n%{http_code}' "${API_URL}/api/publicacoes/tipos-ato?${query}" \
            -H 'Accept: application/vnd.uniplus.tipo-ato.v1+json')"
        http_code="$(tail -n1 <<< "${resposta}")"
        [[ "${http_code}" == "200" ]] || fail "GET tipos-ato → ${http_code}: $(sed '$d' <<< "${resposta}")"
        todas="$(jq -c --argjson pagina "$(sed '$d' <<< "${resposta}")" '. + $pagina' <<< "${todas}")"
        proximo="$(tr -d '\r' < "${cabecalhos}" | grep -i '^link:' | tr ',' '\n' | grep 'rel="next"' | sed -n 's/.*<[^>?]*?\([^>]*\)>.*/\1/p' || true)"
        query="${proximo}"
    done
    rm -f "${cabecalhos}"
    echo "${todas}"
}

# Escrita autenticada que exige 2xx. $1=método $2=caminho $3=corpo
escrever() {
    local metodo="$1" caminho="$2" corpo="$3" resposta http_code
    resposta="$(curl -sS -w '\n%{http_code}' -X "${metodo}" "${API_URL}${caminho}" \
        -H "Authorization: Bearer ${TOKEN}" -H "Idempotency-Key: $(nova_chave)" \
        -H 'Content-Type: application/json' -d "${corpo}")"
    http_code="$(tail -n1 <<< "${resposta}")"
    case "${http_code}" in
        2??) ;;
        *) fail "${metodo} ${caminho} → ${http_code}: $(sed '$d' <<< "${resposta}")" ;;
    esac
}

# Retira o tipo antigo a partir do corte, olhando a série inteira de versões, e não só a
# vigente hoje: a versão que atravessa o corte termina nele, e a que começa no corte ou depois
# (agendada) é removida, senão o código antigo voltaria a valer quando ela começasse.
encerrar_vigencia() {
    local codigo="$1" versao id inicio fim corpo alterou=0
    while IFS= read -r versao; do
        id="$(jq -r '.id' <<< "${versao}")"
        inicio="$(jq -r '.vigenciaInicio' <<< "${versao}")"
        fim="$(jq -r '.vigenciaFim // empty' <<< "${versao}")"

        if [[ ! "${inicio}" < "${DATA_CORTE}" ]]; then
            alterou=1
            if [[ "${DRY_RUN}" == "1" ]]; then
                ok "${codigo}: removeria a versão que começa em ${inicio} (DRY_RUN)"
                continue
            fi
            escrever DELETE "/api/publicacoes/admin/tipos-ato/${id}" ""
            ok "${codigo}: versão que começaria em ${inicio} removida"
        elif [[ -z "${fim}" || "${fim}" > "${DATA_CORTE}" ]]; then
            alterou=1
            # A regra do cadastro recusa tipo que seja resultado e irreversível, inclusive na edição que
            # só encerra a vigência; o ato já publicado guarda as marcas por valor e não é afetado.
            corpo="$(jq -c --arg fim "${DATA_CORTE}" '{
                id, codigo, nome, congelaConfiguracao, unicoPorObjeto,
                efeitoIrreversivel: (if .ehResultado and .efeitoIrreversivel then false else .efeitoIrreversivel end),
                ehResultado, publicavelComoAvulso, exigeAtoCorrigido, vigenciaInicio,
                vigenciaFim: $fim, baseLegal
            }' <<< "${versao}")"
            if [[ "${DRY_RUN}" == "1" ]]; then
                ok "${codigo}: encerraria em ${DATA_CORTE} a versão iniciada em ${inicio} (DRY_RUN)"
                continue
            fi
            escrever PUT "/api/publicacoes/admin/tipos-ato/${id}" "${corpo}"
            ok "${codigo}: versão iniciada em ${inicio} encerrada em ${DATA_CORTE}"
        fi
    done < <(jq -c --arg codigo "${codigo}" '.[] | select(.codigo == $codigo)' <<< "${CATALOGO}")

    [[ "${alterou}" == "1" ]] || ok "${codigo}: nenhuma versão vale no corte ou depois, nada a retirar"
}

criar_tipo() {
    local codigo="$1" nome="$2" corte corpo sucessora
    # Atribuição à parte: dentro de [[ -n "$(...)" ]] a falha do GET seria engolida.
    # Só a versão vigente no corte substitui os tipos antigos; ela cobre também a versão que
    # uma execução anterior já criou a partir do corte.
    corte="$(vigente "${codigo}" "${DATA_CORTE}")"
    if [[ -n "${corte}" ]]; then
        ok "${codigo}: já tem versão vigente em ${DATA_CORTE}, nada a criar"
        return
    fi

    corpo="$(jq -c --arg codigo "${codigo}" --arg corte "${DATA_CORTE}" '
        [.[] | select(.codigo == $codigo)]
        | (map(select(.vigenciaInicio <= $corte and (.vigenciaFim == null or .vigenciaFim > $corte))) | last)
          // last // empty' "${SEED}")"
    if [[ -n "${corpo}" ]]; then
        # Do seed vêm os atributos; a vigência começa no corte, como a dos tipos antigos
        # termina nele, sem sobreposição retroativa nem lacuna.
        corpo="$(jq -c --arg corte "${DATA_CORTE}" '.vigenciaInicio = $corte' <<< "${corpo}")"
    else
        [[ -n "${nome}" ]] || fail "${codigo} não consta no seed e o mapa não traz o nome"
        corpo="$(jq -nc --arg codigo "${codigo}" --arg nome "${nome}" --arg inicio "${DATA_CORTE}" '{
            codigo: $codigo, nome: $nome, congelaConfiguracao: false, unicoPorObjeto: false,
            efeitoIrreversivel: false, ehResultado: true, publicavelComoAvulso: false,
            exigeAtoCorrigido: false, vigenciaInicio: $inicio, vigenciaFim: null, baseLegal: null
        }')"
    fi

    # Se o código já tem versão agendada para depois do corte, a nova termina onde ela começa,
    # em vez de ficar aberta e sobrepor a série que já existe.
    sucessora="$(jq -r --arg codigo "${codigo}" --arg corte "${DATA_CORTE}" '
        [.[] | select(.codigo == $codigo and .vigenciaInicio > $corte) | .vigenciaInicio] | min // empty' <<< "${CATALOGO}")"
    corpo="$(jq -c --arg fim "${sucessora}" '.vigenciaFim = (if $fim == "" then null else $fim end)' <<< "${corpo}")"

    if [[ "${DRY_RUN}" == "1" ]]; then
        ok "${codigo}: criaria o tipo (DRY_RUN)"
        return
    fi

    escrever POST "/api/publicacoes/admin/tipos-ato" "${corpo}"
    ok "${codigo}: tipo criado"
}

# Alinha ao seed os atributos de documento avulso de toda versão do tipo que ainda vale: a vigente
# hoje e as agendadas. Uma versão agendada que nascesse com os atributos desligados tiraria o tipo
# de documento avulso quando começasse. $1=código
definir_atributos_de_avulso() {
    local codigo="$1" hoje versao id inicio esperado corpo encontrou=0
    hoje="$(date -u +%F)"

    esperado="$(jq -c --arg codigo "${codigo}" --arg corte "${DATA_CORTE}" '
        [.[] | select(.codigo == $codigo)]
        | (map(select(.vigenciaInicio <= $corte and (.vigenciaFim == null or .vigenciaFim > $corte))) | last)
          // last // empty
        | {publicavelComoAvulso, exigeAtoCorrigido}' "${SEED}")"
    [[ -n "${esperado}" ]] || fail "${codigo} não consta no seed"

    while IFS= read -r versao; do
        encontrou=1
        id="$(jq -r '.id' <<< "${versao}")"
        inicio="$(jq -r '.vigenciaInicio' <<< "${versao}")"

        if jq -e --argjson esperado "${esperado}" \
            '(.publicavelComoAvulso // false) == $esperado.publicavelComoAvulso and (.exigeAtoCorrigido // false) == $esperado.exigeAtoCorrigido' \
            <<< "${versao}" >/dev/null; then
            ok "${codigo}: versão iniciada em ${inicio} já confere com o seed"
            continue
        fi

        if [[ "${DRY_RUN}" == "1" ]]; then
            ok "${codigo}: definiria os atributos de documento avulso da versão iniciada em ${inicio} (DRY_RUN)"
            continue
        fi

        corpo="$(jq -c --argjson esperado "${esperado}" '{
            id, codigo, nome, congelaConfiguracao, unicoPorObjeto, efeitoIrreversivel, ehResultado,
            publicavelComoAvulso: $esperado.publicavelComoAvulso, exigeAtoCorrigido: $esperado.exigeAtoCorrigido,
            vigenciaInicio, vigenciaFim, baseLegal
        }' <<< "${versao}")"
        escrever PUT "/api/publicacoes/admin/tipos-ato/${id}" "${corpo}"
        ok "${codigo}: atributos de documento avulso da versão iniciada em ${inicio} definidos"
    done < <(jq -c --arg codigo "${codigo}" --arg hoje "${hoje}" '
        .[] | select(.codigo == $codigo and (.vigenciaFim == null or .vigenciaFim > $hoje))' <<< "${CATALOGO}")

    [[ "${encontrou}" == "1" ]] || ok "${codigo}: sem versão vigente nem agendada, nada a definir"
}

# Lê o mapa sem comentários nem linhas em branco: antigo<TAB>novo<TAB>nome.
linhas_do_mapa() { grep -vE '^[[:space:]]*(#|$)' "${MAPA}"; }

# Toda versão de um código novo que já existe e vale no corte ou depois dele (a vigente no corte
# e as agendadas) só serve de tipo por matéria se tiver o contrato dele: é resultado (aceita
# papel), não é único (sai como preliminar e definitivo), não congela a configuração e não tem
# efeito irreversível (o preliminar ancora a janela de recurso, que não aceita ato irreversível).
# Divergência é decisão do catálogo: o roteiro para antes de qualquer escrita, em vez de alterá-la.
conferir_versoes_dos_tipos_novos() {
    local codigo divergentes
    while IFS=$'\t' read -r _ codigo _; do
        divergentes="$(jq -c --arg codigo "${codigo}" --arg corte "${DATA_CORTE}" '
            [.[] | select(.codigo == $codigo and (.vigenciaFim == null or .vigenciaFim > $corte))
                 | select((.ehResultado == true and .unicoPorObjeto == false and .congelaConfiguracao == false
                           and .efeitoIrreversivel == false) | not)
                 | {vigenciaInicio, ehResultado, unicoPorObjeto, congelaConfiguracao, efeitoIrreversivel}]' <<< "${CATALOGO}")"
        [[ "${divergentes}" == "[]" ]] \
            || fail "${codigo}: versão que vale a partir de ${DATA_CORTE} não é resultado não único, sem congelamento e reversível ${divergentes}; ajuste-a no catálogo antes de consolidar"
    done < <(linhas_do_mapa)
}

# O tipo antigo que é resultado e irreversível perde a marca de irreversível ao ser encerrado, e
# continua valendo até o corte. Com o corte no futuro, os atos publicados até lá copiariam a marca
# já retirada: nesse caso o roteiro para antes de qualquer escrita e pede o corte de hoje.
conferir_corte_dos_tipos_antigos() {
    local codigo afetados
    [[ ! "${DATA_CORTE}" > "$(date -u +%F)" ]] && return 0
    while IFS=$'\t' read -r codigo _ _; do
        afetados="$(jq -c --arg codigo "${codigo}" --arg corte "${DATA_CORTE}" '
            [.[] | select(.codigo == $codigo and .vigenciaInicio < $corte
                          and (.vigenciaFim == null or .vigenciaFim > $corte)
                          and .ehResultado == true and .efeitoIrreversivel == true)
                 | .vigenciaInicio]' <<< "${CATALOGO}")"
        [[ "${afetados}" == "[]" ]] \
            || fail "${codigo}: é resultado e irreversível e perderia a marca antes do corte ${DATA_CORTE}; rode a consolidação com o corte de hoje"
    done < <(linhas_do_mapa)
}

CATALOGO="$(catalogo_completo)"
readonly CATALOGO
conferir_versoes_dos_tipos_novos
conferir_corte_dos_tipos_antigos

log "Criando os tipos por matéria (antes de encerrar os antigos)"
declare -A JA_TRATADOS=()
while IFS=$'\t' read -r _ novo nome; do
    [[ -z "${JA_TRATADOS[${novo}]:-}" ]] || continue
    JA_TRATADOS["${novo}"]=1
    criar_tipo "${novo}" "${nome}"
done < <(linhas_do_mapa)

log "Retirando os tipos antigos a partir de ${DATA_CORTE}"
while IFS=$'\t' read -r antigo _ _; do
    encerrar_vigencia "${antigo}"
done < <(linhas_do_mapa)

log "Definindo os atributos de documento avulso de AVISO, COMUNICADO e ERRATA"
for codigo in AVISO COMUNICADO ERRATA; do
    definir_atributos_de_avulso "${codigo}"
done

log "Catálogo consolidado"
