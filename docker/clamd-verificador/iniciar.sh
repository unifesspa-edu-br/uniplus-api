#!/bin/sh
# Copia a base de partida e as assinaturas ignoradas para o volume do pod, tenta atualizar pelo
# espelho e inicia o clamd. Sem o espelho, o clamd sobe com a base da imagem.
set -eu

BASE=/var/lib/clamav
PARTIDA=/opt/clamav

# Sem sobrescrever: o reinício do sidecar não desfaz a atualização do freshclam.
cp -n "$PARTIDA"/base/*.c[lv]d "$BASE"/
cp -f "$PARTIDA"/pua-ignoradas.ign2 "$BASE"/

{
    cat /etc/clamav/freshclam.conf
    if [ -n "${CLAMAV_PRIVATE_MIRROR:-}" ]; then
        echo "PrivateMirror $CLAMAV_PRIVATE_MIRROR"
    else
        echo "DatabaseMirror database.clamav.net"
    fi
} > /tmp/freshclam.conf

# O clamd ainda não existe para ser avisado na atualização inicial.
grep -v '^NotifyClamd' /tmp/freshclam.conf > /tmp/freshclam-inicial.conf
if ! timeout "${CLAMAV_ATUALIZACAO_INICIAL_SEGUNDOS:-120}" freshclam --config-file=/tmp/freshclam-inicial.conf --stdout; then
    echo "Atualização inicial da base indisponível: o clamd sobe com a base da imagem."
fi

freshclam --config-file=/tmp/freshclam.conf --daemon --foreground --stdout &

exec clamd --config-file=/etc/clamav/clamd.conf
