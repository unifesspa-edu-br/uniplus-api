#!/usr/bin/env bash
# Roda no primeiro boot do Postgres (volume vazio), depois do init.sql: restaura a base local pronta.
#   dumps/uniplus.dump      banco do monólito, com perfis, concessões, processos e inscrições de exemplo
#   dumps/keycloak.dump     banco do Keycloak: personas e contas técnicas (os ids que as concessões referenciam)
#   geo/geo-api-database.tar.xz  banco do Geo com a base de CEP (dump em diretório, comprimido com xz para caber no GitHub)
# O Geo é restaurado com os donos: a API do Geo conecta como uniplus_geo_app (papel criado pelo init-db.sql), dono dos objetos.
# Só dados de desenvolvimento; os segredos são os de desenvolvimento do .env.example.
set -euo pipefail
usuario="${POSTGRES_USER:-uniplus}"

# Os dumps gravam valores do ambiente padrão: o admin do Keycloak (admin/admin), o emissor das concessões
# (localhost:8080), as URLs dos documentos no MinIO (localhost:9000) e as origens dos clients web no Keycloak
# (localhost:4200 a 4203). Com outros valores no docker/.env o admin seria sobrescrito e essas URLs apontariam para
# endereços inexistentes; por isso a restauração só segue com os valores padrão do .env.example.
valores_exigidos=(
    "KEYCLOAK_ADMIN=admin" "KEYCLOAK_ADMIN_PASSWORD=admin" "KEYCLOAK_PORT=8080" "MINIO_PORT=9000"
    "SELECAO_WEB_PORT=4200" "INGRESSO_WEB_PORT=4201" "PORTAL_WEB_PORT=4202" "CONFIGURACAO_WEB_PORT=4203"
)
divergentes=()
for exigido in "${valores_exigidos[@]}"; do
    nome="${exigido%%=*}"
    [ "${!nome:-${exigido#*=}}" = "${exigido#*=}" ] || divergentes+=("$nome")
done
if [ "${#divergentes[@]}" -gt 0 ]; then
    echo "[base-local] ERRO: a base pronta exige os valores padrão do docker/.env.example, e estes estão diferentes: ${divergentes[*]}." >&2
    echo "[base-local] Use os valores padrão ou, para subir sem a base pronta, remova do serviço postgres do docker-compose.yml as três linhas de volume em ./base-local. Depois rode 'docker compose down -v' e suba de novo." >&2
    exit 1
fi

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
