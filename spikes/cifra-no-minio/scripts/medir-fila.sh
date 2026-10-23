#!/usr/bin/env bash
# Medições do fluxo com fila, comparado ao fluxo por webhook nas mesmas condições: cada clamd com
# 2 CPUs (o tamanho de um sidecar), o verificador e a API com 2 CPUs e 1 GiB, paralelismo 8 por
# verificador. Em cada cenário, a CPU de cada contêiner é amostrada durante a carga.
#   latencia  um envio por vez, 5 MB: webhook, RabbitMQ, Postgres (polling de 100 ms e o padrão)
#   vazao     N = 8, 32 e 64 envios simultâneos de 5 MB, uma réplica: RabbitMQ e Postgres
#   escala    64 simultâneos de 5 MB com 2 e 3 réplicas (verificador + clamd): RabbitMQ e Postgres
#   promocao  32 simultâneos de 5 MB, uma réplica, aprovação por etiqueta em vez de cópia
#   memoria   64 simultâneos de 10 MB, 3 réplicas
# Grava uma linha JSON por cenário em resultados/medicoes-fila.jsonl e a tabela em
# resultados/medicoes-fila.md. Uso: ./scripts/medir-fila.sh [seção...] (sem argumento, todas, e
# recomeça o arquivo).
source "$(dirname "$0")/comum.sh"
carregar_env

SAIDA="$RESULTADOS/medicoes-fila.jsonl"
TMP="$RESULTADOS/tmp"
mkdir -p "$TMP"
SECOES=("$@")
if [[ ${#SECOES[@]} -eq 0 ]]; then
  SECOES=(latencia vazao escala promocao memoria)
  : >"$SAIDA"
fi
CARGA=(dotnet "$POC_DIR/src/publicado/Unifesspa.UniPlus.Spikes.CifraNoMinio.dll" carga)
API=localhost:18083
export CPUS_CLAMD=2
CONFIG=""

aguardar_clamd() {
  for servico in "$@"; do
    until [[ "$(docker inspect -f '{{.State.Health.Status}}' "$(dc --profile fila --profile escala3 ps -q "$servico")" 2>/dev/null)" == "healthy" ]]; do sleep 2; done
  done
}

limpar() {
  curl -sf -X DELETE "$API/arquivos" >/dev/null 2>&1 || true
  curl -sf -X DELETE localhost:18082/resultados >/dev/null 2>&1 || true
  for bucket in uniplus-envios uniplus-arquivos-candidato; do
    mc_admin "rm --recursive --force local/$bucket" >/dev/null 2>&1 || true
  done
}

# transporte: webhook, rabbitmq, postgres (polling de 100 ms) ou postgres-padrao (polling do Wolverine).
configurar() {
  local transporte=$1 replicas=$2 promocao=$3
  [[ "$transporte/$replicas/$promocao" == "$CONFIG" ]] && return
  CONFIG="$transporte/$replicas/$promocao"
  echo ">> Configurando: $CONFIG" >&2

  dc --profile fila --profile escala3 stop verificador-fila-2 verificador-fila-3 clamav-2 clamav-3 >/dev/null 2>&1
  if [[ $transporte == webhook ]]; then
    dc --profile fila stop verificador-fila-1 >/dev/null 2>&1
    dc up -d clamav verificador >/dev/null 2>&1
    mc_admin "event add --ignore-existing local/uniplus-envios arn:minio:sqs::VERIFICADOR:webhook --event put" >/dev/null
    aguardar_clamd clamav
    until curl -sf localhost:18082/resultados >/dev/null; do sleep 1; done
  else
    mc_admin "event rm local/uniplus-envios arn:minio:sqs::VERIFICADOR:webhook --event put" >/dev/null 2>&1 || true
    dc stop verificador >/dev/null 2>&1
    local perfis=(--profile fila) servicos=(api-fila verificador-fila-1 clamav) clamds=(clamav) polling=""
    [[ $replicas -ge 2 ]] && perfis+=(--profile "escala$replicas") && servicos+=(verificador-fila-2 clamav-2) && clamds+=(clamav-2)
    [[ $replicas -ge 3 ]] && servicos+=(verificador-fila-3 clamav-3) && clamds+=(clamav-3)
    [[ $transporte == postgres ]] && polling=100
    TRANSPORTE=${transporte%-padrao} POLLING_MS=$polling PROMOCAO=$promocao \
      dc "${perfis[@]}" up -d --build "${servicos[@]}" >/dev/null 2>&1
    aguardar_clamd "${clamds[@]}"
    until curl -sf "$API/arquivos/resumo" >/dev/null; do sleep 1; done
    sleep 3
  fi

  local modo=fila
  [[ $transporte == webhook ]] && modo=nova
  "${CARGA[@]}" --modo "$modo" --tamanho-mb 1 --quantidade 16 --concorrencia 4 >/dev/null 2>&1 || true
  limpar
}

contenedores() { dc --profile fila --profile escala3 ps -q; }

# Pico de memória residente de cada processo da aplicação, zerado antes do cenário.
zerar_memoria() {
  for c in $(dc --profile fila --profile escala3 ps -q api-fila verificador-fila-1 verificador-fila-2 verificador-fila-3 verificador 2>/dev/null); do
    docker exec "$c" sh -c 'echo 5 > /proc/1/clear_refs' 2>/dev/null || true
  done
}

ler_memoria() {
  printf '{'
  local primeiro=1
  for c in $(contenedores); do
    local nome pico
    nome=$(docker inspect -f '{{.Name}}' "$c")
    case $nome in
      *api-fila* | *verificador*) pico=$(docker exec "$c" sh -c "grep VmHWM /proc/1/status | tr -s ' ' | cut -d' ' -f2" 2>/dev/null) ;;
      *clamav*) pico=$(docker exec "$c" sh -c 'cat /sys/fs/cgroup/memory.current' 2>/dev/null) && pico=$((pico / 1024)) ;;
      *) continue ;;
    esac
    [[ $primeiro == 1 ]] || printf ','
    primeiro=0
    printf '"%s":%s' "${nome#/uniplus-poc-sse-kms-}" "$((${pico:-0} / 1024))"
  done
  printf '}'
}

cenario() {
  local rotulo=$1 transporte=$2 replicas=$3 promocao=$4 tamanho=$5 quantidade=$6 concorrencia=$7
  # Com o disco quase cheio, o MinIO recusa gravação, e a falha pareceria do fluxo medido. O que a
  # limpeza remove fica na lixeira do MinIO (.minio.sys/tmp) até um expurgo assíncrono; reiniciar o
  # MinIO a esvazia.
  local livre_gib
  livre_gib=$(( $(df --output=avail -k "$(docker info -f '{{.DockerRootDir}}')" | tail -1) / 1024 / 1024 ))
  if (( livre_gib < 3 )); then
    echo ">> Disco com ${livre_gib} GiB livres: reiniciando o MinIO para esvaziar a lixeira" >&2
    dc restart minio >/dev/null 2>&1
    until [[ "$(docker inspect -f '{{.State.Health.Status}}' "$(dc ps -q minio)")" == "healthy" ]]; do sleep 2; done
    sleep 5
    livre_gib=$(( $(df --output=avail -k "$(docker info -f '{{.DockerRootDir}}')" | tail -1) / 1024 / 1024 ))
  fi
  if (( livre_gib < 3 )); then
    echo "Disco com ${livre_gib} GiB livres: medição interrompida antes de $rotulo/$transporte/$replicas." >&2
    exit 1
  fi
  configurar "$transporte" "$replicas" "$promocao"
  local modo=fila
  [[ $transporte == webhook ]] && modo=nova

  zerar_memoria
  local ids
  mapfile -t ids < <(contenedores)
  (while :; do
    docker stats --no-stream --format '{{.Name}};{{.CPUPerc}}' "${ids[@]}" 2>/dev/null | sed "s/^/$(date +%s.%N);/"
  done) >"$TMP/cpu.txt" &
  local amostrador=$!
  local resumo
  resumo=$("${CARGA[@]}" --modo "$modo" --tamanho-mb "$tamanho" --quantidade "$quantidade" --concorrencia "$concorrencia" --rotulo "$rotulo" || true)
  kill "$amostrador" 2>/dev/null
  wait "$amostrador" 2>/dev/null || true

  local distribuicao="{}"
  [[ $modo == fila ]] && distribuicao=$(dc exec -T postgres psql -U poc -d uniplus_poc -At -c \
    "select coalesce(json_object_agg(\"Verificador\", n), '{}') from (select \"Verificador\", count(*) n from poc.arquivo_enviado where \"Verificador\" is not null group by 1) d")

  python3 - "$resumo" "$(ler_memoria)" "$TMP/cpu.txt" "$transporte" "$replicas" "$promocao" "$distribuicao" <<'EOF' | tee -a "$SAIDA"
import collections, json, re, sys
r = json.loads(sys.argv[1])
r["memoriaMiB"] = json.loads(sys.argv[2])
r["transporte"], r["replicas"], r["promocao"] = sys.argv[4], int(sys.argv[5]), sys.argv[6]
r["porVerificador"] = sorted(json.loads(sys.argv[7]).values(), reverse=True)

def familia(nome):
    nome = re.sub(r"^uniplus-poc-sse-kms-", "", nome)
    nome = re.sub(r"-1$", "", nome)
    if nome.startswith("verificador"): return "verificador"
    if nome.startswith("clamav"): return "clamd"
    if nome == "api-fila": return "api"
    return nome

amostras = collections.defaultdict(lambda: collections.defaultdict(float))
for linha in open(sys.argv[3]):
    partes = linha.strip().split(";")
    if len(partes) != 3 or not partes[2].endswith("%"):
        continue
    instante = partes[0]
    amostras[instante][familia(partes[1])] += float(partes[2][:-1]) / 100
familias = sorted({f for a in amostras.values() for f in a})
r["cpuNucleos"] = {
    f: {"media": round(sum(a.get(f, 0) for a in amostras.values()) / max(len(amostras), 1), 2),
        "max": round(max((a.get(f, 0) for a in amostras.values()), default=0), 2)}
    for f in familias if f not in ("api-nova", "api-atual")
}
print(json.dumps(r, ensure_ascii=False))
EOF
  limpar
}

secao_latencia() {
  echo ">> Latência: um envio por vez, 5 MB" >&2
  cenario latencia webhook 1 copia 5 20 1
  cenario latencia rabbitmq 1 copia 5 20 1
  cenario latencia postgres 1 copia 5 20 1
  cenario latencia postgres-padrao 1 copia 5 10 1
}

secao_vazao() {
  echo ">> Vazão: N envios simultâneos de 5 MB, uma réplica" >&2
  for transporte in rabbitmq postgres; do
    cenario vazao "$transporte" 1 copia 5 40 8
    cenario vazao "$transporte" 1 copia 5 128 32
    cenario vazao "$transporte" 1 copia 5 256 64
  done
}

secao_escala() {
  echo ">> Escala: 64 envios simultâneos de 5 MB com 2 e 3 réplicas" >&2
  for transporte in rabbitmq postgres; do
    cenario escala "$transporte" 2 copia 5 256 64
    cenario escala "$transporte" 3 copia 5 384 64
  done
}

secao_promocao() {
  echo ">> Aprovação por etiqueta (sem cópia), uma réplica" >&2
  cenario promocao-etiqueta rabbitmq 1 etiqueta 5 128 32
}

secao_memoria() {
  echo ">> Memória: 64 envios simultâneos de 10 MB, 3 réplicas" >&2
  cenario memoria-10mb rabbitmq 3 copia 10 128 64
}

dotnet publish "$POC_DIR/src/Unifesspa.UniPlus.Spikes.CifraNoMinio" -c Release -o "$POC_DIR/src/publicado" --nologo -v q
for secao in "${SECOES[@]}"; do
  "secao_${secao}"
done

# Volta ao estado das provas: RabbitMQ, uma réplica, cópia.
configurar rabbitmq 1 copia
dc --profile fila --profile escala3 stop verificador-fila-2 verificador-fila-3 clamav-2 clamav-3 >/dev/null 2>&1

python3 - "$SAIDA" >"$RESULTADOS/medicoes-fila.md" <<'EOF'
import json, sys
linhas = [json.loads(l) for l in open(sys.argv[1])]
print("| cenário | transporte | réplicas | promoção | MB | N | arquivos | falhas | arq/s | até o veredicto p50 / p95 (ms) | espera na fila p50 / p95 (ms) | verificação p50 / p95 (ms) | CPU média (núcleos): verificador / clamd / MinIO / KES / API / broker | arquivos por réplica | pico RSS verificador (MiB) |")
print("|---|---|---:|---|---:|---:|---:|---:|---:|---|---|---|---|---|---:|")
for r in linhas:
    c = r["cpuNucleos"]
    def cpu(f): return f'{c[f]["media"]:.2f}' if f in c else "—"
    broker = "rabbitmq" if r["transporte"] == "rabbitmq" else "postgres"
    av, vf, nt = r["ateVeredictoMs"], r["verificacaoMs"], r["notificacaoMs"]
    fila = f'{nt.get("p50")} / {nt.get("p95")}' if r["transporte"] != "webhook" else f'{nt.get("p50")} / {nt.get("p95")} (notificação)'
    falhas = r["falhas"] if not r.get("erros") else f'{r["falhas"]} ({", ".join(f"{k}: {v}" for k, v in r["erros"].items())})'
    ver = max((v for k, v in r["memoriaMiB"].items() if k.startswith("verificador")), default="—")
    por = " / ".join(str(n) for n in r["porVerificador"]) or "—"
    print(f'| {r["rotulo"]} | {r["transporte"]} | {r["replicas"]} | {r["promocao"]} | {r["tamanhoMb"]} | {r["concorrencia"]} | {r["quantidade"]} | {falhas} | '
          f'{r["arquivosPorS"]} | {av.get("p50")} / {av.get("p95")} | {fila} | {vf.get("p50")} / {vf.get("p95")} | '
          f'{cpu("verificador")} / {cpu("clamd")} / {cpu("minio")} / {cpu("kes")} / {cpu("api")} / {cpu(broker) if r["transporte"] != "webhook" else "—"} | {por} | {ver} |')
EOF
cat "$RESULTADOS/medicoes-fila.md"
