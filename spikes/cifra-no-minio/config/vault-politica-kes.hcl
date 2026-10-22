# O KES guarda as chaves mestras no KV v2 (sob kv/kes/) e cada uma vai cifrada pela chave Transit
# uniplus-kes-kek, que nunca sai do Vault. Nada além disso: o KES não lê outros segredos.
path "kv/data/kes/*" {
  capabilities = ["create", "read", "delete"]
}

path "kv/metadata/kes/*" {
  capabilities = ["list", "delete"]
}

path "kv/metadata/kes" {
  capabilities = ["list"]
}

path "transit/encrypt/uniplus-kes-kek" {
  capabilities = ["update"]
}

path "transit/decrypt/uniplus-kes-kek" {
  capabilities = ["update"]
}
