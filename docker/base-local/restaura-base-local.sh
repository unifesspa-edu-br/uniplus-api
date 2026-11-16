#!/usr/bin/env bash
# Roda no primeiro boot do Postgres (volume vazio), depois do init.sql: restaura a base local pronta.
#   dumps/uniplus.dump      banco do monólito, com perfis, concessões, processos e inscrições de exemplo
#   dumps/keycloak.dump     banco do Keycloak: personas e contas técnicas (os ids que as concessões referenciam)
#   geo/geo-api-database.tar.xz  banco do Geo com a base de CEP (dump em diretório, comprimido com xz para caber no GitHub)
# O Geo é restaurado com os donos: a API do Geo conecta como uniplus_geo_app (papel criado pelo init-db.sql), dono dos objetos.
# Só dados de desenvolvimento; os segredos são os de desenvolvimento do .env.example.
set -euo pipefail
usuario="${POSTGRES_USER:-uniplus}"
restaurar() { # $1=banco $2=dump (arquivo ou diretório) $3...=opções extras do pg_restore
    local banco="$1" dump="$2"; shift 2
    echo "[base-local] restaurando $banco"
    pg_restore -U "$usuario" -d "$banco" --clean --if-exists --exit-on-error "$@" "$dump"
}
restaurar keycloak /base-local/dumps/keycloak.dump --no-privileges --no-owner
restaurar uniplus /base-local/dumps/uniplus.dump --no-privileges --no-owner
tar xJf /base-local/geo/geo-api-database.tar.xz -C /tmp
restaurar uniplus_geo /tmp/geo-api-database
rm -rf /tmp/geo-api-database
echo "[base-local] base restaurada"
