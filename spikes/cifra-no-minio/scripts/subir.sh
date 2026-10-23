#!/usr/bin/env bash
# Sobe a POC do zero: gera as identidades do KES, inicia e configura o Vault (KV v2 + Transit +
# AppRole), sobe KES, MinIO, clamd e os três papéis da aplicação, e prepara buckets, usuários,
# cifra padrão e notificação. Idempotente: se .estado/ já existe, só sobe o que faltar.
source "$(dirname "$0")/comum.sh"

mkdir -p "$ESTADO/kes" "$RESULTADOS/registro"

if [[ ! -f "$ESTADO/env" ]]; then
  echo ">> Gerando identidades do KES e segredos locais"
  docker run --rm -u "$(id -u):$(id -g)" -v "$ESTADO/kes:/out" "$KES_IMAGEM" \
    identity new --key /out/kes.key --cert /out/kes.crt --dns kes --dns localhost --expiry 8760h kes >/dev/null
  chave_minio=$(docker run --rm "$KES_IMAGEM" identity new | grep -o -m1 'kes:v1:[A-Za-z0-9+/=]*' | head -1)
  chave_sem_permissao=$(docker run --rm "$KES_IMAGEM" identity new | grep -o -m1 'kes:v1:[A-Za-z0-9+/=]*' | head -1)
  cat >"$ESTADO/env" <<EOF
MINIO_ROOT_USER=poc-admin
MINIO_ROOT_PASSWORD=$(openssl rand -hex 16)
KES_API_KEY_MINIO=$chave_minio
KES_IDENTIDADE_MINIO=$(docker run --rm "$KES_IMAGEM" identity of "$chave_minio" | tail -1 | tr -d ' ')
KES_API_KEY_SEM_PERMISSAO=$chave_sem_permissao
TOKEN_WEBHOOK=$(openssl rand -hex 16)
SENHA_API=$(openssl rand -hex 16)
SENHA_VERIFICADOR=$(openssl rand -hex 16)
CHAVE_AES_LOCAL=$(openssl rand -base64 32)
EOF
fi
# Segredos do fluxo com fila, acrescentados a um .estado/ anterior a ele.
for segredo in SENHA_POSTGRES SENHA_RABBITMQ; do
  grep -q "^$segredo=" "$ESTADO/env" || echo "$segredo=$(openssl rand -hex 16)" >>"$ESTADO/env"
done
carregar_env

echo ">> Vault"
dc up -d vault
# "vault status" sai com 2 enquanto selado; o estado vem do JSON, não do código de saída.
estado_vault() { dc exec -T vault vault status -format=json 2>/dev/null || true; }
until estado_vault | grep -q '"initialized"'; do sleep 1; done
if ! estado_vault | grep -q '"initialized": true'; then
  dc exec -T vault vault operator init -key-shares=1 -key-threshold=1 -format=json >"$ESTADO/vault-init.json"
  python3 -c 'import json,sys;d=json.load(open(sys.argv[1]));open(sys.argv[2],"w").write(d["root_token"]);open(sys.argv[3],"w").write(d["unseal_keys_b64"][0])' \
    "$ESTADO/vault-init.json" "$ESTADO/vault-root-token" "$ESTADO/vault-unseal-key"
fi
if estado_vault | grep -q '"sealed": true'; then
  dc exec -T vault vault operator unseal "$(cat "$ESTADO/vault-unseal-key")" >/dev/null
fi

if ! vault_cmd secrets list -format=json | grep -q '"transit/"'; then
  echo ">> Configurando KV v2, Transit e AppRole do KES"
  vault_cmd secrets enable -path=kv -version=2 kv >/dev/null
  vault_cmd secrets enable transit >/dev/null
  vault_cmd write -f transit/keys/uniplus-kes-kek type=aes256-gcm96 >/dev/null
  vault_cmd policy write kes /poc/politica-kes.hcl >/dev/null
  vault_cmd auth enable approle >/dev/null
  vault_cmd write auth/approle/role/kes token_policies=kes token_ttl=1h token_max_ttl=24h secret_id_ttl=0 >/dev/null
  {
    echo "KES_VAULT_ROLE_ID=$(vault_cmd read -field=role_id auth/approle/role/kes/role-id)"
    echo "KES_VAULT_SECRET_ID=$(vault_cmd write -f -field=secret_id auth/approle/role/kes/secret-id)"
  } >>"$ESTADO/env"
  carregar_env
fi

echo ">> KES, MinIO e clamd"
dc up -d kes minio clamav
until dc exec -T minio curl -sf http://localhost:9000/minio/health/live >/dev/null 2>&1; do sleep 1; done

echo ">> Chave, buckets, cifra padrão, usuários e notificação"
mc_admin "admin kms key create local uniplus-arquivos-candidato" >/dev/null 2>&1 || true
for bucket in uniplus-envios uniplus-arquivos-candidato; do
  mc_admin "mb --ignore-existing local/$bucket" >/dev/null
  mc_admin "encrypt set sse-kms uniplus-arquivos-candidato local/$bucket" >/dev/null
done
# Controle das provas (sem cifra) e os buckets do fluxo atual, reproduzido para comparação: envio
# em claro e cópia selada cifrada pela aplicação, sem SSE-KMS e sem notificação.
mc_admin "mb --ignore-existing local/uniplus-controle-sem-cifra local/uniplus-envios-atual local/uniplus-selados-atual" >/dev/null
# Objetos de envio que ninguém verificou não ficam para sempre.
mc_admin "ilm rule add --expire-days 1 local/uniplus-envios" >/dev/null 2>&1 || true

dc cp "$POC_DIR/config/politica-api.json" minio:/tmp/politica-api.json
dc cp "$POC_DIR/config/politica-verificador.json" minio:/tmp/politica-verificador.json
dc cp "$POC_DIR/config/politica-api-etiqueta.json" minio:/tmp/politica-api-etiqueta.json
mc_admin "admin policy create local poc-api /tmp/politica-api.json" >/dev/null
mc_admin "admin policy create local poc-verificador /tmp/politica-verificador.json" >/dev/null
mc_admin "admin policy create local poc-api-etiqueta /tmp/politica-api-etiqueta.json" >/dev/null
mc_admin "admin user add local api $SENHA_API" >/dev/null
mc_admin "admin user add local verificador $SENHA_VERIFICADOR" >/dev/null
# A credencial da API na alternativa sem cópia: lê do bucket de envio só o que tem a etiqueta de aprovado.
mc_admin "admin user add local api-etiqueta $SENHA_API" >/dev/null
mc_admin "admin policy attach local poc-api --user api" >/dev/null 2>&1 || true
mc_admin "admin policy attach local poc-verificador --user verificador" >/dev/null 2>&1 || true
mc_admin "admin policy attach local poc-api-etiqueta --user api-etiqueta" >/dev/null 2>&1 || true

echo ">> Aplicação (verificador, api-nova, api-atual)"
dotnet publish "$POC_DIR/src/Unifesspa.UniPlus.Spikes.CifraNoMinio" -c Release -o "$POC_DIR/src/publicado" --nologo -v q
dc up -d --build verificador api-nova api-atual
mc_admin "event add --ignore-existing local/uniplus-envios arn:minio:sqs::VERIFICADOR:webhook --event put" >/dev/null

echo ">> Fluxo com fila: Postgres, RabbitMQ, API e verificador (o transporte vem de TRANSPORTE)"
dc --profile fila up -d --build postgres rabbitmq api-fila verificador-fila-1

echo ">> Aguardando as assinaturas do clamd (a primeira subida baixa a base, alguns minutos)"
until [[ "$(docker inspect -f '{{.State.Health.Status}}' "$(dc ps -q clamav)")" == "healthy" ]]; do sleep 5; done
echo ">> POC no ar"
