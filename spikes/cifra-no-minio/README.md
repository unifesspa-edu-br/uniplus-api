# POC: cifra no MinIO (SSE-KMS via KES e Vault) e verificação dos arquivos sem a API

Prova de conceito da issue #2045, base da [ADR-0146](../../docs/adrs/0146-cifra-e-verificacao-dos-arquivos-do-candidato-no-armazenamento.md). Roda só localmente, isolada do `docker/docker-compose.yml`, e não toca HML. Código descartável: não entra no `UniPlus.slnx`.

A POC tem duas partes. A primeira prova a cifra no armazenamento e o verificador acionado pelo webhook do bucket. A segunda troca o webhook por uma fila: a API publica o pedido de verificação na confirmação, pelo outbox do Wolverine, e o verificador consome a fila. O transporte foi testado em duas versões, RabbitMQ e a fila do Wolverine no Postgres.

## O que a POC mostra

### Cifra e verificador por webhook

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

### Verificador por fila

As provas 1 a 6 e 8 a 10 rodaram com os dois transportes. Estão em `resultados/provas-fila.txt`, na seção de cada transporte.

| Prova | RabbitMQ | Postgres |
|---|---|---|
| 1. Fluxo básico: confirmar, verificar, registrar o veredicto | ✓ | ✓ |
| 2. O verificador morre (SIGKILL) no meio de 40 verificações: nada se perde, os 40 terminam verificados | ✓ | ✓ |
| 3. O clamd fora do ar: nenhum arquivo é recusado; após as novas tentativas, o pedido vai para a fila de erro, e a reconciliação o republica quando o clamd volta | ✓ | ✓ |
| 4. Pedido perdido no transporte (fila esvaziada à mão): a reconciliação pelo banco da API recupera | ✓ | ✓ |
| 5. Reentrega de um arquivo já promovido: mesmo veredicto e mesmo SHA-256, sem nova verificação | ✓ | ✓ |
| 6. O transporte fora do ar | a confirmação continua (10 de 10), o pedido espera no outbox e segue quando o RabbitMQ volta | a confirmação falha inteira (HTTP 500), sem arquivo sem pedido nem pedido sem arquivo |
| 7. Arquivo acima do tamanho máximo (110 MB, o clamd não verifica acima de 100 MB): recusado sem ser lido | ✓ | — |
| 8. Arquivo infectado (EICAR): recusado, fica na quarentena, e a reentrega devolve o mesmo veredicto | ✓ | ✓ |
| 9. Confirmação sem objeto no bucket: reenvio necessário, e a reconciliação não o republica | ✓ | ✓ |
| 10. A confirmação fixa a versão pelo ETag: um PUT depois dela termina em reenvio necessário, e a reentrega não sobrescreve o arquivo promovido | ✓ | ✓ |

Outras provas, na mesma saída:

- **A API não lê o conteúdo.** No trace do MinIO não há nenhuma chamada da `api-fila`; só o verificador lê o objeto.
- **Aprovação por etiqueta é insegura.** Nessa alternativa, o verificador aprova o arquivo com uma etiqueta no próprio bucket de envio, sem copiá-lo, e a política da API só lê o objeto que tem a etiqueta. Mas o candidato pode mandar `x-amz-tagging: verificacao=aprovado` no PUT pela URL pré-assinada, e o MinIO aceita: o arquivo se aprova sozinho, sem verificação. Por isso a promoção continua por cópia entre buckets.

As medições estão em `resultados/medicoes-fila.md` e `resultados/medicoes-fila.jsonl`.

## Componentes

Fluxo por webhook (primeira parte):

```text
candidato ──PUT pré-assinado──▶ MinIO ◀──── KES ◀──── Vault (KV v2 + Transit)
     ▲                            │ webhook (fila em disco)
     │ URL                        ▼
  api-nova (só assina)      verificador ──INSTREAM──▶ clamd
                                 │ limpo: cópia no servidor para o bucket definitivo
                                 │ infectado: remove; registra sem conteúdo
```

Fluxo por fila (segunda parte):

```text
candidato ──PUT pré-assinado──▶ MinIO (bucket de envio)
     │                            ▲ lê em stream, promove por cópia
     │ confirma                   │
     ▼                            │
  api-fila ──outbox──▶ fila "verificacao" ──▶ verificador ──INSTREAM──▶ clamd (sidecar)
     ▲   │                                         │
     │   └─ Postgres: arquivo EM_VERIFICACAO       │
     └──────────── fila "resultado_verificacao" ◀──┘ veredicto, SHA-256, tamanho
```

- **Vault 1.21.2** (a versão de HML): KV v2 em `kv/`, Transit em `transit/` com a chave `uniplus-kes-kek`, AppRole `kes` com a política `config/vault-politica-kes.hcl`.
- **KES 2025-03-12T09-35-18Z**: guarda a chave `uniplus-arquivos-candidato` no KV, cifrada pela chave Transit. Só a identidade do MinIO consta da política (`config/kes.yml`).
- **MinIO RELEASE.2025-09-07** (a imagem do projeto): SSE-KMS como cifra padrão dos buckets `uniplus-envios` e `uniplus-arquivos-candidato`, notificação de PUT para o verificador e log de auditoria por webhook.
- **clamd 1.4** (a imagem do projeto). Nas medições com fila, cada clamd tem 2 CPUs, o tamanho de um sidecar; `clamav-2` e `clamav-3` são as réplicas (perfis `escala2` e `escala3`).
- **Wolverine 5.41.0** (a versão da API), **RabbitMQ 4.1.4** com quorum queues e **Postgres 18**, no perfil `fila`.
- **Aplicação** (`src/`, um executável com vários papéis): `verificador`, `api-nova` (assina URLs), `api-atual` (o fluxo de hoje, para comparação), `api-fila` (confirmação com outbox), `verificador-fila` (uma a três réplicas) e o gerador de carga. Os verificadores e as APIs têm os limites do pod da API em HML: 2 CPUs e 1 GiB.

Credenciais do MinIO por papel (`config/politica-*.json`): a API só assina PUT no bucket de envio e GET no definitivo. Ela não tem leitura no bucket de envio. O verificador lê, etiqueta e remove no bucket de envio, grava no definitivo e também lê no definitivo, para reconhecer na reentrega o arquivo que já promoveu. O SDK .NET do MinIO faz `HEAD` do bucket antes de operar, o que exige `s3:ListBucket` nos dois buckets. A política `politica-api-etiqueta.json` só existe para a prova da aprovação por etiqueta.

## Como reproduzir

Pré-requisitos: Docker com Compose v2 e o SDK .NET da banda 10.0.4xx (a do `global.json`). A rede do compose usa `172.31.250.0/24`, fora das rotas que a VPN instala em `tun0`.

```bash
cd spikes/cifra-no-minio
./scripts/subir.sh          # gera .estado/ (identidades, AppRole, segredos locais) e sobe tudo
./scripts/provas.sh         # grava resultados/provas.txt
./scripts/medir.sh          # grava resultados/medicoes.jsonl e resultados/medicoes.md
./scripts/provas-fila.sh    # grava resultados/provas-fila.txt (aceita seções: rabbitmq postgres trace etiqueta)
./scripts/medir-fila.sh     # grava resultados/medicoes-fila.jsonl e .md, cerca de 1 hora
docker compose --env-file .estado/env --profile provas --profile fila --profile escala3 down -v && rm -rf .estado
```

Na primeira subida, o clamd baixa a base de assinaturas, o que leva alguns minutos. Os segredos ficam só em `.estado/`, que está no `.gitignore`. Os roteiros com fila removem o evento do webhook e param o verificador por webhook, para os dois fluxos não disputarem o mesmo envio.

## Decisões de desenho da POC

- **KV com Transit, e não só KV.** O keystore Vault do KES guarda as chaves no KV, e o bloco `transit` faz o KES cifrar cada chave com uma chave Transit que nunca sai do Vault. Quem lê o KV, ou um backup dele, vê só `vault:v1:…` (prova 4). O Transit já é o que a API usa em HML.
- **Dois buckets.** O de envio recebe o PUT pré-assinado e é a quarentena. O definitivo só recebe o que o verificador promoveu. As políticas de acesso seguem essa divisão. O envio não verificado expira em 1 dia (regra de ciclo de vida).
- **Mesmo ETag na leitura e na cópia.** O verificador lê e promove com `If-Match` do ETag: o do evento, no webhook; o confirmado pelo candidato, na fila. No fluxo com fila, o candidato manda na confirmação o ETag que o PUT lhe devolveu, e a API o põe no pedido. Se outro PUT trocar o objeto antes da verificação, a versão confirmada não existe mais, e o arquivo termina em reenvio necessário.
- **Leitura em stream.** O verificador passa o objeto ao clamd em pedaços de 64 KiB e calcula o SHA-256 no caminho, sem ter o arquivo inteiro em memória.
- **Cache do KES curto** (30 s, sem cache offline), para a prova de falha fechada não esperar minutos. Em HML, esse valor é escolha de disponibilidade × revogação.
- **O pedido nasce na confirmação, não no bucket.** A API grava o arquivo como `EM_VERIFICACAO` e publica o pedido no outbox na mesma transação. Não existe arquivo em verificação sem pedido, nem pedido sem arquivo.
- **Reconciliação pelo banco da API.** `POST /reconciliar?segundos=N` republica o que está em verificação sem pedido publicado há mais de N segundos, contados da última publicação. Ela cobre pedido perdido, fila de erro e clamd que voltou. Em produção, roda periodicamente.
- **Idempotência no verificador.** A promoção grava o SHA-256 e o ETag do envio no metadado do objeto definitivo, junto do `Content-Type` do PUT (a cópia com REPLACE o perderia). A reentrega consulta primeiro o bucket definitivo e nunca sobrescreve o que já foi promovido. O arquivo infectado não é removido: fica na quarentena até expirar. Assim, a reentrega devolve o mesmo veredicto nos dois casos, e a API só conta o veredicto repetido, sem mudar o estado. O veredicto vira estado por `UPDATE` atômico, condicionado a `EM_VERIFICACAO`, e dois veredictos simultâneos não se sobrescrevem.
- **Reenvio necessário.** A versão confirmada que não está na quarentena nem no definitivo (confirmação sem PUT, PUT depois da confirmação, envio expirado) volta como `NAO_ENCONTRADO` e termina em `REENVIO_NECESSARIO`, que não é recusa e encerra a reconciliação.
- **Tamanho máximo no verificador.** A URL pré-assinada de PUT não limita o tamanho, e o clamd não verifica stream acima do `StreamMaxLength`. O verificador recusa sem ler o que passa de `TAMANHO_MAXIMO_BYTES` (padrão de 100 milhões de bytes, abaixo dos 104857600 do clamd).
- **Dois pools por processo.** Cada processo com Wolverine abre dois pools no Postgres, o do EF e o do Wolverine, ambos com o `Maximum Pool Size` da string de conexão (`POOL_POSTGRES`, padrão 10). A soma de todos os pools tem de caber no `max_connections`. Numa rodada anterior, com 50 por pool, só a API chegou às 100 conexões do Postgres, e a fila no Postgres falhou com `too many clients`. Os números publicados são da rodada com o pool corrigido.
- **Lixeira do MinIO.** O objeto removido fica em `.minio.sys/tmp` até um expurgo assíncrono, mais lento que as remoções da medição. Com o disco quase cheio, o MinIO recusa gravação, e a cópia para o bucket definitivo falha; numa rodada anterior, isso mandou 120 pedidos à fila de erro. O `medir-fila.sh` reinicia o MinIO quando o disco cai abaixo de 3 GiB e para se não recuperar. Em HML, o bucket de envio precisa de folga para essa lixeira.
- **Consumo um a um no RabbitMQ.** O verificador usa `ProcessInline`, `PreFetchCount(1)` e um consumidor por slot de paralelismo. A mensagem só é confirmada depois do veredicto publicado; se o processo morrer, ela volta à fila.

## O que não foi provado

- **Auditoria pelo KES.** Nesta versão, o KES não emitiu evento de auditoria, nem no stdout nem por `kes log --audit`. O log de erros dele traz a identidade em cada falha. A auditoria de acesso foi provada pelo audit webhook do MinIO (prova 10).
- **Quem usou a URL pré-assinada.** No log do MinIO, o GET pela URL aparece com a credencial de quem assinou (a API) e o IP de origem. A identidade do candidato não aparece ali. Ela precisa vir do registro de emissão da URL na API.
- **Volume cifrado.** A cifra do disco em si (LUKS ou a do provedor) é da infraestrutura e não entra na POC.
- **Autenticação do KES no Vault por Kubernetes.** O binário do KES aceita `kubernetes` no lugar de `approle`. A POC usou AppRole por não ter cluster.
- **RabbitMQ em cluster.** A POC rodou um nó só. A quorum queue tolera a queda de um nó num cluster de três, mas isso não foi exercitado.
- **Etiqueta exigida na assinatura do PUT.** Assinar o cabeçalho `x-amz-tagging` na URL de envio impediria o candidato de mandar a própria etiqueta, mas não foi testado. A promoção por cópia resolve sem isso.
- **Nome no `Content-Disposition`.** A `api-nova` da POC põe o nome recebido no cabeçalho sem codificar. Bastou para provar que o MinIO honra o `response-content-disposition`; a API real codifica o nome (`filename*`) e aceita só `inline` ou `attachment` (ADR-0146).
- **Formato do arquivo.** O verificador da POC não confere se o formato corresponde ao declarado; isso é da implementação.
