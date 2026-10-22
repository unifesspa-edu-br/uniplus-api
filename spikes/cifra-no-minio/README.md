# POC: cifra no MinIO (SSE-KMS via KES e Vault) e verificação dos arquivos sem a API

Prova de conceito da issue #2045, base da [ADR-0146](../../docs/adrs/0146-cifra-e-verificacao-dos-arquivos-do-candidato-no-armazenamento.md). Roda só localmente, isolada do `docker/docker-compose.yml`, e não toca HML. Código descartável: não entra no `UniPlus.slnx`.

## O que a POC mostra

| Prova | Onde está a saída |
|---|---|
| A URL pré-assinada de PUT funciona com SSE-KMS no bucket, sem cabeçalho de SSE na URL | `resultados/provas.txt`, seção 1 |
| O objeto fica cifrado em disco; o mesmo conteúdo em claro vira texto cifrado diferente | seção 2 |
| Cada objeto tem chave de dados própria, selada pela chave do KES | seção 3 |
| O volume e o backup bruto não revelam o conteúdo; nem o KV do Vault revela a chave do KES | seção 4 |
| A URL pré-assinada de GET devolve o conteúdo em claro | seção 5 |
| `Range` (206) e `Content-Disposition` (inline e attachment) na URL de GET; adulterar a URL dá 403 | seção 6 |
| MinIO com credencial de administrador e identidade do KES sem permissão não decifra; Vault selado nega leitura e gravação | seção 7 |
| O EICAR é removido e o registro não traz conteúdo nem nome da ameaça | seção 8 |
| A API não lê o conteúdo: nenhum acesso dela no trace do MinIO, rede do contêiner parada | seção 9 |
| O log de auditoria do MinIO registra cada acesso ao objeto, com credencial e origem | seção 10 |
| Tempo por arquivo, vazão com N envios simultâneos e memória, fluxo novo × atual | `resultados/medicoes.md` e `resultados/medicoes.jsonl` |

## Componentes

```text
candidato ──PUT pré-assinado──▶ MinIO ◀──── KES ◀──── Vault (KV v2 + Transit)
     ▲                            │ webhook (fila em disco)
     │ URL                        ▼
  api-nova (só assina)      verificador ──INSTREAM──▶ clamd
                                 │ limpo: cópia no servidor para o bucket definitivo
                                 │ infectado: remove; registra sem conteúdo
```

- **Vault 1.21.2** (a versão de HML): KV v2 em `kv/`, Transit em `transit/` com a chave `uniplus-kes-kek`, AppRole `kes` com a política `config/vault-politica-kes.hcl`.
- **KES 2025-03-12T09-35-18Z**: guarda a chave `uniplus-arquivos-candidato` no KV, cifrada pela chave Transit. Só a identidade do MinIO consta da política (`config/kes.yml`).
- **MinIO RELEASE.2025-09-07** (a imagem do projeto): SSE-KMS como cifra padrão dos buckets `uniplus-envios` e `uniplus-arquivos-candidato`, notificação de PUT para o verificador e log de auditoria por webhook.
- **clamd 1.4** (a imagem do projeto).
- **Aplicação** (`src/`, um executável com quatro papéis): `verificador`, `api-nova` (assina URLs), `api-atual` (o fluxo de hoje, para comparação) e o gerador de carga. O verificador e a `api-atual` têm os limites do pod da API em HML: 2 CPUs e 1 GiB.

Credenciais do MinIO por papel (`config/politica-*.json`): a API só assina PUT no bucket de envio e GET no definitivo. Ela não tem leitura no bucket de envio. O verificador lê e remove no envio e grava no definitivo. O SDK .NET do MinIO faz `HEAD` do bucket antes de operar, o que exige `s3:ListBucket` nos dois buckets.

## Como reproduzir

Pré-requisitos: Docker com Compose v2 e o SDK .NET da banda 10.0.4xx (a do `global.json`). A rede do compose usa `172.31.250.0/24`, fora das rotas que a VPN instala em `tun0`.

```bash
cd spikes/cifra-no-minio
./scripts/subir.sh      # gera .estado/ (identidades, AppRole, segredos locais) e sobe tudo
./scripts/provas.sh     # grava resultados/provas.txt
./scripts/medir.sh      # grava resultados/medicoes.jsonl e resultados/medicoes.md
docker compose --env-file .estado/env --profile provas down -v && rm -rf .estado   # derruba e apaga
```

Na primeira subida, o clamd baixa a base de assinaturas, o que leva alguns minutos. Os segredos ficam só em `.estado/`, que está no `.gitignore`.

## Decisões de desenho da POC

- **KV com Transit, e não só KV.** O keystore Vault do KES guarda as chaves no KV, e o bloco `transit` faz o KES cifrar cada chave com uma chave Transit que nunca sai do Vault. Quem lê o KV, ou um backup dele, vê só `vault:v1:…` (prova 4). O Transit já é o que a API usa em HML.
- **Dois buckets.** O de envio recebe o PUT pré-assinado e é a quarentena. O definitivo só recebe o que o verificador promoveu. As políticas de acesso seguem essa divisão. O envio não verificado expira em 1 dia (regra de ciclo de vida).
- **Mesmo ETag na leitura e na cópia.** O verificador lê e promove com `If-Match` do ETag do evento. Se outro PUT trocar o objeto entre a verificação e a promoção, a cópia falha, e o evento do novo PUT traz a sua própria verificação.
- **Leitura em stream.** O verificador passa o objeto ao clamd em pedaços de 64 KiB e calcula o SHA-256 no caminho, sem ter o arquivo inteiro em memória.
- **Cache do KES curto** (30 s, sem cache offline), para a prova de falha fechada não esperar minutos. Em HML, esse valor é escolha de disponibilidade × revogação.

## O que não foi provado

- **Auditoria pelo KES.** Nesta versão, o KES não emitiu evento de auditoria, nem no stdout nem por `kes log --audit`. O log de erros dele traz a identidade em cada falha. A auditoria de acesso foi provada pelo audit webhook do MinIO (prova 10).
- **Quem usou a URL pré-assinada.** No log do MinIO, o GET pela URL aparece com a credencial de quem assinou (a API) e o IP de origem. A identidade do candidato não aparece ali. Ela precisa vir do registro de emissão da URL na API.
- **Volume cifrado.** A cifra do disco em si (LUKS ou a do provedor) é da infraestrutura e não entra na POC.
- **Autenticação do KES no Vault por Kubernetes.** O binário do KES aceita `kubernetes` no lugar de `approle`. A POC usou AppRole por não ter cluster.
- **Durabilidade do evento até o fim da verificação.** O webhook com fila em disco garante a entrega ao verificador, mas a fila do verificador é em memória: se ele cair depois de aceitar o evento, o arquivo fica no bucket de envio sem veredicto. O primeiro arquivo da POC caiu exatamente nesse caso (`AccessDenied` antes de ajustar a política). Em produção, isso exige reentrega ou varredura (ADR-0146).
