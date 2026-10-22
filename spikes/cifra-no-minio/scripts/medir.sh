#!/usr/bin/env bash
# Medições da POC: tempo por arquivo (1, 5 e 10 MB, um por vez), vazão com N candidatos enviando ao
# mesmo tempo e pico de memória de cada papel, no fluxo novo (verificador) e no atual (a API lê o
# conteúdo). N = 1 é a base sem disputa; 8 fica perto do paralelismo do verificador (8) e do limite de
# conexões do clamd (10); 32 e 64 são rajadas de 4 e 8 vezes essa capacidade, como na véspera do
# encerramento. Ao fim, o fluxo novo de novo com o webhook sem fila em disco, para separar o atraso da
# entrega da notificação do tempo de verificação.
# Por último, o verificador com paralelismo 32, para comparar com o fluxo atual na mesma concorrência.
# Grava uma linha JSON por cenário em resultados/medicoes.jsonl e a tabela em resultados/medicoes.md.
# Uso: ./scripts/medir.sh [seção...]  (sem argumento roda todas e recomeça o arquivo; com argumento,
# acrescenta só as seções pedidas: por-arquivo vazao memoria sem-fila paralelismo).
source "$(dirname "$0")/comum.sh"
carregar_env

SAIDA="$RESULTADOS/medicoes.jsonl"
SECOES=("$@")
if [[ ${#SECOES[@]} -eq 0 ]]; then
  SECOES=(por-arquivo vazao memoria sem-fila paralelismo)
  : >"$SAIDA"
fi
CARGA=(dotnet "$POC_DIR/src/publicado/Unifesspa.UniPlus.Spikes.CifraNoMinio.dll" carga)
declare -A PORTA=([api-nova]=18080 [api-atual]=18081 [verificador]=18082)

zerar_memoria() {
  for papel in "${!PORTA[@]}"; do curl -sf -X POST "localhost:${PORTA[$papel]}/memoria/zerar" >/dev/null; done
}

memoria() {
  printf '{'
  local primeiro=1
  for papel in api-nova api-atual verificador; do
    [[ $primeiro == 1 ]] || printf ','
    primeiro=0
    printf '"%s":%s' "$papel" "$(curl -sf "localhost:${PORTA[$papel]}/memoria")"
  done
  printf ',"clamav":"%s"}' "$(docker stats --no-stream --format '{{.MemUsage}}' "$(dc ps -q clamav)")"
}

# Cada cenário grava até ~1,3 GB; esvaziar os buckets entre um e outro mantém o disco do host livre.
limpar_buckets() {
  for bucket in uniplus-envios uniplus-arquivos-candidato uniplus-envios-atual uniplus-selados-atual; do
    mc_admin "rm --recursive --force local/$bucket" >/dev/null 2>&1 || true
  done
  curl -sf -X DELETE localhost:18082/resultados >/dev/null
}

cenario() {
  local modo=$1 tamanho=$2 quantidade=$3 concorrencia=$4 rotulo=$5
  zerar_memoria
  local resumo
  # Falha de envio não interrompe a rodada: o resumo conta e classifica as falhas.
  resumo=$("${CARGA[@]}" --modo "$modo" --tamanho-mb "$tamanho" --quantidade "$quantidade" --concorrencia "$concorrencia" --rotulo "$rotulo" || true)
  python3 -c 'import json,sys;r=json.loads(sys.argv[1]);r["memoria"]=json.loads(sys.argv[2]);print(json.dumps(r,ensure_ascii=False))' \
    "$resumo" "$(memoria)" | tee -a "$SAIDA"
  limpar_buckets
}

dotnet publish "$POC_DIR/src/Unifesspa.UniPlus.Spikes.CifraNoMinio" -c Release -o "$POC_DIR/src/publicado" --nologo -v q
dc up -d --build verificador api-nova api-atual >/dev/null 2>&1
until curl -sf localhost:18082/resultados >/dev/null && curl -sf localhost:18081/memoria >/dev/null && curl -sf localhost:18080/memoria >/dev/null; do sleep 1; done

echo ">> Aquecimento (JIT, conexões, assinaturas do clamd em memória)"
"${CARGA[@]}" --modo nova --tamanho-mb 1 --quantidade 10 --concorrencia 2 >/dev/null || true
"${CARGA[@]}" --modo atual --tamanho-mb 1 --quantidade 10 --concorrencia 2 >/dev/null || true
limpar_buckets

secao_por_arquivo() {
  echo ">> Tempo por arquivo: um envio por vez, 20 arquivos por tamanho"
  for modo in nova atual; do
    for tamanho in 1 5 10; do
      cenario "$modo" "$tamanho" 20 1 "por-arquivo"
    done
  done
}

secao_vazao() {
  echo ">> Vazão: N envios simultâneos de 5 MB"
  for modo in nova atual; do
    for n in 1 8 32 64; do
      quantidade=$((n * 4 > 40 ? n * 4 : 40))
      cenario "$modo" 5 "$quantidade" "$n" "vazao"
    done
  done
}

secao_memoria() {
  echo ">> Pico de memória: 64 envios simultâneos de 10 MB"
  for modo in nova atual; do
    cenario "$modo" 10 64 64 "memoria-10mb"
  done
}

secao_sem_fila() {
  echo ">> Fluxo novo com o webhook sem fila em disco"
  FILA_DO_WEBHOOK= dc up -d minio >/dev/null 2>&1
  until dc exec -T minio curl -sf http://localhost:9000/minio/health/live >/dev/null 2>&1; do sleep 1; done
  "${CARGA[@]}" --modo nova --tamanho-mb 1 --quantidade 5 --concorrencia 1 >/dev/null || true
  cenario nova 5 20 1 "sem-fila-em-disco"
  cenario nova 5 128 32 "sem-fila-em-disco"
  dc up -d minio >/dev/null 2>&1
  until dc exec -T minio curl -sf http://localhost:9000/minio/health/live >/dev/null 2>&1; do sleep 1; done
}

secao_paralelismo() {
  echo ">> Verificador com paralelismo 32 (a mesma concorrência que o fluxo atual alcança)"
  PARALELISMO_VERIFICADOR=32 dc up -d verificador >/dev/null 2>&1
  until curl -sf localhost:18082/resultados >/dev/null; do sleep 1; done
  cenario nova 5 256 64 "paralelismo-32"
  cenario nova 10 64 64 "paralelismo-32"
  dc up -d verificador >/dev/null 2>&1
  until curl -sf localhost:18082/resultados >/dev/null; do sleep 1; done
}

for secao in "${SECOES[@]}"; do
  "secao_${secao//-/_}"
done

python3 - "$SAIDA" >"$RESULTADOS/medicoes.md" <<'EOF'
import json, sys
linhas = [json.loads(l) for l in open(sys.argv[1])]
print("| cenário | fluxo | MB | N | arquivos | falhas | arq/s | MB/s | até o veredicto p50 / p95 (ms) | notificação p50 (ms) | fila p95 (ms) | verificação p50 / p95 (ms) | pico RSS API (MiB) | pico RSS verificador (MiB) |")
print("|---|---|---:|---:|---:|---:|---:|---:|---|---:|---:|---|---:|---:|")
for r in linhas:
    m = r["memoria"]
    api = m["api-nova"]["picoRssMiB"] if r["modo"] == "nova" else m["api-atual"]["picoRssMiB"]
    ver = m["verificador"]["picoRssMiB"] if r["modo"] == "nova" else "—"
    av, vf, fl, nt = r["ateVeredictoMs"], r["verificacaoMs"], r["filaMs"], r["notificacaoMs"]
    nova = r["modo"] == "nova"
    falhas = r["falhas"] if not r.get("erros") else f'{r["falhas"]} ({", ".join(f"{k}: {v}" for k, v in r["erros"].items())})'
    print(f'| {r["rotulo"]} | {r["modo"]} | {r["tamanhoMb"]} | {r["concorrencia"]} | {r["quantidade"]} | {falhas} | '
          f'{r["arquivosPorS"]} | {r["mbPorS"]} | {av.get("p50")} / {av.get("p95")} | {nt.get("p50") if nova else "—"} | '
          f'{fl.get("p95") if nova else "—"} | {vf.get("p50")} / {vf.get("p95")} | {api} | {ver} |')
EOF
cat "$RESULTADOS/medicoes.md"
