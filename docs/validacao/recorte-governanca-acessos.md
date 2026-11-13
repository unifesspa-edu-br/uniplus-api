# Validação local de ponta a ponta do recorte da governança de acessos

Roteiro reproduzível e resultado da validação local do recorte obrigatório da governança de acessos: separação entre quem cria e quem aprova, segunda etapa pela proteção de dados, designação do Encarregado de dados, continuidade dos gestores e do Representante do controlador, registro de acesso no tópico Kafka e E2E reais da web. A execução usa a stack local do `uniplus-api` (docker compose, Keycloak e Kafka locais) e o `uniplus-web` servido com `nx serve`. HML e produção não fazem parte desta validação.

Tudo que o roteiro grava passa pela API, sem escrita direta no banco. As identidades são as personas fictícias do realm `unifesspa` (`docker/keycloak/personas.json`). Nenhum token, `sub` ou e-mail entra no registro do resultado.

## 1. Versões

| Item | Versão |
|---|---|
| uniplus-api | `9de4a34f3` (`main` em 13/11/2026), imagem da API construída localmente |
| uniplus-web | `0591efee` (`main` em 13/11/2026) |
| uniplus-developers | `6d46f8054` (fonte dos requisitos e das páginas de erro conferidas) |
| Kafka | `apache/kafka:4.2.0` |
| Keycloak | `ghcr.io/unifesspa-edu-br/uniplus-keycloak:26.7.2` |
| PostgreSQL | `postgis/postgis:18-3.6` |

Para registrar as imagens efetivamente usadas numa nova rodada, depois da subida da seção 2.2:

```bash
source apoio.sh        # funções da seção 2.1
dc ps --format '{{.Service}}\t{{.Image}}' | while IFS=$'\t' read -r s i; do
  printf '%s\t%s\t%s\n' "$s" "$i" "$(docker image inspect -f '{{.Id}}' "$i")"; done
```

## 2. Stack própria com portas deslocadas

A validação usa um projeto compose próprio, para não disputar containers, volumes e portas com outra stack da máquina. Os arquivos `docker/.env` e `docker/docker-compose.override.yml` são cópias locais dos exemplos, fora do git.

```bash
cp docker/.env.example docker/.env
cp docker/docker-compose.override.example.yml docker/docker-compose.override.yml
```

No `docker/.env`, mude as portas publicadas (faixa +2000 sobre o padrão):

| Serviço | Variável | Padrão | Usada |
|---|---|---|---|
| PostgreSQL | `POSTGRES_PORT` | 5432 | 7432 |
| Redis | `REDIS_PORT` | 6379 | 8379 |
| Kafka | `KAFKA_PORT` | 9092 | 11092 |
| MinIO / console | `MINIO_PORT` / `MINIO_CONSOLE_PORT` | 9000 / 9001 | 11000 / 11001 |
| RabbitMQ / gestão | `RABBITMQ_PORT` / `RABBITMQ_MANAGEMENT_PORT` | 5672 / 15672 | 7672 / 17672 |
| Mailpit SMTP / UI | `MAILPIT_SMTP_PORT` / `MAILPIT_UI_PORT` | 1025 / 8025 | 3025 / 10025 |
| OpenLDAP | `LDAP_PORT` | 1389 | 3389 |
| Keycloak | `KEYCLOAK_PORT` | 8080 | **10180** |
| Apicurio | `APICURIO_PORT` | 8081 | 10081 |
| API | `API_PORT` | 5200 | 7200 |
| Geo / Portal API | `GEO_API_PORT` / `PORTAL_API_PORT` | 5400 / 5302 | 7400 / 7302 |
| Documentação OpenAPI | `OPENAPI_DOCS_PORT` | 5299 | 7299 |
| Gateway / painel | `GATEWAY_PORT` / `GATEWAY_DASHBOARD_PORT` | 5000 / 5080 | 7000 / 7080 |
| Web seleção, ingresso, portal, configuração | `*_WEB_PORT` | 4200-4203 | 6200-6203 |

**Não use 10080 para o Keycloak.** A porta está na lista de portas bloqueadas do padrão Fetch: o navegador e o `fetch` do Node recusam a conexão (`TypeError: fetch failed`, causa `bad port`). Com `curl` ela funciona, o que esconde o problema até os E2E. Evite também 6000, 6566 e 6665-6669.

### 2.1 Funções de apoio

Salve como `apoio.sh` fora do repositório e carregue com `source` antes de cada bloco. `API_DIR` é o checkout do `uniplus-api`; `VAL_DIR` recebe as evidências brutas, que não vão para o repositório.

```bash
API_DIR="${API_DIR:?checkout do uniplus-api}"; VAL_DIR="${VAL_DIR:?pasta das evidências}"
PROJETO=uniplus2413
API_PORT=7200; KEYCLOAK_PORT=10180; MINIO_PORT=11000
SELECAO_WEB_PORT=6200; INGRESSO_WEB_PORT=6201; PORTAL_WEB_PORT=6202; CONFIGURACAO_WEB_PORT=6203
API_URL="http://localhost:$API_PORT"; KC_URL="http://localhost:$KEYCLOAK_PORT"
SENHA="${SENHA:-Uni+Teste26}"; TOPICO=autorizacao_registros_de_acesso
PDF="$API_DIR/scripts/edital-de-teste.pdf"
export API_DIR VAL_DIR API_URL KC_URL

# Compose sempre com -p: nenhum comando alcança outro projeto.
dc() { docker compose -p "$PROJETO" --project-directory "$API_DIR/docker" \
         -f "$API_DIR/docker/docker-compose.yml" -f "$API_DIR/docker/docker-compose.override.yml" "$@"; }

token() {
  curl -sS -X POST "$KC_URL/realms/unifesspa/protocol/openid-connect/token" \
    --data-urlencode grant_type=password --data-urlencode client_id=admin-cli \
    --data-urlencode "username=$1" --data-urlencode "password=$SENHA" | jq -r '.access_token // empty'
}
claim() { jq -rR --arg c "$2" 'split(".")[1] | gsub("-";"+") | gsub("_";"/") | @base64d | fromjson | .[$c]' <<< "$1"; }
sub_de()   { claim "$(token "$1")" sub; }
email_de() { claim "$(token "$1")" email; }

# Chamada como a persona: corpo em $VAL_DIR/ultima.json, cabeçalhos em ultima.hdr, status em $STATUS.
api() {
  local quem="$1" metodo="$2" caminho="$3" corpo="${4:-}"
  local args=(-sS -o "$VAL_DIR/ultima.json" -D "$VAL_DIR/ultima.hdr" -w '%{http_code}' --max-time 90 -X "$metodo" "$API_URL$caminho"
              -H "Authorization: Bearer $(token "$quem")")
  [ "$metodo" = GET ] || args+=(-H "Idempotency-Key: $(cat /proc/sys/kernel/random/uuid)")
  [ -z "$corpo" ] || args+=(-H "Content-Type: application/json" -d "$corpo")
  STATUS=$(curl "${args[@]}")
  echo "$quem $metodo $caminho -> $STATUS $(jq -c 'if type=="object" then {code,title} else "lista de \(length)" end' "$VAL_DIR/ultima.json" 2>/dev/null)"
}
corpo() { jq "$@" "$VAL_DIR/ultima.json"; }

# Confere o status (padrão do case, ex.: 2??) e o code; grava uma linha em resultado.tsv.
espera() {
  local code r; code=$(jq -r 'if type=="object" then .code // empty else empty end' "$VAL_DIR/ultima.json" 2>/dev/null)
  case "$STATUS" in $1) r=PASSOU ;; *) r=FALHOU ;; esac
  [ -z "$2" ] || [ "$code" = "$2" ] || r=FALHOU
  printf '%s\t%s\tesperado %s %s\tobtido %s %s\n' "$r" "$3" "$1" "$2" "$STATUS" "$code" | tee -a "$VAL_DIR/resultado.tsv"
}

# Envia o PDF à URL assinada do MinIO e imprime o ETag.
enviar_pdf() {
  curl -fsS -D - -o /dev/null -X PUT -H "Content-Type: application/pdf" --data-binary @"$PDF" "$1" \
    | tr -d '\r' | awk -F': ' 'tolower($1)=="etag"{gsub(/"/,"",$2); print $2}'
}
situacao_do_pedido() { api "$2" GET "/api/autorizacao/pedidos-de-concessao/$1" >/dev/null; corpo -r .situacao; }

# Kafka lido de dentro do broker, pelo listener interno. O consumidor de linha de comando é só instrumento de teste.
fim_do_topico() {
  dc exec -T kafka /opt/kafka/bin/kafka-get-offsets.sh --bootstrap-server kafka:29092 --topic "$TOPICO" --time -1 | awk -F: '{print $NF}'
}
mensagens_desde() {   # cabeçalhos, chave e valor (no Kafka 4.2 a opção é --formatter-property)
  dc exec -T kafka /opt/kafka/bin/kafka-console-consumer.sh --bootstrap-server kafka:29092 --topic "$TOPICO" \
    --partition 0 --offset "$1" --timeout-ms 8000 \
    --formatter-property print.offset=true --formatter-property print.headers=true --formatter-property print.key=true 2>/dev/null
}
valores_desde() {     # só o valor, um lote JSON por linha
  dc exec -T kafka /opt/kafka/bin/kafka-console-consumer.sh --bootstrap-server kafka:29092 --topic "$TOPICO" \
    --partition 0 --offset "$1" --timeout-ms 8000 2>/dev/null
}

# O realm importado só registra os clients web em 4200-4203: alinha os quatro às portas novas.
ajustar_clients_web() {
  local adm cid atual porta
  adm=$(curl -fsS -X POST "$KC_URL/realms/master/protocol/openid-connect/token" \
        --data-urlencode grant_type=password --data-urlencode client_id=admin-cli \
        --data-urlencode username=admin --data-urlencode "password=${KEYCLOAK_ADMIN_PASSWORD:-admin}" | jq -r .access_token)
  for par in selecao-web:$SELECAO_WEB_PORT ingresso-web:$INGRESSO_WEB_PORT portal-web:$PORTAL_WEB_PORT configuracao-web:$CONFIGURACAO_WEB_PORT; do
    porta=${par##*:}
    cid=$(curl -fsS -H "Authorization: Bearer $adm" "$KC_URL/admin/realms/unifesspa/clients?clientId=${par%%:*}" | jq -r '.[0].id')
    atual=$(curl -fsS -H "Authorization: Bearer $adm" "$KC_URL/admin/realms/unifesspa/clients/$cid")
    jq --arg o "http://localhost:$porta" '.redirectUris = [$o + "/*"] | .webOrigins = [$o]' <<< "$atual" \
      | curl -fsS -o /dev/null -w "${par%%:*} -> %{http_code}\n" -X PUT -H "Authorization: Bearer $adm" \
          -H 'Content-Type: application/json' -d @- "$KC_URL/admin/realms/unifesspa/clients/$cid"
  done
}

# Uma leitura registrada no Kafka: $1=rótulo $2=persona $3=rota $4=status esperado $5=ids esperados (JSON) ou "corpo".
k_leitura() {
  local rot="$1" quem="$2" rota="$3" st="$4" ids="$5" n sub
  sub=$(sub_de "$quem"); n=$(fim_do_topico)
  api "$quem" GET "$rota"; espera "$st" "${6:-}" "$rot $quem GET $rota"
  [ "$ids" = corpo ] && ids=$(corpo -c '[.[].id]')
  sleep 2
  mensagens_desde "$n" > "$VAL_DIR/$rot.kafka.txt"
  valores_desde "$n" | jq -s --arg sub "$sub" --argjson ids "$ids" '{
     mensagens: length, registros: (map(length) | add // 0),
     chavesValidas: (map(.[].chave | test("^[0-9a-f]{64}$")) | all),
     execucaoUnica: (map([.[].registro.execucaoId] | unique | length == 1) | all),
     idsConferem: ((map(.[].registro.resourceId) | sort) == ($ids | sort)),
     atorConfere: (map(.[].registro.atorSubject == $sub) | all),
     campos: (map(.[].registro | {permissao, fonteGrant, resourceTipo, sensibilidade, resultado, permitido,
                                 motivoNegativa, grantId, perfilId, versaoId, escopoUnidadeId}) | unique)}' \
    | tee "$VAL_DIR/$rot.resumo.json" | jq -c .
  # Nenhum dado pessoal no tópico: e-mail, nome e justificativa nunca entram.
  printf '%s dadosPessoaisNoTopico=%s\n' "$rot" \
    "$(grep -c -i -E 'uniplus-test\.local|justificativa|Designação fictícia|Conferência dos documentos|Neves|Figueiredo|Aparicio|Carvalho|Moraes' "$VAL_DIR/$rot.kafka.txt")"
}
```

### 2.2 Subida numa base nova

```bash
source apoio.sh
dc down -v --remove-orphans
dc up -d --build $(dc config --services | grep -v -E -- '-web$')   # sem os containers da web: ela roda com nx serve
dc wait ambiente-init; dc logs ambiente-init | tail -3              # esperado saída 0 e "Ambiente pronto."
curl -fsS "$API_URL/health" >/dev/null && echo "api ok"
claim "$(token admin)" iss                                          # esperado http://localhost:10180/realms/unifesspa
ajustar_clients_web
```

## 3. Identidades

Todas entram com a senha `Uni+Teste26` pelo client `admin-cli`.

| Papel na validação | Persona | Como chega lá |
|---|---|---|
| Executor dos roteiros de preparo | `admin` | perfis do roteiro concedidos pela API (`scripts/acessos-do-roteiro.sh`) |
| Gestor de acessos 1 e 2 | `diogo.souza`, `leandro.viana` | instalação pelo `ambiente-init` |
| Representante do controlador 1 e 2 | `betina.neves`, `caroline.jesus` | preparo (`scripts/governanca-do-roteiro.sh`); a segunda etapa da designação de `caroline.jesus` é decidida por `betina.neves` |
| Encarregado de dados | `isabella.figueiredo` | passo 5.3; é parte do próprio pedido |
| Solicitante sem permissão | `manoel.aparicio` | pede o perfil protegido para outra pessoa (5.2) |
| Beneficiário do pedido protegido | `kevin.carvalho` | aceita o pedido do 5.2 |
| Pessoa sem nenhum pedido | `evelyn.moraes` | nenhuma ação |

## 4. Preparo

```bash
cd "$API_DIR"
time scripts/roteiro-processo-teste-inscricao.sh 2>&1 | tee "$VAL_DIR/preparo-1.log"
echo "saída: ${PIPESTATUS[0]}"                                                          # esperado 0 e "roteiro concluído"
scripts/roteiro-processo-teste-inscricao.sh 2>&1 | tee "$VAL_DIR/preparo-2.log"
echo "saída: ${PIPESTATUS[0]}"                                                          # reaproveita tudo e para no 409 do psm-2027
grep -E "já é Representante|já tem o perfil|409" "$VAL_DIR/preparo-2.log"
```

O roteiro concede ao `admin` os perfis de cadastro (um gestor cria a versão, o outro aprova), publica o termo de responsabilidade, designa os dois Representantes, envia à proteção de dados os itens do catálogo de tipos de documento e de fatos do candidato (o Representante aprova cada um), cria a unidade CEPS e publica o processo `psm-2027`.

```bash
GESTOR_ID=019a0000-0000-7000-8000-000000000001; REPRESENTANTE_ID=019a0000-0000-7000-8000-000000000002
ENCARREGADO_ID=019a0000-0000-7000-8000-000000000003
api diogo.souza GET "/api/autorizacao/concessoes?perfil=$REPRESENTANTE_ID&situacao=ativa&limit=100"
corpo -c '[.[] | select(.ativa)] | {representantes: length, comPrazo: map(select(.validaAte != null)) | length}'   # {2, 0}
api admin GET "/api/organizacao/unidades?limit=100"; CEPS=$(corpo -r '.[] | select(.codigo=="CEPS") | .id' | head -1)
```

## 5. Cenários dos critérios

### 5.1 Versão de perfil: um gestor cria, outro aprova, quem criou é recusado

Perfil comum com uma permissão pessoal de unidade, para que a versão também exija a etapa da proteção de dados.

```bash
TAM=$(stat -c%s "$PDF")
api diogo.souza POST /api/autorizacao/perfis "$(jq -nc --argjson t "$TAM" '{nome: "Validação local — documentos pessoais da unidade",
  descricao: "Perfil protegido da validação local de ponta a ponta.", permissoes: ["selecao:documentos-pessoais:consultar"],
  protocolo: "VALIDACAO-LOCAL-0001/2026", tamanhoDoDocumento: $t}')"
espera '2??' "" "5.1 cria o perfil e a versão 1"
PERFIL_P=$(corpo -r .perfilId); VERSAO_P=$(corpo -r .versaoId); ETAG=$(enviar_pdf "$(corpo -r .urlDeEnvio)")
api diogo.souza POST "/api/autorizacao/perfis/$PERFIL_P/versoes/$VERSAO_P/documento/confirmacao" "{\"etag\":\"$ETAG\"}"
for i in $(seq 1 80); do api leandro.viana GET "/api/autorizacao/perfis/$PERFIL_P" >/dev/null
  [ "$(corpo -r .versaoPendente.situacaoDoDocumento)" = verificado ] && break; sleep 3; done

api diogo.souza POST "/api/autorizacao/perfis/$PERFIL_P/versoes/$VERSAO_P/aprovacao" '{"etapa":"gestao_de_acessos"}'
espera 422 uniplus.autorizacao.perfil.decisor_e_autor "5.1 quem criou a versão não a aprova"
api leandro.viana POST "/api/autorizacao/perfis/$PERFIL_P/versoes/$VERSAO_P/aprovacao" '{"etapa":"gestao_de_acessos"}'
espera '2??' "" "5.1 o outro gestor aprova a gestão de acessos"
api leandro.viana POST "/api/autorizacao/perfis/$PERFIL_P/versoes/$VERSAO_P/aprovacao" '{"etapa":"protecao_de_dados"}'
espera 422 uniplus.autorizacao.perfil.etapa_invalida "5.1 gestor não decide a proteção de dados"
api betina.neves POST "/api/autorizacao/perfis/$PERFIL_P/versoes/$VERSAO_P/aprovacao" '{"etapa":"protecao_de_dados"}'
espera '2??' "" "5.1 o Representante aprova a proteção de dados"
api diogo.souza GET "/api/autorizacao/perfis/$PERFIL_P"; corpo '{versaoPendente, emVigor: .composicaoEmVigor.versaoId}'
```

### 5.2 Segunda etapa de um pedido protegido decidida pelo Representante

```bash
api leandro.viana GET /api/autorizacao/termos-de-responsabilidade/vigente; TERMO=$(corpo -r .id)
api manoel.aparicio POST /api/autorizacao/pedidos-de-concessao "$(jq -nc --arg e "$(email_de kevin.carvalho)" --arg p "$PERFIL_P" --arg u "$CEPS" --arg t "$TERMO" \
  '{beneficiarioEmail: $e, perfilId: $p, unidadeId: $u, anexaraDocumento: false, termoVersaoApresentadaId: $t,
    justificativa: "Conferência dos documentos pessoais na validação local."}')"
espera 201 "" "5.2 pedido protegido criado por terceiro sem permissão"; PEDIDO_K=$(corpo -r .id)
api kevin.carvalho POST "/api/autorizacao/pedidos-de-concessao/$PEDIDO_K/aceite" "{\"termoVersaoApresentadaId\":\"$TERMO\"}"
api manoel.aparicio POST "/api/autorizacao/pedidos-de-concessao/$PEDIDO_K/aprovacoes"
espera 403 uniplus.auth.forbidden "5.2 solicitante não decide"
api leandro.viana POST "/api/autorizacao/pedidos-de-concessao/$PEDIDO_K/aprovacoes"
espera 200 "" "5.2 gestor aprova a etapa funcional"
api diogo.souza POST "/api/autorizacao/pedidos-de-concessao/$PEDIDO_K/aprovacoes"
espera 422 uniplus.autorizacao.pedido.etapa_invalida "5.2 gestor não decide a segunda etapa"
api betina.neves POST "/api/autorizacao/pedidos-de-concessao/$PEDIDO_K/aprovacoes"
espera 200 "" "5.2 Representante decide a segunda etapa"; CONC_K=$(corpo -r .concessaoId)
```

### 5.3 Designação do Encarregado de dados

O pedido do perfil de governança sempre declara documento. `isabella.figueiredo` pede para si; a segunda etapa é decidida por `caroline.jesus`.

```bash
api isabella.figueiredo POST /api/autorizacao/pedidos-de-concessao "$(jq -nc --arg p "$ENCARREGADO_ID" --arg t "$TERMO" \
  '{beneficiarioEmail: null, perfilId: $p, unidadeId: null, anexaraDocumento: true, termoVersaoApresentadaId: $t,
    justificativa: "Designação fictícia do Encarregado de dados na validação local."}')"
espera 201 "" "5.3 pedido de Encarregado de dados"; PEDIDO_E=$(corpo -r .id)
api isabella.figueiredo POST "/api/autorizacao/pedidos-de-concessao/$PEDIDO_E/anexos" "{\"tamanhoDoPdf\":$TAM}"
ANEXO_E=$(corpo -r .id); ETAG=$(enviar_pdf "$(corpo -r .urlDeEnvio)")
api isabella.figueiredo POST "/api/autorizacao/pedidos-de-concessao/$PEDIDO_E/anexos/$ANEXO_E/confirmacao" "{\"etag\":\"$ETAG\"}"
for i in $(seq 1 80); do [ "$(situacao_do_pedido "$PEDIDO_E" isabella.figueiredo)" != aguardando_documento ] && break; sleep 3; done
api isabella.figueiredo POST "/api/autorizacao/pedidos-de-concessao/$PEDIDO_E/aceite" "{\"termoVersaoApresentadaId\":\"$TERMO\"}"
api diogo.souza POST "/api/autorizacao/pedidos-de-concessao/$PEDIDO_E/aprovacoes"; espera 200 "" "5.3 gestor aprova a etapa funcional"
# Leitura K10 (seção 6), aqui, antes da concessão: o Encarregado ainda não tem a permissão e lê por titularidade.
P=/api/autorizacao/pedidos-de-concessao; N_INICIO=$(fim_do_topico)
k_leitura K10 isabella.figueiredo "$P?fila=meus&limit=50" 200 corpo
api caroline.jesus POST "/api/autorizacao/pedidos-de-concessao/$PEDIDO_E/aprovacoes"
espera 200 "" "5.3 o segundo Representante decide a segunda etapa"; CONC_E=$(corpo -r .concessaoId)
```

### 5.4 Revogações: comum, mínimo de gestores e último Representante

A trava do último Representante vale desde a primeira designação: com dois, a revogação de um passa, e a do último restante é recusada.

```bash
conc_de() { api diogo.souza GET "/api/autorizacao/concessoes?perfil=$1&situacao=ativa&limit=100" >/dev/null; corpo -r --arg s "$2" '.[] | select(.beneficiario.sub==$s and .ativa) | .id'; }
revogar() { api diogo.souza POST "/api/autorizacao/concessoes/$1/revogacao" "$(jq -nc --arg e "$2" '{motivo: "Validação local da governança de acessos.", confirmacaoEmail: $e}')"; }

revogar "$CONC_K" "$(email_de kevin.carvalho)"; espera 204 "" "5.4 revogação comum"
revogar "$(conc_de "$GESTOR_ID" "$(sub_de leandro.viana)")" "$(email_de leandro.viana)"
espera 422 uniplus.autorizacao.concessao.minimo_de_gestores "5.4 revogação do penúltimo gestor recusada"
revogar "$(conc_de "$REPRESENTANTE_ID" "$(sub_de caroline.jesus)")" "$(email_de caroline.jesus)"
espera 204 "" "5.4 revogação de um de dois Representantes"
C_BETINA=$(conc_de "$REPRESENTANTE_ID" "$(sub_de betina.neves)")
revogar "$C_BETINA" "$(email_de betina.neves)"
espera 422 uniplus.autorizacao.concessao.ultimo_representante "5.4 revogação do último Representante recusada"
corpo -r .detail                                                            # orienta a designar antes quem substitui
api diogo.souza GET "/api/autorizacao/concessoes/$C_BETINA/trilha"; corpo '[.[].tipo]'   # sem "revogada"
```

## 6. Registro de acesso no tópico Kafka

Formato, conforme `contracts/kafka/registro-de-acesso/v1.schema.json`: uma mensagem por requisição, com chave igual ao `execucaoId`; o valor é um array de `{chave, registro}`, um item por resource lido; a `chave` tem 64 caracteres hexadecimais; o cabeçalho `uniplus.quantidade` é o tamanho do array.

| Origem do acesso | `permissao` | `fonteGrant` | `grantId` | `perfilId` |
|---|---|---|---|---|
| Encarregado de dados com a concessão | `autorizacao:pedidos:consultar` | `concessao` | a concessão do Encarregado | `019a…0003` |
| Parte sem a permissão de consulta | `autorizacao:pedidos:ler-proprios` | `titularidade` | nulo | nulo |

```bash
# K10 já rodou no 5.3, antes da concessão. As demais leituras vêm depois do 5.3.
k_leitura K1  isabella.figueiredo "$P?fila=meus&limit=50" 200 corpo     # depois do 5.3: concessão
k_leitura K2  isabella.figueiredo "$P/$PEDIDO_E" 200 "[\"$PEDIDO_E\"]"
k_leitura K3  isabella.figueiredo "$P/$PEDIDO_E/trilha" 200 "[\"$PEDIDO_E\"]"
k_leitura K4  isabella.figueiredo "$P/$PEDIDO_E/anexos/$ANEXO_E" 200 "[\"$PEDIDO_E\"]"   # Cache-Control: no-store
k_leitura K5  manoel.aparicio "$P?fila=meus&limit=50" 200 corpo
k_leitura K6  manoel.aparicio "$P/$PEDIDO_K" 200 "[\"$PEDIDO_K\"]"
k_leitura K7  kevin.carvalho "$P/$PEDIDO_K/trilha" 200 "[\"$PEDIDO_K\"]"
k_leitura K7b kevin.carvalho "$P/$PEDIDO_K" 200 "[\"$PEDIDO_K\"]"
api betina.neves GET "$P?fila=meus&limit=50"; PEDIDO_B=$(corpo -r --arg r "$REPRESENTANTE_ID" '.[] | select(.perfil.id==$r) | .id' | head -1)
api betina.neves GET "$P/$PEDIDO_B"; ANEXO_B=$(corpo -r '.anexos[0].id')
k_leitura K8  betina.neves "$P/$PEDIDO_B/anexos/$ANEXO_B" 200 "[\"$PEDIDO_B\"]"          # Representante também por titularidade
k_leitura K9  evelyn.moraes "$P?fila=meus" 200 '[]'                                     # 0 mensagens
k_leitura K11 manoel.aparicio "$P/$(cat /proc/sys/kernel/random/uuid)" 404 '[null,null,null,null]' uniplus.autorizacao.pedido.nao_encontrado
```

Depois das leituras, a busca de dado pessoal cobre todos os lotes gravados desde o K10, e não só os de cada leitura. Ela procura os e-mails institucionais das personas (`@uniplus-test.local`), os sobrenomes das personas envolvidas e os textos das justificativas usadas no passo 5:

```bash
mensagens_desde "$N_INICIO" > "$VAL_DIR/topico-desde-5.txt"
grep -c '^' "$VAL_DIR/topico-desde-5.txt"                                     # quantidade de lotes
grep -c -i -E 'uniplus-test\.local|justificativa|Designação fictícia|Conferência dos documentos|Neves|Figueiredo|Aparicio|Carvalho|Moraes|Jesus|Souza|Viana' \
  "$VAL_DIR/topico-desde-5.txt"                                                # esperado 0
```

## 6.5 Proteção aprovada no catálogo de tipos de documento e de fatos do candidato depois de recriar a API

O administrador do cadastro muda a classificação de um tipo de documento e de um fato do candidato da semente, envia os dois à proteção de dados, e o Representante aprova. Depois, o container da API é recriado sem apagar volumes, e nada pode mudar.

```bash
TD_ID=d0c00000-0000-7000-8000-000000000007      # CERTIDAO_CASAMENTO
FATO_ID=fa700000-0000-7000-8000-000000000038    # ESTADO_CIVIL (fato de sistema: só a proteção muda)
CLASSE_NOVA=SENSIVEL                           # diferente da semente nos dois itens

api admin GET "/api/configuracao/tipos-documento/$TD_ID"
api admin PUT "/api/configuracao/admin/tipos-documento/$TD_ID" "$(corpo -c --arg c "$CLASSE_NOVA" '{id, codigo, nome, categoria, descricao,
  formatosAceitos, tamanhoMaximoMb, tipoEquivalente, classificacaoProtecao: $c, finalidadeTratamento, hipoteseLegal}')"
espera 204 "" "6.5 admin muda a classificação do tipo de documento da semente"
api admin GET "/api/configuracao/admin/fatos-candidato/$FATO_ID"
api admin PUT "/api/configuracao/admin/fatos-candidato/$FATO_ID/definicao" "$(corpo -c --arg c "$CLASSE_NOVA" '{pontoResolucao,
  classificacaoProtecao: $c, finalidadeTratamento, hipoteseLegal, dominio, cardinalidade, fonteValores, formato, escopo}')"
espera 204 "" "6.5 admin muda a classificação do fato da semente"
api admin POST /api/configuracao/admin/protecao-do-catalogo/solicitacoes \
  "$(jq -nc --arg td "$TD_ID" --arg f "$FATO_ID" '{itens: [{cadastro: "TIPO_DOCUMENTO", id: $td}, {cadastro: "FATO", id: $f}]}')"
espera 200 "" "6.5 solicitação à proteção de dados"

decidir_item() {   # a decisão repete a proteção vigente, o ciclo e as manifestações lidas
  api betina.neves GET "/api/configuracao/admin/protecao-do-catalogo/$1/$2" >/dev/null
  api betina.neves POST /api/configuracao/admin/protecao-do-catalogo "$(corpo -c --arg cad "$3" '{cadastro: $cad, itemId: .id,
    decisao: "APROVAR", classificacaoProtecao, hipoteseLegal, finalidadeTratamento, ciclo: .aprovacao.ciclo,
    manifestacoesNoCiclo: .aprovacao.manifestacoesNoCiclo, justificativa: "Classificação revista na validação local."}')"
}
decidir_item tipos-documento "$TD_ID" TIPO_DOCUMENTO; espera 204 "" "6.5 Representante aprova o tipo de documento"
decidir_item fatos "$FATO_ID" FATO;                   espera 204 "" "6.5 Representante aprova o fato"

retrato() {   # leitura pela API e, só para conferência, SELECT no banco
  api betina.neves GET "/api/configuracao/admin/protecao-do-catalogo/tipos-documento/$TD_ID"; corpo -S . > "$VAL_DIR/6.5-td-$1.json"
  api betina.neves GET "/api/configuracao/admin/protecao-do-catalogo/fatos/$FATO_ID";         corpo -S . > "$VAL_DIR/6.5-fato-$1.json"
  dc exec -T postgres sh -c 'psql -U "$POSTGRES_USER" -d uniplus -At -F "|"' > "$VAL_DIR/6.5-banco-$1.txt" <<SQL
SELECT id, xmin, classificacao_protecao, protecao_situacao, protecao_ciclo, protecao_decidida_por, protecao_decidida_em
  FROM configuracao.tipo_documento WHERE id = '$TD_ID';
SELECT id, xmin, classificacao_protecao, protecao_situacao, protecao_ciclo, protecao_decidida_por, protecao_decidida_em
  FROM configuracao.rol_de_fatos_candidato WHERE id = '$FATO_ID';
SELECT count(*), md5(string_agg(t::text, ',' ORDER BY t::text)) FROM configuracao.protecao_do_catalogo_atos t;
SQL
}
retrato antes
dc up -d --force-recreate --no-deps uniplus-api     # nunca down -v aqui
until curl -fsS "$API_URL/health" >/dev/null; do sleep 3; done
retrato depois
for f in td fato; do diff "$VAL_DIR/6.5-$f-antes.json" "$VAL_DIR/6.5-$f-depois.json" && echo "$f IGUAL"; done
diff "$VAL_DIR/6.5-banco-antes.txt" "$VAL_DIR/6.5-banco-depois.txt" && echo "banco IGUAL"
```

A lista `GET /api/configuracao/admin/protecao-do-catalogo` é a fila do que aguarda a proteção de dados: `situacao=APROVADA` responde 422 `situacao_invalida` por desenho. O item aprovado se lê pelo detalhe da proteção e pelos cadastros.

O reinício prova que nada na subida reescreve a proteção aprovada e que a subida sobre uma base já aprovada é idempotente. Ele não reexecuta migration já aplicada, então não prova que uma migration nova com `UpdateData` respeita a proteção aprovada: essa garantia fica com a #2423.

## 7. Kafka parado com a API no ar

```bash
N_ANTES=$(fim_do_topico); dc stop kafka
curl -s -o /dev/null -w '%{http_code}\n' "$API_URL/health"          # 503, Kafka Unhealthy
api isabella.figueiredo GET "$P?fila=meus&limit=50"
espera 503 uniplus.autorizacao.registro_de_acesso.falha_ao_gravar "7 K1 com o broker parado"
corpo -c keys                                                       # só o ProblemDetails, sem dado
# … idem para as rotas de K2 a K8 e K7b; K9 (página vazia) continua 200 []
dc start kafka
until [ "$(docker inspect -f '{{.State.Health.Status}}' "$(dc ps -q kafka)")" = healthy ]; do sleep 3; done
N_VOLTA=$(fim_do_topico); [ "$N_VOLTA" = "$N_ANTES" ] && echo "nada gravado durante a parada"
api isabella.figueiredo GET "$P?fila=meus&limit=50"; espera 200 "" "7 K1 volta com o broker restaurado"
```

## 8. E2E reais da web

Pré-requisito: a stack do passo 2 com o preparo do passo 4, sem os containers `*-web`. O `runtime-config.json` dos apps aponta para as portas novas só durante a rodada, sem commit:

```bash
source apoio.sh
WEB_DIR="${WEB_DIR:?checkout do uniplus-web}"
cd "$WEB_DIR"
sed -i 's#http://localhost:5000#http://localhost:7000#; s#http://localhost:8080/realms#http://localhost:10180/realms#; s#http://localhost:4203#http://localhost:6203#' \
  apps/configuracao/public/assets/runtime-config.json apps/selecao/public/assets/runtime-config.json
npm ci
npx nx serve configuracao --port 6203 &       # o redirect do client OIDC é a porta ajustada no passo 2.2
npx nx serve selecao --port 6200 &

export E2E_BACKEND_REAL=1 KEYCLOAK_URL=http://localhost:10180 KEYCLOAK_ADMIN=admin KEYCLOAK_ADMIN_PASSWORD=admin
export E2E_API_URL=http://localhost:7200 E2E_GEO_URL=http://localhost:7400 GEO_URL=http://localhost:7400   # GEO_URL é lido pelo roteiro das notas do ENEM
export E2E_KAFKA_CONTAINER=uniplus2413-kafka-1 E2E_KAFKA_BOOTSTRAP=kafka:29092
export E2E_ROTEIRO_DO_PROCESSO="$API_DIR/scripts/roteiro-processo-teste-inscricao.sh"
export E2E_ROTEIRO_DAS_NOTAS_DO_ENEM="$API_DIR/scripts/roteiro-processo-teste-notas-do-enem.sh"
BASE_URL=http://localhost:6203 npx nx e2e configuracao-e2e -- --project=configuracao-backend-real --reporter=list
BASE_URL=http://localhost:6200 npx nx e2e selecao-e2e      -- --project=selecao-backend-real      --reporter=list
git checkout -- apps/configuracao/public/assets/runtime-config.json apps/selecao/public/assets/runtime-config.json
```

`BASE_URL` tem de ser a porta servida: o Playwright reaproveita o servidor já no ar e só sobe o próprio na porta padrão.

### 8.1 Resultado dos E2E (13/11/2026, api `9de4a34f3`, web `0591efee`, base recriada com os dois Representantes)

**Configuração (`configuracao-e2e`, governança de acessos): 28 de 28 passaram, em 1,3 min.** Specs: `fatos-candidato`, `perfis-de-acesso`, `perfis-versoes`, `protecao-do-catalogo`, `protecao-do-catalogo.manifestacao`, `providencias`, `revisoes-de-acesso` e `termo-de-responsabilidade`.

**Seleção (`selecao-e2e`): não executada por falta da carga da Geo (DNE).** Na base nova, a Geo sobe sem cidades nem logradouros: `GET /api/cidades` responde `[]` e `GET /api/cep/79096730` responde 404. A carga exige os dumps da release DNE no banco de staging e a importação pelo `POST /api/admin/geo/importacoes`, como descreve o [guia da carga da Geo](../geo-etl-dataset-dne.md). A seleção não faz parte do recorte da governança de acessos. Na tentativa, passaram as duas specs que não dependem da Geo (`bloqueio-por-protecao-pendente`, a recusa da API e a oferta do tipo pendente na tela) e a autenticação. Falharam pela mesma causa, e as demais não chegaram a rodar:

| Spec | Como a falta da Geo aparece |
|---|---|
| `cadastro-inicial` | o município "Marabá — PA" não aparece na busca |
| `comprovante-da-inscricao`, `detalhe-da-inscricao`, `inscricoes-do-processo`, `matriz-de-permissoes`, `revelacao-de-identificadores` | a fixture de inscrição recebe 404 ao resolver o CEP 79096730 |
| `notas-do-enem` | o roteiro das notas do ENEM não resolve o CEP das personas |

A rodada completa da seleção fica para uma base com a Geo carregada.

## 9. Resultado (13/11/2026)

| Critério ou cenário | Esperado | Obtido | Resultado |
|---|---|---|---|
| Base nova, `ambiente-init` | saída 0, "Ambiente pronto.", 2 gestores | saída 0, `diogo.souza` e `leandro.viana` | passou |
| Preparo, 1ª execução | saída 0, "roteiro concluído" | saída 0 em 46 s; dois Representantes designados; 161 itens do catálogo de tipos de documento e de fatos do candidato aprovados | passou |
| Preparo, 2ª execução | reaproveita; 409 no `psm-2027` | reaproveitou executor, Representantes e catálogo; 409 `identificador_legivel_em_uso` | passou |
| Concessões após o preparo | 2 gestores, 2 Representantes sem prazo | 2 gestores, 2 Representantes sem prazo, 2 perfis do `admin` | passou |
| Versão: quem criou aprova | 422 `perfil.decisor_e_autor` | 422 `perfil.decisor_e_autor` | passou |
| Versão: outro gestor aprova | 2xx | 200 | passou |
| Versão: gestor decide a proteção de dados | 422 `perfil.etapa_invalida` | 422 `perfil.etapa_invalida` | passou |
| Versão: Representante aprova | 2xx; versão em vigor | 200; `composicaoEmVigor.versaoId` igual à versão, pendente nula | passou |
| Pedido protegido: solicitante decide | 403 `uniplus.auth.forbidden` | 403 `uniplus.auth.forbidden` | passou |
| Pedido protegido: gestor decide a segunda etapa | 422 `pedido.etapa_invalida` | 422 `pedido.etapa_invalida` | passou |
| Pedido protegido: Representante decide | 200 `aprovado`, concessão ativa | 200 `aprovado`, concessão ativa na CEPS | passou |
| Encarregado de dados: segunda etapa pelo outro Representante | 200 `aprovado` | 200 `aprovado`; perfil em `/api/profile/me` | passou |
| Revogação comum | 204; `ativa=false` | 204; `ativa=false` com motivo | passou |
| Penúltimo gestor | 422 `minimo_de_gestores` | 422; 2 gestores ativos; concessão intacta | passou |
| Um de dois Representantes | 204 | 204 | passou |
| Último Representante | 422 `ultimo_representante`; trilha sem revogação | 422; o detail orienta a designar antes quem substitui; 1 ativo; trilha sem `revogada` | passou |
| K10, Encarregado antes da concessão | titularidade | 1 lote, `ler-proprios` por `titularidade` | passou |
| K1-K4, Encarregado com a concessão | concessão, `grantId` da concessão, perfil `0003` | 1 lote e 1 registro em cada, `consultar` por `concessao`; K4 com `Cache-Control: no-store` e `anexo_lido` na trilha; `resourceId` é o id do pedido | passou |
| K5-K8 e K7b, partes sem a permissão | titularidade | 1 registro em cada, `ler-proprios` por `titularidade`, `grantId` e `perfilId` nulos; K8 com o Representante também por titularidade | passou |
| K9, página vazia | 200 `[]` e nenhuma mensagem | 200 `[]`, 0 mensagens | passou |
| K11, pedido inexistente | 404 e negativas | 404 `pedido.nao_encontrado`; 1 lote com 4 negativas `sem_concessao_aplicavel`, `resourceId` nulo | passou |
| Nenhum dado pessoal no tópico | 0 ocorrências | 0 ocorrências de e-mail das personas, sobrenome das personas envolvidas e texto de justificativa nos 49 lotes gravados do passo 5 em diante | passou |
| Proteção aprovada com classificação diferente da semente | 204/200/204 | `CERTIDAO_CASAMENTO` e `ESTADO_CIVIL` passaram a `SENSIVEL`, ciclo 2, `APROVADA` | passou |
| Igual depois de recriar a API | diffs vazios | API e banco iguais: `xmin`, ator, data, ciclo, total e hash dos atos; nenhuma migration pendente | passou |
| Broker parado: K1-K8 e K7b | 503 `falha_ao_gravar`, sem dado | 503 em todos, cerca de 10 s cada, só ProblemDetails | passou |
| Broker parado: K9 | 200 `[]` | 200 `[]` | passou |
| Nada gravado durante a parada | offset igual | 265 antes e depois; nenhuma entrega atrasada | passou |
| `/health` com o broker parado | registrar | 503, Kafka Unhealthy | registrado |
| Broker restaurado | 200 e lotes de volta | 200 na primeira tentativa; K1 por concessão e K5 por titularidade | passou |
| E2E reais da configuração | todos passam | 28 de 28 | passou |
| E2E reais da seleção | — | Geo sem carga da DNE (seção 8.1) | não executado |

## 10. O que ainda exige conferência manual

Os E2E e as chamadas acima não cobrem:

1. A tela com o broker parado: entrar como parte (`manoel.aparicio`) e como Encarregado de dados (`isabella.figueiredo`) na lista e no detalhe dos próprios pedidos no app de configuração, com o Kafka parado. Esperado: aviso de indisponibilidade em pt-BR, sem dado parcial, sem levar a `/acesso-negado` nem ao login.
2. A mensagem do `ultimo_representante` na tela da concessão (`/acessos/concessoes/:id`), como gestor, tentando revogar o único Representante: o texto orienta a designar antes quem substitui.
3. A recusa `decisor_e_autor` na tela da versão de perfil (`/acessos/perfis/:id/versoes/:versaoId`), como o gestor que criou a versão.
4. O registro no Kafka das leituras pela tela: nenhuma spec da web confere o tópico nessas leituras; o passo 6 as confere pela API.

## 11. Limites

1. O reinício do passo 6.5 não reexecuta migration aplicada; a garantia para migration nova com `UpdateData` segue com a #2423.
2. Os E2E da seleção dependem da carga da Geo (DNE), que a base nova não traz (seção 8.1).
3. A spec `detalhe-da-inscricao.backend.spec.ts` da web espera a URL assinada em `localhost:9000`; com outra `MINIO_PORT`, ela falha por ambiente mesmo com a Geo carregada.
4. O Keycloak não pode ficar na porta 10080: o padrão Fetch a bloqueia, e o navegador e o `fetch` do Node recusam a conexão com `bad port` (seção 2).
