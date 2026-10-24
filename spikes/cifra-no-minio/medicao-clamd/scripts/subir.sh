#!/usr/bin/env bash
# Sobe os clamd da medição, cada um com 4 CPUs e a base de assinaturas copiada do volume da POC
# (rodar antes ./scripts/subir.sh da POC, que baixa a base). Recria os contêineres e os volumes que já existirem.
# Portas: hml-atual 13320, aprovada 13321, final 13322, final-alerta 13323, final-limites 13324,
# adotada 13325.
set -euo pipefail
cd "$(dirname "$0")/.."
IMG="clamav/clamav:1.4@sha256:3bba6dd5ac6663d55b80f86a3126e146612d93b593cc2617188d13f637d91260"
NOMES=(hml-atual aprovada final final-alerta final-limites adotada)
for i in "${!NOMES[@]}"; do
  nome="${NOMES[$i]}"
  porta=$((13320 + i))
  docker rm -f "medicao-clamd-$nome" >/dev/null 2>&1 || true
  docker volume rm -f "medicao-clamd-db-$nome" >/dev/null
  docker volume create "medicao-clamd-db-$nome" >/dev/null
  docker run --rm -v uniplus-poc-sse-kms_clamav_dados:/origem:ro -v "medicao-clamd-db-$nome":/destino \
    -v "$PWD/config/pua-ignoradas.ign2":/ignoradas.ign2:ro --entrypoint sh "$IMG" -c \
    "cp -a /origem/. /destino/ && if [ $nome = adotada ]; then cp /ignoradas.ign2 /destino/pua-ignoradas.ign2; fi"
  docker run -d --name "medicao-clamd-$nome" --cpus 4 -e CLAMAV_NO_FRESHCLAMD=true \
    -v "medicao-clamd-db-$nome":/var/lib/clamav -v "$PWD/config/$nome.conf":/etc/clamav/clamd.conf:ro \
    -p "127.0.0.1:$porta:3310" "$IMG" >/dev/null
done
for i in "${!NOMES[@]}"; do
  porta=$((13320 + i))
  # o clamd carrega a base em segundos; dois minutos sem PING é configuração inválida ou base ausente
  for _ in $(seq 1 120); do
    [ "$(printf 'zPING\0' | timeout 2 nc -q1 127.0.0.1 "$porta" 2>/dev/null | tr -d '\0')" = PONG ] && continue 2
    sleep 1
  done
  echo "clamd ${NOMES[$i]} não respondeu na porta $porta; veja docker logs medicao-clamd-${NOMES[$i]}" >&2
  exit 1
done
echo "clamd no ar"
