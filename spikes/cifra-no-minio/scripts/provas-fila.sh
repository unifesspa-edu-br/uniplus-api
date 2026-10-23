#!/usr/bin/env bash
# Provas do fluxo com fila (a confirmação do envio publica o pedido de verificação pelo outbox do
# Wolverine) sobre a stack de scripts/subir.sh, com RabbitMQ e com a fila do Postgres. Grava
# comando e saída em resultados/provas-fila.txt.
# Uso: ./scripts/provas-fila.sh [rabbitmq|postgres|etiqueta|trace ...]  (sem argumento, todas)
source "$(dirname "$0")/comum.sh"
carregar_env

TMP="$RESULTADOS/tmp"
mkdir -p "$TMP"
exec > >(sed -u "s#$POC_DIR#.#g" | tee "$RESULTADOS/provas-fila.txt") 2>&1

# Postgres, RabbitMQ, a API e o verificador do fluxo com fila estão no perfil "fila" do compose:
# sem ele, start, stop e exec nesses serviços não fazem nada.
dc() {
  docker compose --project-directory "$POC_DIR" -f "$POC_DIR/docker-compose.yml" --env-file "$ESTADO/env" --profile fila "$@"
}
export -f dc

API=localhost:18083
CARGA=(dotnet "$POC_DIR/src/publicado/Unifesspa.UniPlus.Spikes.CifraNoMinio.dll" carga)
# A fila do Postgres é lida por polling; 100 ms é o valor usado nas medições.
POLLING_POSTGRES=100

titulo() { printf '\n\n==================== %s ====================\n' "$*"; }
nota() { printf '\n# %s\n' "$*"; }

sql() { dc exec -T postgres psql -U poc -d uniplus_poc -At -F ' | ' -c "$1"; }
resumo() { mostrar curl -sf "$API/arquivos/resumo"; echo; }
estado() { curl -sf "$API/arquivos/$1/estado"; }
objetos() { mc_admin "ls --recursive local/$1" 2>/dev/null | wc -l; }
filas_rabbitmq() { mostrar dc exec -T rabbitmq rabbitmqctl -q list_queues name type messages messages_unacknowledged consumers; }

# Sobe a API e o verificador com o transporte pedido; o restante do ambiente segue de pé.
subir_fila() {
  local transporte=$1 polling=""
  [[ $transporte == postgres ]] && polling=$POLLING_POSTGRES
  TRANSPORTE=$transporte POLLING_MS=$polling PROMOCAO=${PROMOCAO:-copia} \
    dc up -d --build --force-recreate api-fila verificador-fila-1 >/dev/null 2>&1
  until curl -sf "$API/arquivos/resumo" >/dev/null; do sleep 1; done
  sleep 3
}

# Envia e confirma N arquivos de T MB, sem esperar o veredicto; imprime só falhas e veredictos.
enviar() {
  # A carga sai com 1 quando algum envio falha, o que algumas provas esperam.
  { "${CARGA[@]}" --modo fila --quantidade "$1" --tamanho-mb "$2" --concorrencia "${3:-8}" --aguardar nao || true; } |
    python3 -c 'import json,sys;r=json.load(sys.stdin);print("enviados e confirmados: %d de %d; falhas: %s" % (r["quantidade"]-r["falhas"], r["quantidade"], r["erros"] or 0))'
}

aguardar_veredictos() {
  timeout "${1:-240}" bash -c "until ! curl -sf $API/arquivos/resumo | grep -q EM_VERIFICACAO; do sleep 1; done" ||
    echo "(ainda há arquivos em verificação depois de ${1:-240} s)"
}

aguardar_clamd() {
  until [[ "$(docker inspect -f '{{.State.Health.Status}}' "$(dc ps -q clamav)")" == "healthy" ]]; do sleep 2; done
}

limpar() {
  curl -sf -X DELETE "$API/arquivos" >/dev/null
  for bucket in uniplus-envios uniplus-arquivos-candidato; do
    mc_admin "rm --recursive --force local/$bucket" >/dev/null 2>&1 || true
  done
  dc exec -T rabbitmq rabbitmqctl -q purge_queue wolverine-dead-letter-queue >/dev/null 2>&1 || true
  sql "delete from wolverine_verificador.wolverine_dead_letters; delete from wolverine_api.wolverine_dead_letters" >/dev/null 2>&1 || true
}

pendentes_no_transporte() {
  if [[ $1 == rabbitmq ]]; then
    filas_rabbitmq
  else
    mostrar sql "select 'na fila', count(*) from wolverine_filas.wolverine_queue_verificacao
      union all select 'no inbox do verificador', count(*) from wolverine_verificador.wolverine_incoming_envelopes
      union all select 'na fila de erro', count(*) from wolverine_verificador.wolverine_dead_letters"
  fi
}

fila_de_erro() {
  if [[ $1 == rabbitmq ]]; then
    mostrar dc exec -T rabbitmq rabbitmqctl -q list_queues name messages --no-table-headers | grep dead-letter
  else
    mostrar sql "select message_type, exception_type, count(*) from wolverine_verificador.wolverine_dead_letters group by 1, 2"
  fi
}

descartar_pedidos() {
  if [[ $1 == rabbitmq ]]; then
    mostrar dc exec -T rabbitmq rabbitmqctl -q purge_queue verificacao
  else
    mostrar sql "delete from wolverine_filas.wolverine_queue_verificacao"
  fi
}

provas_do_transporte() {
  local t=$1
  subir_fila "$t"
  limpar

  titulo "$t · 1. Fluxo básico: confirmar, verificar, registrar"
  enviar 5 1
  aguardar_veredictos 60
  resumo
  chave=$(sql "select \"Chave\" from poc.arquivo_enviado limit 1")
  mostrar estado "$chave"; echo
  echo "Objetos no bucket de envio: $(objetos uniplus-envios); no definitivo: $(objetos uniplus-arquivos-candidato)"
  limpar

  titulo "$t · 2. O verificador cai no meio das verificações (SIGKILL)"
  nota "40 arquivos de 10 MB; o verificador é morto depois dos primeiros veredictos e só volta 10 s depois."
  # Sem isso, a política de reinício do Docker o traria de volta na hora.
  docker update --restart=no "$(dc ps -q verificador-fila-1)" >/dev/null
  enviar 40 10 16
  timeout 120 bash -c "until [[ \$(curl -sf $API/arquivos/resumo | grep -o '\"estado\":\"VERIFICADO\",\"arquivos\":[0-9]*' | grep -o '[0-9]*\$') -ge 5 ]]; do sleep 0.2; done"
  mostrar docker kill -s KILL "$(dc ps -q verificador-fila-1)"
  # As métricas da quorum queue se atualizam a cada poucos segundos.
  sleep 8
  nota "Com o verificador fora: o que estava com ele voltou para a fila."
  resumo
  pendentes_no_transporte "$t"
  sleep 2
  dc start verificador-fila-1 >/dev/null 2>&1
  docker update --restart=unless-stopped "$(dc ps -q verificador-fila-1)" >/dev/null
  aguardar_veredictos 300
  nota "Depois de o verificador voltar: nenhum arquivo perdido. Veredictos acima de 40 são entregas repetidas, absorvidas."
  resumo
  echo "Objetos no bucket de envio: $(objetos uniplus-envios); no definitivo: $(objetos uniplus-arquivos-candidato)"
  limpar

  titulo "$t · 3. O clamd fora do ar: o arquivo segue em verificação e nunca é recusado"
  mostrar dc stop clamav
  enviar 5 1
  sleep 10
  nota "10 s depois: em verificação, com o verificador repetindo (1 s, 5 s, 15 s)."
  resumo
  sleep 25
  nota "Esgotadas as tentativas, o pedido vai para a fila de erro; o arquivo continua em verificação."
  fila_de_erro "$t"
  resumo
  dc start clamav >/dev/null 2>&1
  aguardar_clamd
  nota "Com o clamd de volta, a reconciliação republica o que está em verificação."
  mostrar curl -sf -X POST "$API/reconciliar?segundos=0"; echo
  aguardar_veredictos 120
  resumo
  limpar

  titulo "$t · 4. Pedido perdido no transporte: a reconciliação pelo banco da API recupera"
  dc stop verificador-fila-1 >/dev/null 2>&1
  enviar 5 1
  sleep 2
  descartar_pedidos "$t"
  dc start verificador-fila-1 >/dev/null 2>&1
  sleep 15
  nota "15 s depois de o verificador voltar: nada chegou, porque os pedidos foram descartados."
  resumo
  mostrar curl -sf -X POST "$API/reconciliar?segundos=0"; echo
  aguardar_veredictos 120
  resumo
  limpar

  titulo "$t · 5. Reentrega de um arquivo já promovido devolve o mesmo veredicto"
  enviar 1 1
  aguardar_veredictos 60
  chave=$(sql "select \"Chave\" from poc.arquivo_enviado limit 1")
  antes=$(sql "select \"Sha256\" from poc.arquivo_enviado where \"Chave\" = '$chave'")
  nota "O arquivo volta a 'em verificação' no banco e é republicado; o objeto já está no bucket definitivo."
  sql "update poc.arquivo_enviado set \"Estado\" = 'EM_VERIFICACAO' where \"Chave\" = '$chave'" >/dev/null
  mostrar curl -sf -X POST "$API/reconciliar?segundos=0"; echo
  aguardar_veredictos 60
  mostrar sql "select \"Estado\", \"Veredicto\", \"Sha256\" = '$antes' as mesmo_sha256, \"Veredictos\", \"Publicacoes\" from poc.arquivo_enviado where \"Chave\" = '$chave'"
  limpar

  if [[ $t == rabbitmq ]]; then
    titulo "$t · 6. O RabbitMQ fora do ar: a confirmação continua, o outbox segura os pedidos"
    mostrar dc stop rabbitmq
    enviar 10 1
    resumo
    mostrar sql "select count(*) as pedidos_no_outbox_da_api from wolverine_api.wolverine_outgoing_envelopes"
    dc start rabbitmq >/dev/null 2>&1
    until dc exec -T rabbitmq rabbitmq-diagnostics -q check_port_connectivity >/dev/null 2>&1; do sleep 1; done
    aguardar_veredictos 300
    nota "Com o RabbitMQ de volta, o outbox entrega e tudo é verificado."
    resumo
    mostrar sql "select count(*) as pedidos_no_outbox_da_api from wolverine_api.wolverine_outgoing_envelopes"
    limpar

    titulo "$t · 7. Arquivo acima do StreamMaxLength do clamd (100 MB no clamd 1.4)"
    mostrar dc exec -T clamav sh -c 'clamconf | grep -E "^(StreamMaxLength|MaxThreads) "'
    enviar 1 110 1
    sleep 35
    nota "O clamd recusa o stream acima do limite; o verificador trata como falha passageira e, esgotadas as tentativas, o pedido vai à fila de erro. O arquivo fica em verificação, sem ser recusado: o tamanho máximo aceito no envio precisa ficar abaixo do limite do clamd."
    resumo
    fila_de_erro "$t"
    limpar
  else
    titulo "$t · 6. O Postgres fora do ar: a confirmação falha inteira, sem metade gravada"
    mostrar dc stop postgres
    enviar 3 1 1
    dc start postgres >/dev/null 2>&1
    until dc exec -T postgres pg_isready -U poc -d uniplus_poc >/dev/null 2>&1; do sleep 1; done
    timeout 60 bash -c "until curl -sf $API/arquivos/resumo >/dev/null; do sleep 1; done" || true
    nota "Com o Postgres de volta: nenhum arquivo em verificação sem pedido, nem pedido sem arquivo."
    resumo
    mostrar sql "select count(*) as pedidos_pendentes from wolverine_filas.wolverine_queue_verificacao"
    limpar
  fi
}

prova_trace() {
  subir_fila rabbitmq
  limpar
  titulo "A API não lê o conteúdo no fluxo com fila"
  declare -A NOMES
  for servico in api-fila verificador-fila-1; do
    NOMES[$(docker inspect -f '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}' "$(dc ps -q "$servico")")]=$servico
  done
  NOMES[172.31.250.1]="candidato (host)"
  dc exec -T minio sh -c 'mc alias set local http://localhost:9000 "$MINIO_ROOT_USER" "$MINIO_ROOT_PASSWORD" >/dev/null; timeout 20 mc admin trace --json local' >"$TMP/trace-fila.json" 2>/dev/null &
  sleep 2
  enviar 5 10 1
  aguardar_veredictos 60
  wait
  python3 - "$TMP/trace-fila.json" "$(for ip in "${!NOMES[@]}"; do printf '%s=%s;' "$ip" "${NOMES[$ip]}"; done)" <<'EOF'
import collections, json, sys
nomes = dict(p.split("=", 1) for p in sys.argv[2].split(";") if p)
soma = collections.defaultdict(lambda: [0, 0, 0])
for linha in open(sys.argv[1]):
    try:
        t = json.loads(linha)
    except ValueError:
        continue
    if t.get("type") != "S3":
        continue
    chave = (nomes.get(t["client"], t["client"]), t["api"])
    soma[chave][0] += 1
    soma[chave][1] += t["callStats"]["rx"]
    soma[chave][2] += t["callStats"]["tx"]
print(f"{'origem':<20} {'operação':<18} {'chamadas':>8} {'bytes enviados ao MinIO':>24} {'bytes lidos do MinIO':>21}")
for (origem, api), (n, rx, tx) in sorted(soma.items()):
    print(f"{origem:<20} {api:<18} {n:>8} {rx:>24} {tx:>21}")
print("\nNenhuma linha com origem api-fila: confirmar só grava no banco e publica a chave.")
EOF
  limpar
}

prova_etiqueta() {
  subir_fila rabbitmq
  limpar
  titulo "Alternativa sem cópia: o arquivo aprovado ganha uma etiqueta no próprio bucket de envio"
  nota "A credencial api-etiqueta só lê do bucket de envio o objeto com a etiqueta verificacao=aprovado."
  mostrar cat "$POC_DIR/config/politica-api-etiqueta.json"

  head -c $((1024 * 1024)) /dev/urandom >"$TMP/um-mb.pdf"
  resposta=$(curl -sf -X POST "$API/envios")
  CHAVE=$(python3 -c 'import json,sys;print(json.loads(sys.argv[1])["chave"])' "$resposta")
  URL=$(python3 -c 'import json,sys;print(json.loads(sys.argv[1])["url"])' "$resposta")

  nota "1. O candidato tenta se aprovar: acrescenta x-amz-tagging ao PUT pela URL pré-assinada."
  printf '\n$ curl -T um-mb.pdf -H "x-amz-tagging: verificacao=aprovado" <URL de envio>\n'
  curl -s -o "$TMP/resposta-etiqueta.xml" -w 'HTTP %{http_code}\n' -T "$TMP/um-mb.pdf" \
    -H 'Content-Type: application/pdf' -H 'x-amz-tagging: verificacao=aprovado' "$URL"
  sed -n 's/.*<Code>\(.*\)<\/Code>.*/código: \1/p' "$TMP/resposta-etiqueta.xml"
  mostrar mc_admin "tag list local/uniplus-envios/$CHAVE"

  nota "2. Sem a etiqueta, a URL de leitura assinada pela api-etiqueta é recusada."
  curl -s -o /dev/null -T "$TMP/um-mb.pdf" -H 'Content-Type: application/pdf' "$URL"
  ler_como_api() {
    local url
    url=$("${CARGA[0]}" "${CARGA[1]}" assinar-leitura api-etiqueta "$SENHA_API" uniplus-envios "$CHAVE")
    curl -s -o /dev/null -w 'GET pela URL assinada com a credencial api-etiqueta: HTTP %{http_code}\n' "$url"
  }
  ler_como_api
  nota "3. Com a etiqueta posta pelo verificador, a mesma leitura passa."
  mc_admin "tag set local/uniplus-envios/$CHAVE verificacao=aprovado" >/dev/null
  ler_como_api
  nota "4. Se o candidato regravar o objeto pela mesma URL, a etiqueta não sobrevive: a leitura volta a ser recusada."
  curl -s -o /dev/null -T "$TMP/um-mb.pdf" -H 'Content-Type: application/pdf' "$URL"
  mostrar mc_admin "tag list local/uniplus-envios/$CHAVE"
  ler_como_api
  limpar

  nota "5. O verificador no modo etiqueta: 5 arquivos, nenhum copiado."
  PROMOCAO=etiqueta subir_fila rabbitmq
  enviar 5 1
  aguardar_veredictos 60
  resumo
  CHAVE=$(sql "select \"Chave\" from poc.arquivo_enviado limit 1")
  mostrar mc_admin "tag list local/uniplus-envios/$CHAVE"
  echo "Objetos no bucket de envio: $(objetos uniplus-envios); no definitivo: $(objetos uniplus-arquivos-candidato)"
  limpar
  subir_fila rabbitmq
}

titulo "Ambiente"
echo "Data: $(date -Iseconds)"
dc exec -T rabbitmq rabbitmqctl -q version | sed 's/^/RabbitMQ /'
dc exec -T postgres postgres --version
echo "Wolverine 5.41.0 (a versão da API); verificador com paralelismo ${PARALELISMO_VERIFICADOR:-8}"

# O fluxo com fila não usa a notificação do bucket: o webhook sai, e o verificador por webhook para.
mc_admin "event rm local/uniplus-envios arn:minio:sqs::VERIFICADOR:webhook --event put" >/dev/null 2>&1 || true
dc stop verificador >/dev/null 2>&1

SECOES=("$@")
[[ ${#SECOES[@]} -eq 0 ]] && SECOES=(rabbitmq postgres trace etiqueta)
for secao in "${SECOES[@]}"; do
  case $secao in
    rabbitmq | postgres) provas_do_transporte "$secao" ;;
    trace) prova_trace ;;
    etiqueta) prova_etiqueta ;;
  esac
done
subir_fila rabbitmq
