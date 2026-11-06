# Instalação dos gestores de acessos no ambiente local

Guia para deixar o Uni+ local utilizável depois da entrada do sistema de permissionamento. Com o
permissionamento, **o token não concede acesso**: toda permissão vem de uma concessão gravada no Uni+
([ADR-0152](adrs/0152-governanca-das-concessoes-de-acesso.md)). Num banco recém-criado não há concessão
nenhuma, então ninguém acessa nada além da área do candidato até que os dois primeiros **gestores de
acessos** sejam instalados.

> Complementa o [setup do ambiente local](setup-ambiente-local.md). Vale para banco novo ou recriado
> (`down -v`, `DROP DATABASE`): as concessões vivem no schema `autorizacao` do banco `uniplus`.

## Caminho principal: o compose deixa tudo pronto

Com Docker (Compose v2), a partir da raiz do repositório:

```bash
scripts/dev-up.sh
```

O comando leva o ambiente do zero a "dois gestores de acessos instalados e acesso funcionando":

1. cria `docker/.env` e `docker/docker-compose.override.yml` a partir dos exemplos, quando não existem (só
   valores de desenvolvimento, sem segredo real); se existem, confere se têm todas as chaves dos exemplos e
   o serviço `ambiente-init` e, faltando algo, **para antes de subir qualquer serviço** nomeando o que falta
   e como obtê-lo;
2. sobe o compose: a infraestrutura, o `kafka-init` (tópico do registro de acesso), o verificador de arquivos
   (`clamd` + `verificador`), o Geo, a API e o serviço **`ambiente-init`**;
3. acompanha o `ambiente-init` até terminar. Ele configura o Keycloak (`scripts/setup-keycloak-dev.sh`: senha
   do `admin`, personas fictícias do portal, papel `uniplus-instalacao`), instala os dois gestores
   (`diogo.souza` e `leandro.viana`) pelos quatro passos da API, lendo o código no Mailpit
   (`scripts/dev-instalar-gestores.sh`), e confere o acesso: `GET /api/autorizacao/concessoes` responde 200
   para os dois gestores e 403 para o executor (`admin`) e para o `candidato`.

O `ambiente-init` é um serviço do próprio compose, então **o `docker compose up -d` sozinho (com o override)
produz o mesmo resultado**; o script só acrescenta a conferência dos arquivos locais, o deslocamento de portas
e a espera até ficar pronto. Sem o script, acompanhe com
`docker compose -f docker/docker-compose.yml -f docker/docker-compose.override.yml logs -f ambiente-init`.

Nada grava concessão direto no banco: a instalação passa pela mesma regra da [ADR-0152](adrs/0152-governanca-das-concessoes-de-acesso.md).

| Situação | O que acontece |
|---|---|
| Rodar de novo (`dev-up.sh` ou `docker compose up -d`) com os gestores instalados | O `ambiente-init` roda de novo, confere o acesso e não altera nada (sem concessão nem instalação nova). |
| Banco ou volume recriado (`docker compose ... down -v`) | O `ambiente-init` reconfigura o Keycloak e reinstala os gestores. |
| `docker/.env` ou override de uma cópia antiga | O `dev-up.sh` falha antes de subir serviço, nomeando a chave, a variável ou o serviço ausente. Com `docker compose` direto, o override antigo não tem o `ambiente-init`: nada configura o Keycloak. |
| `--completo` | Sobe também o `portal-api`, o gateway e os quatro frontends (exige o `uniplus-web` clonado ao lado). |
| `--sem-instalacao` (ou `INSTALAR_GESTORES=false`) | Configura o Keycloak, sem instalar os gestores. |
| `--so-validar` | Só confere `.env`, override e compose. |

### Mais de um ambiente na mesma máquina

Cada serviço publica numa porta que vem de uma variável (`API_PORT`, `KEYCLOAK_PORT`, `POSTGRES_PORT`,
`MAILPIT_UI_PORT` e as demais listadas, comentadas, no fim de `docker/.env.example`). Defina-as no shell ou no
`docker/.env`, ou some um deslocamento a todas de uma vez:

```bash
scripts/dev-up.sh --deslocamento-de-portas 100   # API em 5300, Keycloak em 8180, Mailpit em 8125...
```

O deslocamento usa um projeto do compose próprio (`uniplus-desloc-100`), então contêineres e volumes não se
misturam com os do ambiente nas portas padrão. Para escolher as portas à mão, defina também
`COMPOSE_PROJECT_NAME` diferente. Os frontends só autenticam nas portas padrão (o realm importado registra os
redirecionamentos de `4200` a `4203`); o `dev-up.sh` ajusta apenas o client da Configuração.

### Domínios institucionais de desenvolvimento

A API aceita e-mail de gestor e do executor só em domínio institucional, por igualdade exata. No local, os
e-mails são `admin@teste.unifesspa.edu.br` e `*@uniplus-test.local`. Por isso o
`docker-compose.override.example.yml` — arquivo só de desenvolvimento — configura
`Autorizacao__Instalacao__DominiosInstitucionais__0..2`. Nenhum `appsettings` nem o `docker-compose.yml`
base traz esses domínios, e o CI confere isso (`tools/dev-up/validate.sh`): em HML e produção vale o domínio
exato da instituição.

O restante deste guia descreve o que o comando automatiza, para quem precisa entender, depurar ou repetir um
passo à mão.

## 1. O que a instalação exige

A instalação é a única operação que o papel de realm `uniplus-instalacao` habilita, e só enquanto **não
há nenhum gestor ativo**. Ela acontece em quatro passos de uma única pessoa, o **executor**:

| Passo | Rota | O que acontece |
|---|---|---|
| 1 | `POST /api/autorizacao/instalacao` | Informa os dois gestores, o número do documento de designação e o tamanho do PDF. Devolve a URL de PUT assinada. |
| 2 | `POST /api/autorizacao/instalacao/{id}/documento/confirmacao` | Informa o `ETag` do PUT. O verificador varre o PDF (clamd) e o promove. |
| 3 | `POST /api/autorizacao/instalacao/{id}/codigo` | Com o documento verificado, envia o código de 2 horas ao e-mail do executor. |
| 4 | `POST /api/autorizacao/instalacao/confirmacao` | O executor informa o código (5 tentativas, uso único). Os dois gestores passam a valer. |

Regras que aparecem como recusa se não forem cumpridas:

- o executor **não pode estar entre os dois gestores** (`instalacao.executor_entre_gestores`);
- são exatamente **dois gestores**, cada um com `emissor`, `sub` e `email` institucional;
- o PDF tem até 10 MB; o número do documento, até 60 caracteres;
- o código vai **só** para o e-mail do token do executor, que precisa estar verificado e em domínio
  institucional (`instalacao.email_nao_institucional`);
- com qualquer gestor ativo, a operação responde `instalacao.ja_ha_gestor`.

Não há tela de instalação: é só API.

## 2. Procedimento manual (alternativa ao comando)

Use só se não puder rodar o `scripts/dev-up.sh`. Antes do passo 1, deixe cinco peças no lugar (2.0 a 2.4).

### 2.0 Atualizar o `.env` e o override do compose

`docker/.env` e `docker/docker-compose.override.yml` **não são versionados** (estão no `.gitignore`): um
`git pull` não os atualiza, e quem clonou há algumas semanas tem cópias desatualizadas dos
`.env.example` e `docker-compose.override.example.yml`. O `scripts/dev-up.sh --so-validar` acusa o que falta.
À mão, compare:

```bash
# Chaves que o .env.example tem e o seu .env não tem
comm -13 <(grep -E '^[A-Z_]+=' docker/.env | cut -d= -f1 | sort -u) \
         <(grep -E '^[A-Z_]+=' docker/.env.example | cut -d= -f1 | sort -u)

# Variáveis de configuração do override de exemplo que o seu override não tem
diff <(grep -oE '^\s+[A-Za-z_]+(__[A-Za-z_0-9]+)+:' docker/docker-compose.override.yml | sort -u) \
     <(grep -oE '^\s+[A-Za-z_]+(__[A-Za-z_0-9]+)+:' docker/docker-compose.override.example.yml | sort -u)
```

Linhas com `>` são o que falta no seu arquivo. Se você não personalizou nada, o mais simples é guardar uma
cópia e recopiar os exemplos (valores de desenvolvimento, sem segredo real):

```bash
cp docker/.env docker/.env.bak && cp docker/.env.example docker/.env
cp docker/docker-compose.override.yml docker/docker-compose.override.yml.bak
cp docker/docker-compose.override.example.yml docker/docker-compose.override.yml
```

O que costuma faltar em cópias antigas:

| Arquivo | Chave | O que acontece sem ela |
|---|---|---|
| `docker/.env` | `UNIPLUS_API_SELECAO_CLIENT_SECRET` (`uniplus-dev-m2m`) | `setup-keycloak-dev.sh` aborta ("vazio em docker/.env") e o `docker compose` recusa subir a API (`:?defina ...`). |
| `docker/.env` | `GEO_IMAGE_TAG` (`v1.0.1`) | O compose recusa subir o serviço do Geo: não existe tag `latest`. |
| override | `ConnectionStrings__AutorizacaoDb` | A API não conecta no schema de autorização: é o banco onde ficam as concessões. |
| override | `ArquivosDoCandidato__AccessKey` e `__SecretKey` | A URL de envio do PDF e o verificador não acessam os buckets. |
| override | `RabbitMq__Uri` | O pedido de verificação do PDF não chega ao verificador: o passo 2 nunca sai de "em verificação". |
| override | `Geo__BaseUrl`, `Geo__OAuth__*` | `Geo__BaseUrl` é obrigatório: sem ele a API não sobe. |
| override | `Autorizacao__Instalacao__DominiosInstitucionais__0..2` | A API recusa os e-mails locais (2.2). |

A lista não é exaustiva. Depois de ajustar, confira `docker compose -f docker/docker-compose.yml -f docker/docker-compose.override.yml config -q`,
que acusa variável obrigatória sem valor.

### 2.1 Papel `uniplus-instalacao` no realm `unifesspa`

O `realm-export.json` (realm `unifesspa`, o que a API valida) **não traz esse papel**; só o
`realm-export-dev-local.json` o traz. O `scripts/setup-keycloak-dev.sh` o cria e o atribui ao `admin`. À mão,
use o usuário `admin` do realm, que não será gestor:

```bash
KC="docker compose -f docker/docker-compose.yml exec keycloak /opt/keycloak/bin/kcadm.sh"

$KC config credentials --server http://localhost:8080 --realm master --user admin --password admin
$KC create roles -r unifesspa -s name=uniplus-instalacao \
  -s 'description=Habilita só a instalação dos primeiros gestores de acessos'
$KC add-roles -r unifesspa --uusername admin --rolename uniplus-instalacao
```

Se o papel já existir, o `create` recusa; siga para o `add-roles`. O login por senha do `admin` e as personas
só funcionam depois de rodar `scripts/setup-keycloak-dev.sh`: sem isso o `admin` nasce com senha temporária
e o login é recusado com `invalid_grant`.

### 2.2 Domínio institucional aceito pela API

A API compara o domínio do e-mail por **igualdade exata** com `Autorizacao:Instalacao:DominiosInstitucionais`
(padrão: `unifesspa.edu.br`). O `docker-compose.override.example.yml` já traz os três domínios locais, no serviço
`uniplus-api`:

```yaml
      Autorizacao__Instalacao__DominiosInstitucionais__0: "unifesspa.edu.br"
      Autorizacao__Instalacao__DominiosInstitucionais__1: "teste.unifesspa.edu.br"
      Autorizacao__Instalacao__DominiosInstitucionais__2: "uniplus-test.local"
```

Com `dotnet run`, a chave equivalente vai no `appsettings.Development.json` do Host, seção
`Autorizacao:Instalacao:DominiosInstitucionais`. Não faça isso em HML ou produção: lá o domínio é o exato
da instituição.

### 2.3 Verificador de arquivos (clamd + verificador)

O PDF só vira "documento verificado" depois do verificador. Sem ele, o passo 2 fica em verificação para
sempre e o passo 3 recusa (`instalacao.situacao_invalida`). Os dois são serviços do `docker-compose.yml`
(`clamd-verificador` e `verificador`, construídos a partir dos Dockerfiles do repositório; o verificador
compartilha a rede do clamd, que escuta só em `127.0.0.1`):

```bash
docker compose -f docker/docker-compose.yml up -d --build clamd-verificador verificador
```

A primeira subida tenta atualizar a base de assinaturas e segue com a da imagem se não conseguir (até 2
minutos); o healthcheck do clamd só fica `healthy` depois disso.

### 2.4 Serviços de apoio no ar

MinIO (buckets `uniplus-candidato-envio` e `uniplus-candidato-definitivo`, criados pelo `minio-init`),
RabbitMQ, Mailpit (interface em <http://localhost:8025>), a API e o **`kafka-init`**. A API precisa de
`Storage__PublicEndpoint` apontando para o MinIO publicado no host (já no `docker-compose.override.example.yml`,
acompanhando `MINIO_PORT`) para a URL de PUT funcionar a partir da sua máquina.

O `kafka-init` cria o tópico `autorizacao_registros_de_acesso`. Subir só os serviços de infraestrutura
pelo nome costuma deixá-lo de fora, e sem o tópico a instalação funciona, mas **toda rota protegida por
permissão responde `503 registro_de_acesso.falha_ao_gravar`**, inclusive para os gestores recém-instalados:
o acesso é recusado quando não pode ser registrado. Suba-o com
`docker compose -f docker/docker-compose.yml up kafka-init`.

Com `dotnet run` e portas diferentes das padrão, a chave do SMTP é `Email__Smtp__Porta` (e não `Port`):
chave errada manda o e-mail para o Mailpit padrão `:1025`, que pode ser de outro ambiente.

### 2.5 Escolher quem são os gestores

Use as **personas fictícias** do portal, nunca pessoas reais. O `setup-keycloak-dev.sh` as cria no realm
`unifesspa` (etapa 5b) com a senha `Uni+Teste26`. Para o ambiente local, a sugestão é `diogo.souza` e
`leandro.viana`, mas qualquer par de personas serve, desde que diferente do executor.

Pegue o `sub` (id do usuário no Keycloak) e o e-mail de cada uma:

```bash
for u in diogo.souza leandro.viana; do
  $KC get users -r unifesspa -q username=$u --fields id,username,email,firstName,lastName
done
```

### 2.6 Executar a instalação

```bash
KC_URL=http://localhost:8080
API=http://localhost:5200
SENHA='Uni+Teste26'

# Token do executor (admin). --data-urlencode, não -d: a senha tem '+'.
TOKEN=$(curl -s -X POST "$KC_URL/realms/unifesspa/protocol/openid-connect/token" \
  --data-urlencode grant_type=password --data-urlencode client_id=admin-cli \
  --data-urlencode username=admin --data-urlencode "password=$SENHA" | jq -r .access_token)

# O emissor dos gestores é o `iss` do token do executor, sem edição.
ISS=$(jq -rR 'split(".")[1] | gsub("-"; "+") | gsub("_"; "/") | @base64d | fromjson | .iss' <<< "$TOKEN")

# PDF de designação fictício (qualquer PDF válido de até 10 MB).
PDF=scripts/edital-de-teste.pdf
TAM=$(stat -c%s "$PDF")
```

Substitua `SUB_DIOGO`, `SUB_LEANDRO` e os e-mails pelos valores de 2.5. O `scripts/dev-instalar-gestores.sh` faz estes quatro passos sozinho.

```bash
# Passo 1 — inicia
INICIO=$(curl -s -X POST "$API/api/autorizacao/instalacao" \
  -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -H "Idempotency-Key: $(cat /proc/sys/kernel/random/uuid)" \
  -d "{
    \"gestores\": [
      {\"emissor\": \"$ISS\", \"sub\": \"SUB_DIOGO\",   \"email\": \"EMAIL_DIOGO\",   \"nome\": \"Diogo Bruno Filipe Souza\"},
      {\"emissor\": \"$ISS\", \"sub\": \"SUB_LEANDRO\", \"email\": \"EMAIL_LEANDRO\", \"nome\": \"Leandro Caleb Viana\"}
    ],
    \"numeroDoDocumento\": \"PORTARIA-LOCAL-0001/2026\",
    \"tamanhoDoPdf\": $TAM
  }")
ID=$(echo "$INICIO" | jq -r .id)
URL=$(echo "$INICIO" | jq -r .urlDeEnvio)

# PUT do PDF na URL assinada (válida por 15 minutos). O ETag vem no cabeçalho da resposta.
ETAG=$(curl -s -D - -o /dev/null -X PUT -H "Content-Type: application/pdf" \
  --data-binary @"$PDF" "$URL" | tr -d '\r' | awk -F': ' 'tolower($1)=="etag"{print $2}')

# Passo 2 — confirma o documento
curl -s -X POST "$API/api/autorizacao/instalacao/$ID/documento/confirmacao" \
  -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -H "Idempotency-Key: $(cat /proc/sys/kernel/random/uuid)" -d "{\"etag\": $ETAG}"
```

`$ETAG` já vem entre aspas duplas do cabeçalho; por isso entra sem aspas extras no JSON.

Aguarde o veredicto do verificador (segundos) e confira `situacao`:

```bash
curl -s -H "Authorization: Bearer $TOKEN" -H "Accept: application/vnd.uniplus.instalacao.v1+json" \
  "$API/api/autorizacao/instalacao/$ID" | jq .situacao   # esperado: "documento_verificado"
```

> O `Accept` é o media type vendor da rota (`instalacao`, versão 1); sem ele a API também responde `200`.

```bash
# Passo 3 — envia o código (vai para o e-mail do admin; veja no Mailpit)
curl -s -X POST "$API/api/autorizacao/instalacao/$ID/codigo" \
  -H "Authorization: Bearer $TOKEN" -H "Idempotency-Key: $(cat /proc/sys/kernel/random/uuid)"

# Passo 4 — confirma, com o código lido em http://localhost:8025
curl -s -o /dev/null -w "%{http_code}\n" -X POST "$API/api/autorizacao/instalacao/confirmacao" \
  -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -H "Idempotency-Key: $(cat /proc/sys/kernel/random/uuid)" -d '{"codigo": "CODIGO_DO_MAILPIT"}'   # esperado: 204
```

### 2.7 Conferir

1. A confirmação responde `204`.
2. Entre na Configuração (<http://localhost:4203>) como `diogo.souza`: a primeira tela é
   `/acessos/fila-do-gestor`.
3. Repetir a instalação responde `422 instalacao.ja_ha_gestor`. É o comportamento esperado.

A partir daqui, as demais concessões (inclusive a da encarregada de dados) seguem o fluxo de pedido e
aprovação da ADR-0152, pela tela da Configuração. O `admin` volta a ser só `admin`: o papel
`uniplus-instalacao` não concede nada fora da instalação e pode ser retirado do usuário.

## 3. Problemas comuns

| Sintoma | Causa provável |
|---|---|
| `503 registro_de_acesso.falha_ao_gravar` depois da instalação | Tópico `autorizacao_registros_de_acesso` ausente: rode o `kafka-init` (2.4). |
| `403` no passo 1 | O executor não tem `uniplus-instalacao` no realm `unifesspa` (2.1), ou o token foi emitido antes de o papel ser atribuído: peça outro. |
| `422 instalacao.gestores_invalidos` | E-mail de gestor fora dos domínios configurados (2.2), ou campo ausente. |
| `422 instalacao.email_nao_institucional` no passo 3 | E-mail do executor fora dos domínios configurados, ou `emailVerified=false`. |
| `422 instalacao.executor_entre_gestores` | O executor foi listado como gestor. Use outro usuário para executar. |
| Passo 3 recusa com `instalacao.situacao_invalida` | O verificador não está rodando (2.3) ou ainda não deu o veredicto. Veja `docker compose -f docker/docker-compose.yml -f docker/docker-compose.override.yml logs verificador clamd-verificador`. |
| `422 instalacao.documento_invalido` | Arquivo que não é PDF, acima de 10 MB, ou ETag que não é o do PUT. |
| `422 instalacao.codigo_invalido` | Código errado, expirado (2 h), já usado ou de outra instalação. Esgotadas as 5 tentativas, recomece no passo 1. |
| `422 instalacao.ja_ha_gestor` | O banco já tem gestor ativo. Para reinstalar, recrie o banco ou o volume do Postgres. |
| Banco recriado e tudo voltou a dar 403 | As concessões foram junto. Rode `scripts/dev-up.sh` de novo (ou refaça 2.1 e 2.6); o realm do Keycloak só é afetado por `down -v`. |

## 4. Limitações conhecidas

- Não há tela de instalação (só API). A instalação continua sendo feita pela API, como em HML, de propósito,
  para exercitar o mesmo caminho; o `scripts/dev-instalar-gestores.sh` só automatiza as quatro chamadas.
- Os códigos de erro completos levam o prefixo `uniplus.autorizacao.` (ex.:
  `uniplus.autorizacao.instalacao.codigo_invalido`); a tabela acima os abrevia.
- O `nome` informado para cada gestor não volta na resposta da instalação (`nome: null`); a concessão grava
  o perfil "Gestor de acessos", escopo `instituicao` e origem `instalacao`.
- O `ambiente-init` redireciona o envio do PDF (a URL assinada aponta para a porta publicada do MinIO) com
  `curl --connect-to`, sem trocar o `Host` da assinatura; se mudar o endereço público do MinIO, ajuste
  `ARMAZENAMENTO_CONNECT_TO` no override.
- Os frontends só autenticam nas portas padrão (ver "Mais de um ambiente na mesma máquina").
- O teste automatizado do CI (`tools/dev-up/validate.sh`) cobre shellcheck, a validação de `.env` e override,
  o deslocamento das portas e a coerência dos compose; **não** sobe o ambiente. O percurso completo leva
  vários minutos e baixa a base de assinaturas do clamd, então é validado à mão a cada mudança nos scripts.

## 5. Validação

Percurso executado de ponta a ponta em 06/11/2026 a partir de um clone limpo (sem `docker/.env` nem override),
com `scripts/dev-up.sh --deslocamento-de-portas 30000`, ao lado de outros ambientes nas portas padrão, e repetido
com `docker compose up -d` direto nas duas últimas situações:

- primeira subida: a infraestrutura, o verificador e a API sobem, o Keycloak é configurado e os dois gestores são
  instalados (passos 1 a 4, código lido no Mailpit);
- `GET /api/autorizacao/concessoes`: 200 para `diogo.souza` e `leandro.viana`, 403 para `admin` e `candidato`;
- segunda execução (`dev-up.sh` e `docker compose up -d`): "Gestores já instalados", e a contagem de concessões e instalações
  no banco não muda (2 e 1);
- volumes do projeto removidos (`down -v`) e `docker compose up -d` de novo: os dois gestores são reinstalados.

Não exercitados: `--completo` (exige o `uniplus-web`) e a tela da Configuração.
