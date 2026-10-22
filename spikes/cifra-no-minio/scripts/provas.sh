#!/usr/bin/env bash
# Roda as provas da POC sobre a stack de scripts/subir.sh e grava comando e saída em
# resultados/provas.txt. Cada prova imprime o comando antes da saída.
source "$(dirname "$0")/comum.sh"
carregar_env

TMP="$RESULTADOS/tmp"
mkdir -p "$TMP"
exec > >(sed "s#$POC_DIR#.#g" | tee "$RESULTADOS/provas.txt") 2>&1

VOLUME="uniplus-poc-sse-kms_minio_dados"
CIFRADO="uniplus-arquivos-candidato"

titulo() { printf '\n\n==================== %s ====================\n' "$*"; }

# Executa um comando que deve falhar e mostra o código de saída em vez de abortar o roteiro.
mostrar_falha() {
  printf '\n$ %s\n' "$(mascarar "$@")"
  set +e
  "$@"
  printf '(código de saída: %s)\n' "$?"
  set -e
}

no_volume() { docker run --rm -v "$VOLUME:/data:ro" alpine:3.22 sh -c "$1"; }

# A URL de envio vem da api-nova, como viria da API: POST /envios devolve chave e URL assinada.
assinar_envio() {
  local resposta
  resposta=$(curl -sf -X POST "localhost:18080/envios?fluxo=${1:-nova}")
  CHAVE=$(python3 -c 'import json,sys;print(json.loads(sys.argv[1])["chave"])' "$resposta")
  URL=$(python3 -c 'import json,sys;print(json.loads(sys.argv[1])["url"])' "$resposta")
}

url_de_leitura() {
  curl -sf "localhost:18080/arquivos/$1/url?$2" | python3 -c 'import json,sys;print(json.load(sys.stdin)["url"])'
}

esperar_veredicto() {
  timeout 60 bash -c "until curl -sf localhost:18082/resultados/$1; do sleep 0.3; done"
  echo
}

# Conteúdo reconhecível: se aparecer no disco, a busca acha.
python3 -c 'import sys;sys.stdout.write("UNIPLUS-POC-MARCADOR-EM-CLARO;" * 8000)' >"$TMP/marcador.pdf"
SHA_MARCADOR=$(sha256sum "$TMP/marcador.pdf" | cut -d' ' -f1)

titulo "Ambiente"
mostrar docker compose version
mostrar dc exec -T vault vault version
mostrar docker run --rm "$KES_IMAGEM" --version
mostrar dc exec -T minio minio --version
dc ps --format '{{.Service}}  {{.Image}}'

titulo "1. URL pré-assinada de PUT continua funcionando com SSE-KMS no bucket"
mostrar mc_admin "encrypt info local/uniplus-envios"
mostrar mc_admin "encrypt info local/$CIFRADO"
assinar_envio
echo "URL de envio assinada pela api-nova (sem nenhum cabeçalho de SSE): ${URL%%\?*}?X-Amz-Algorithm=...&X-Amz-SignedHeaders=host&..."
mostrar curl -s -o /dev/null -w 'HTTP %{http_code}\n' -T "$TMP/marcador.pdf" -H 'Content-Type: application/pdf' "$URL"
CHAVE_A=$CHAVE
mostrar esperar_veredicto "$CHAVE_A"
mostrar mc_admin "stat local/$CIFRADO/$CHAVE_A"

assinar_envio
curl -s -o /dev/null -T "$TMP/marcador.pdf" -H 'Content-Type: application/pdf' "$URL"
CHAVE_B=$CHAVE
esperar_veredicto "$CHAVE_B" >/dev/null
# Controle: o mesmo conteúdo num bucket sem cifra.
dc cp "$TMP/marcador.pdf" minio:/tmp/marcador.pdf >/dev/null 2>&1
mc_admin "cp /tmp/marcador.pdf local/uniplus-controle-sem-cifra/marcador.pdf" >/dev/null

titulo "2. O objeto fica cifrado em disco (leitura direta do volume)"
echo "Mesmo conteúdo ($SHA_MARCADOR) gravado em três lugares:"
echo "  $CIFRADO/$CHAVE_A e $CIFRADO/$CHAVE_B (SSE-KMS) e uniplus-controle-sem-cifra/marcador.pdf (sem cifra)"
# A contagem roda no host (GNU grep -a -o conta também dentro de arquivo binário).
contar_marcador() { grep -a -o 'UNIPLUS-POC-MARCADOR-EM-CLARO' | wc -l; }
for arquivo in $(no_volume "find /data/uniplus-controle-sem-cifra/marcador.pdf /data/$CIFRADO/$CHAVE_A /data/$CIFRADO/$CHAVE_B -type f"); do
  printf '\n$ no_volume "cat %s" | grep -a -o MARCADOR | wc -l\n%s ocorrências\n' "$arquivo" "$(no_volume "cat $arquivo" | contar_marcador)"
done
mostrar no_volume "find /data/$CIFRADO/$CHAVE_A -type f -exec ls -l {} \;"
mostrar no_volume "head -c 96 \$(find /data/$CIFRADO/$CHAVE_A -name 'part.*') | od -A d -c | head -6"
mostrar no_volume "head -c 96 \$(find /data/uniplus-controle-sem-cifra/marcador.pdf -name 'part.*') | od -A d -c | head -6"
echo
echo "Mesmo conteúdo em claro, texto cifrado diferente em disco:"
mostrar no_volume "sha256sum /data/$CIFRADO/$CHAVE_A/*/part.1 /data/$CIFRADO/$CHAVE_B/*/part.1"

titulo "3. Chave de dados distinta por objeto"
echo "Cada objeto guarda a própria chave de dados selada pela chave do KES (Kms-Sealed-Key):"
for chave in "$CHAVE_A" "$CHAVE_B"; do
  mostrar no_volume "strings -n 6 /data/$CIFRADO/$chave/xl.meta | grep -A1 -E 'Kms-Sealed-Key|Kms-Key-Id|Encryption-Iv'"
done

titulo "4. Quem tem o volume ou o backup bruto não lê o conteúdo"
no_volume "tar -C /data -cf - $CIFRADO" >"$TMP/backup-cifrado.tar"
no_volume "tar -C /data -cf - uniplus-controle-sem-cifra" >"$TMP/backup-controle.tar"
mostrar ls -l "$TMP/backup-cifrado.tar" "$TMP/backup-controle.tar"
printf '\n$ grep -a -o MARCADOR backup-cifrado.tar | wc -l\n%s ocorrências\n' "$(contar_marcador <"$TMP/backup-cifrado.tar")"
printf '\n$ grep -a -o MARCADOR backup-controle.tar | wc -l\n%s ocorrências\n' "$(contar_marcador <"$TMP/backup-controle.tar")"
echo
echo "Nem o backup do Vault entrega a chave do KES: no KV ela está cifrada pela chave Transit."
mostrar vault_cmd kv get -format=json kv/kes/uniplus-arquivos-candidato
mostrar vault_cmd read -format=json transit/keys/uniplus-kes-kek

titulo "5. URL pré-assinada de GET devolve o conteúdo em claro"
URL_GET=$(url_de_leitura "$CHAVE_A" "")
mostrar curl -s -o "$TMP/baixado.pdf" -w 'HTTP %{http_code}  %{content_type}  %{size_download} bytes\n' "$URL_GET"
mostrar sha256sum "$TMP/marcador.pdf" "$TMP/baixado.pdf"
mostrar head -c 60 "$TMP/baixado.pdf"
echo

titulo "6. Range e Content-Disposition na URL de GET"
mostrar curl -s -D - -o "$TMP/faixa.bin" -r 30-89 "$URL_GET" -H 'Accept: */*'
mostrar cmp "$TMP/faixa.bin" <(tail -c +31 "$TMP/marcador.pdf" | head -c 60)
echo "(cmp sem saída: a faixa devolvida é a do arquivo em claro)"
mostrar curl -s -D - -o /dev/null -r 239990-239999 "$URL_GET"
URL_ANEXO=$(url_de_leitura "$CHAVE_A" "disposicao=attachment&nome=laudo-medico.pdf")
mostrar curl -s -D - -o /dev/null "$URL_ANEXO"
URL_INLINE=$(url_de_leitura "$CHAVE_A" "disposicao=inline")
mostrar curl -s -D - -o /dev/null "$URL_INLINE"
echo "Adulterar a disposição na URL quebra a assinatura:"
mostrar curl -s -o /dev/null -w 'HTTP %{http_code}\n' "${URL_ANEXO/laudo-medico/outro-nome}"

titulo "7. Quem tem credencial do MinIO sem permissão no KES não decifra"
echo "7a. Credencial de administrador do MinIO, mesmo volume, identidade do KES fora da política:"
dc stop minio >/dev/null 2>&1
dc --profile provas up -d minio-sem-permissao-no-kes >/dev/null 2>&1
timeout 90 bash -c "until dc --profile provas exec -T minio-sem-permissao-no-kes curl -sf http://localhost:9000/minio/health/live; do sleep 2; done" || true
SEM_KES='mc alias set atacante http://localhost:9000 "$MINIO_ROOT_USER" "$MINIO_ROOT_PASSWORD" >/dev/null &&'
mostrar_falha dc --profile provas exec -T minio-sem-permissao-no-kes sh -c "$SEM_KES mc cat atacante/$CIFRADO/$CHAVE_A"
mostrar dc --profile provas exec -T minio-sem-permissao-no-kes sh -c "$SEM_KES mc cat atacante/uniplus-controle-sem-cifra/marcador.pdf | head -c 60"
echo
dc --profile provas rm -sf minio-sem-permissao-no-kes >/dev/null 2>&1
dc start minio >/dev/null 2>&1
until dc exec -T minio curl -sf http://localhost:9000/minio/health/live >/dev/null 2>&1; do sleep 1; done

echo
echo "7b. Credencial da API (só assina): não lê o bucket de envio."
# A senha vai por variável de ambiente, para não aparecer no comando impresso.
mostrar_falha dc exec -T -e SENHA_API="$SENHA_API" minio sh -c 'mc alias set api http://localhost:9000 api "$SENHA_API" >/dev/null && mc cat api/uniplus-envios/qualquer'
echo
echo "7c. Vault selado (o KES perde a fonte da chave): o MinIO não decifra nem cifra. Falha fechada."
mostrar vault_cmd operator seal
echo "(aguardando 35 s: o cache do KES expira em 30 s)"
sleep 35
mostrar_falha mc_admin "cat local/$CIFRADO/$CHAVE_A"
assinar_envio
mostrar curl -s -w '\nHTTP %{http_code}\n' -T "$TMP/marcador.pdf" "$URL"
mostrar dc exec -T -e CHAVE="$(cat "$ESTADO/vault-unseal-key")" vault sh -c 'vault operator unseal "$CHAVE"'
timeout 60 bash -c "until mc_admin 'cat local/$CIFRADO/$CHAVE_A' >/dev/null 2>&1; do sleep 2; done"
mostrar mc_admin "cat local/$CIFRADO/$CHAVE_A | head -c 40"
echo

titulo "8. Verificador: arquivo infectado (EICAR) é removido e o registro não traz conteúdo"
# A assinatura de teste EICAR é montada em tempo de execução para o repositório não carregá-la.
printf '%s%s' 'X5O!P%@AP[4\PZX54(P^)7CC)7}$EICAR-STANDARD' '-ANTIVIRUS-TEST-FILE!$H+H*' >"$TMP/eicar.pdf"
assinar_envio
mostrar curl -s -o /dev/null -w 'HTTP %{http_code}\n' -T "$TMP/eicar.pdf" -H 'Content-Type: application/pdf' "$URL"
mostrar esperar_veredicto "$CHAVE"
mostrar_falha mc_admin "stat local/uniplus-envios/$CHAVE"
mostrar_falha mc_admin "stat local/$CIFRADO/$CHAVE"
mostrar grep "$CHAVE" "$RESULTADOS/registro/verificacoes.jsonl"
mostrar bash -c "dc logs --no-log-prefix verificador 2>&1 | grep '$CHAVE'"

titulo "9. A API não lê o conteúdo em nenhum momento"
echo "9a. O código da api-nova não tem nenhuma chamada de leitura de objeto:"
mostrar_falha grep -nE '\.(Get|Stat|Copy|Put|Remove)ObjectAsync|ListObjects' "$POC_DIR/src/Unifesspa.UniPlus.Spikes.CifraNoMinio/ApiNova.cs"
echo "(nenhuma ocorrência; o código 1 do grep é \"nada encontrado\". Só há PresignedPut/PresignedGet, que não fazem requisição.)"
echo
echo "9b. Trace do MinIO durante 5 envios de 10 MB: quem chamou o quê, e quantos bytes cada um recebeu."
declare -A NOMES
for servico in api-nova verificador api-atual; do
  NOMES[$(docker inspect -f '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}' "$(dc ps -q "$servico")")]=$servico
done
NOMES[172.31.250.1]="candidato (host)"
rede_antes=$(docker stats --no-stream --format '{{.NetIO}}' "$(dc ps -q api-nova)")
dc exec -T minio sh -c 'mc alias set local http://localhost:9000 "$MINIO_ROOT_USER" "$MINIO_ROOT_PASSWORD" >/dev/null; timeout 25 mc admin trace --json local' >"$TMP/trace.json" 2>/dev/null &
sleep 2
head -c $((10 * 1024 * 1024)) /dev/urandom >"$TMP/dez-mb.pdf"
CHAVES=()
for _ in 1 2 3 4 5; do
  assinar_envio
  curl -s -o /dev/null -T "$TMP/dez-mb.pdf" -H 'Content-Type: application/pdf' "$URL"
  CHAVES+=("$CHAVE")
done
for chave in "${CHAVES[@]}"; do esperar_veredicto "$chave" >/dev/null; done
wait
rede_depois=$(docker stats --no-stream --format '{{.NetIO}}' "$(dc ps -q api-nova)")
python3 - "$TMP/trace.json" "$(for ip in "${!NOMES[@]}"; do printf '%s=%s;' "$ip" "${NOMES[$ip]}"; done)" <<'EOF'
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
print(f"{'origem':<18} {'operação':<16} {'chamadas':>8} {'bytes enviados ao MinIO':>24} {'bytes lidos do MinIO':>21}")
for (origem, api), (n, rx, tx) in sorted(soma.items()):
    print(f"{origem:<18} {api:<16} {n:>8} {rx:>24} {tx:>21}")
print("\nNenhuma linha com origem api-nova: assinar não faz requisição ao MinIO.")
EOF
echo
echo "9c. Tráfego de rede do contêiner da api-nova (recebido / enviado), antes e depois dos 50 MB:"
echo "    antes: $rede_antes    depois: $rede_depois"

titulo "10. Auditoria: o log de auditoria do MinIO registra quem leu qual objeto"
echo "Cada requisição ao MinIO vai, por audit webhook, ao coletor (aqui, /auditoria do verificador)."
URL_GET=$(url_de_leitura "$CHAVE_A" "")
curl -s -o /dev/null "$URL_GET"
sleep 2
mostrar python3 - "$RESULTADOS/registro/auditoria.jsonl" "$CHAVE_A" <<'EOF2'
import json, sys
for linha in open(sys.argv[1]):
    a = json.loads(linha)
    if sys.argv[2] in a.get("api", {}).get("object", ""):
        api = a["api"]
        print(a["time"], api["name"], api["bucket"], api["object"], "status", api.get("statusCode"),
              "accessKey", a.get("accessKey") or a.get("requestClaims", {}).get("accessKey"), "origem", a.get("remotehost"))
EOF2
echo
echo "O KES 2025-03-12 não emitiu evento de auditoria (nem no stdout, nem por 'kes log --audit');"
echo "o log de erros dele traz a identidade de quem pediu a chave:"
mostrar bash -c "dc logs --no-log-prefix kes 2>&1 | grep -m2 'req.identity'"
