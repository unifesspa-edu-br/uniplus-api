# Vault da POC: armazenamento em arquivo (sobrevive a reinício, ao contrário do modo -dev) e sem
# TLS na rede interna do compose. Em HML o Vault já existe (1.21.2) e não é tocado por esta POC.
storage "file" {
  path = "/vault/file"
}

listener "tcp" {
  address     = "0.0.0.0:8200"
  tls_disable = true
}

api_addr      = "http://vault:8200"
disable_mlock = true
ui            = false
