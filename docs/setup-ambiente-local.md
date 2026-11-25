# Setup do ambiente de desenvolvimento local

Guia para configurar e executar o ambiente de desenvolvimento da Uni+ API.

## Pré-requisitos

| Ferramenta | Versão mínima | Verificação | Para quê |
|---|---|---|---|
| Docker | 24+ | `docker --version` | Tudo: é o único pré-requisito do caminho principal |
| Docker Compose | 2.20+ | `docker compose version` | |
| Git | 2.40+ | `git --version` | |
| .NET SDK | 10.0.400 (qualquer patch `10.0.4xx`) | `dotnet --version` | Só para rodar a API fora do Docker (Modo 1), build e testes |
| `jq` e `curl` | — | `jq --version` | Só para rodar à mão `scripts/setup-keycloak-dev.sh` e `scripts/dev-instalar-gestores.sh` (Modo 1) |

## Setup rápido

Um clone novo chega a **infraestrutura, API no ar, Keycloak configurado e dois gestores de acessos instalados**
com dois comandos:

```bash
git clone https://github.com/unifesspa-edu-br/uniplus-api.git
cd uniplus-api/docker
cp .env.example .env
docker compose up --build
```

O `.env.example` já traz todos os valores do ambiente local (só desenvolvimento, sem segredo real): copiar é
tudo. O primeiro `up` constrói imagens e o clamd baixa a base de assinaturas: conte alguns minutos. Quando o
`ambiente-init` termina com "Ambiente pronto", a API responde em http://localhost:5200/health.

O `ambiente-init` é um serviço do próprio compose, que configura o Keycloak (senha do `admin`, personas
fictícias, papel de instalação) e instala os dois gestores pela API. Rodar `docker compose up` de novo não altera
nada: ele confere o acesso e termina. Para acompanhar a configuração: `docker compose logs -f ambiente-init`.

Sem a instalação dos gestores ninguém acessa nada além da área do candidato: o token não concede acesso, toda
permissão vem de uma concessão gravada no Uni+ ([ADR-0152](adrs/0152-governanca-das-concessoes-de-acesso.md)).
Detalhes, o procedimento manual e a solução dos problemas comuns estão no
[guia da instalação dos gestores](guia-instalacao-gestores-ambiente-local.md).

Variações (todas por variável no `docker/.env`):

| Quero | Faça |
|---|---|
| Configurar o Keycloak sem instalar os gestores | `INSTALAR_GESTORES=false` |
| Trocar uma porta ocupada | Edite a variável `*_PORT` correspondente |
| Um segundo ambiente na mesma máquina | Copie o repositório para outra pasta, troque todas as portas e defina `COMPOSE_PROJECT_NAME` diferente |
| Stack completo (`portal-api`, gateway Traefik e frontends; exige o `uniplus-web` clonado ao lado) | `cp docker-compose.override.example.yml docker-compose.override.yml` e `docker compose up --build` |

Quando algo não sobe, `scripts/diagnosticar-ambiente.sh` faz o diagnóstico (só lê os arquivos):
chaves do `.env` que faltam ou estão vazias frente ao `.env.example`, override antigo que redefine a API ou o
`ambiente-init`, recusa do compose e portas já ocupadas, e mostra o comando que corrige cada um. Não faz parte da
subida. Com `--corrigir` ele aplica a correção, guardando cópia de segurança (`.env.bak-<data>`,
`docker-compose.override.yml.bak-<data>`): acrescenta ao `.env` as chaves que faltam sem mexer nos seus valores,
preenche as obrigatórias vazias e põe o override antigo de lado.

Se você tem um `docker/docker-compose.override.yml` de antes desta mudança, apague-o ou recopie o exemplo:
a API e o `ambiente-init` agora vivem no `docker-compose.yml`, e o override antigo sobrepõe os valores deles.
Se o `.env` é antigo e falta uma chave, o compose para e nomeia a chave (copie-a do `.env.example`).

### Base local pronta

O `docker compose up` já sobe com uma base pronta, restaurada na primeira subida (volume do Postgres vazio) a partir de
`docker/base-local/`: dois processos seletivos de exemplo, os perfis de operação com concessões às personas, as 60 personas no Keycloak
e a base de CEP no Geo. Não há passo extra além de `cp .env.example .env`.

- **Processos:** `psm-teste-multicurso-2027` (56 inscrições das personas) e `psm-2027` (Medicina, sem inscrições). A inscrição de `psm-2027` vai de
  12/11/2026 às 00:00 até 27/11/2026 às 23:59:59, e a de `psm-teste-multicurso-2027`, de 12/11/2026 às 00:00 até 25/11/2026 às 14:08:22, horário de Belém.
- **Pessoas:** as 60 personas fictícias do portal, com e-mail `{usuário}-uniplus-dev@unifesspa.edu.br` (os avisos vão só ao Mailpit) e a
  senha de desenvolvimento do `.env.example`. Gestores de acessos: `diogo.souza` e `leandro.viana`; Representantes do controlador:
  `betina.neves` e `caroline.jesus`; Gestor de proteção de dados: `marlene.martins`.
- **O que vem:** bancos `uniplus` e `keycloak` (`docker/base-local/dumps/`), arquivos do MinIO e o Geo (`geo/geo-api-database.tar.xz`, comprimido
  com xz). Os dumps do `uniplus` e do `keycloak` são restaurados juntos: as concessões guardam o id da conta no Keycloak.
- **Reiniciar do zero:** `docker compose down -v && docker compose up`. A restauração só acontece com o volume vazio, e leva alguns
  minutos na primeira subida (o Geo tem 1,6 milhão de logradouros).
- **Se a primeira subida for interrompida ou falhar** (disco cheio, `Ctrl+C`), o Postgres e o MinIO não repetem a restauração sobre um
  volume já iniciado: rode `docker compose down -v` e suba de novo.
- **Valores obrigatórios no `docker/.env`:** os dumps gravam o admin do Keycloak e endereços do ambiente padrão: o emissor das concessões
  (`localhost:8080`), as URLs dos editais no MinIO (`localhost:9000`) e as origens dos clients web (`localhost:4200` a `4203`). Por isso a base pronta
  exige os valores do `.env.example` em `KEYCLOAK_ADMIN`, `KEYCLOAK_ADMIN_PASSWORD`, `KEYCLOAK_PORT`, `MINIO_PORT`, `SELECAO_WEB_PORT`,
  `INGRESSO_WEB_PORT`, `PORTAL_WEB_PORT` e `CONFIGURACAO_WEB_PORT`. Com outros valores a restauração para com uma mensagem de erro que lista os
  divergentes; ajuste o `.env` e rode `docker compose down -v`, ou suba sem a base pronta. As demais portas (inclusive `API_PORT`) podem mudar: o
  `localhost:5200` que aparece no banco é só o cache de idempotência de respostas antigas, que nunca é reproduzido.
- **Versão da base:** vale para a migration `Inicial` da `main`. Quando a migration for regerada, os dumps precisam ser gerados de novo.
  Com dumps de outra versão da migration, a API para na subida com a mensagem "tem migration aplicada que esta versão não conhece", que traz o comando `docker compose down -v && docker compose up`; se o volume já era novo, os dumps estão defasados e precisam ser regerados.

### Gerar de novo a base local

A base pronta guarda o banco, o Keycloak e os arquivos do MinIO do dia em que foi gerada. Gere-a de novo, e refaça a prova do
`docker compose up`, sempre que mudar a migration, o modelo, o Keycloak, os perfis e permissões ou as portas gravadas (lista em
`CONTRIBUTING.md`, "Base local pronta e o que invalida os dumps"). Os dados novos nascem dos roteiros abaixo, e não de uma geração
anterior: quem segue estes passos não precisa de nenhum dado gravado antes.

**Antes de começar**

- A migration nova já está gerada e a branch compila (`docs/guia-banco-de-dados.md`, seção 7).
- Só um ambiente completo por vez na máquina, com as portas padrão do `.env.example`: o restaurador exige `KEYCLOAK_PORT=8080`,
  `MINIO_PORT=9000` e as quatro `*_WEB_PORT` de 4200 a 4203. Pare qualquer outro ambiente antes de começar.
- Use um nome de projeto próprio no compose (`-p base-local`), para não apagar os volumes do seu ambiente de trabalho.
- Tenha `jq` e `curl` instalados e o repositório `uniplus-developers` clonado ao lado deste: o roteiro das notas do ENEM lê
  `uniplus-developers/src/data/fake-people.json`.
- A geração sobe o ambiente completo e roda os roteiros: conta como teste pesado. Na geração de prova de 21/11/2026, a subida do ambiente levou cerca de 2 minutos (com as imagens já construídas; sem elas, a construção leva a maior parte do tempo),
  os perfis e as concessões 21 segundos, o processo da Medicina 16 segundos e o processo multicurso 15 segundos; a espera pelo fim do
  período de inscrição do multicurso (40 minutos) é a maior parte do tempo total. A prova em projeto limpo levou cerca de 1,5 minuto para subir.

**Passos** (a partir da raiz do repositório)

1. **Subir o ambiente sem o banco antigo.** Copie `docker/.env.example` para `docker/.env`. A sobreposição
   `docker/base-local/docker-compose.gerar-base-local.yml` faz o Postgres restaurar só o Geo (o `down -v` antes garante volumes vazios; sem ele, uma tentativa anterior deixaria dados antigos): o Keycloak importa o realm do repositório, a API
   cria o banco do monólito do zero com a migration da branch e o MinIO começa vazio:
   ```bash
   cd docker
   docker compose -p base-local -f docker-compose.yml -f base-local/docker-compose.gerar-base-local.yml down -v   # apaga só os volumes deste projeto
   docker compose -p base-local -f docker-compose.yml -f base-local/docker-compose.gerar-base-local.yml up -d --build
   docker wait base-local-ambiente-init-1   # espera o ambiente-init; deve devolver 0
   cd ..
   ```
   O `ambiente-init` cria as 60 personas no Keycloak e instala os dois gestores de acessos. Confira nos logs da API (`docker logs base-local-uniplus-api-1`)
   que ela aplicou a migration da branch.
2. **Criar os perfis de operação, as unidades e as concessões.** Rode `scripts/perfis-de-operacao.sh`. Ele lê
   `scripts/perfis-de-operacao.json` e faz, pela API, o que uma pessoa faria: o primeiro gestor cria cada perfil e envia o PDF que o institui, o
   segundo gestor aprova a versão e, se o perfil é protegido, um Representante do controlador aprova a proteção de dados; cria as unidades CEPS e
   CRCA; cada persona pede o seu perfil e aceita o termo, o gestor aprova e, se o perfil é protegido, o outro Representante decide. Rodar de novo
   não repete o que já existe. Os perfis e as concessões são:

   | Perfil | Alcance | Concedido a |
   |---|---|---|
   | Administração dos cadastros, Organização institucional e Publicações | instituição | a persona administradora (`ADMIN_USER`; `diogo.souza` por padrão) |
   | Coordenação de processo seletivo | unidade CEPS | `betina.neves` |
   | Equipe de inscrições | unidade CEPS | `isabella.figueiredo`, `kevin.carvalho`, `helena.rezende` |
   | Equipe de inscrições | unidade CRCA | `breno.novaes` |
   | Envio ao INEP | unidade CEPS | `kevin.carvalho` |
   | Dados e documentos sensíveis | unidade CEPS | `helena.rezende` |
   | Consulta sem dados pessoais | unidade CEPS | `erick.ramos` |
   | Gestor de proteção de dados (já vem da migration) | instituição | `marlene.martins` |

3. **Criar os dois processos de exemplo,** com a persona administradora. No multicurso, informe o período de inscrição com data, hora e fuso de Belém:
   ```bash
   export ADMIN_USER=diogo.souza
   # Medicina: a inscrição abre ontem e fecha 31 dias depois do início, conforme a minuta.
   scripts/roteiro-processo-teste-inscricao.sh
   # Multicurso: a inscrição fecha depois de as personas se inscreverem (aqui, 40 minutos; aumente se o seu computador for lento:
   # o roteiro para com erro se o período fechar antes da última inscrição).
   INICIO_INSCRICAO="$(TZ=America/Belem date -d '13 days ago' +%Y-%m-%dT00:00:00-03:00)" \
   FIM_INSCRICAO="$(TZ=America/Belem date -d '40 minutes' +%Y-%m-%dT%H:%M:%S-03:00)" \
   NOME_PROCESSO="PSR 2027 — Teste multicurso" IDENTIFICADOR_PROCESSO=psm-teste-multicurso-2027 \
     scripts/roteiro-processo-teste-notas-do-enem.sh
   ```
   Depois do segundo roteiro, espere o fim do período de inscrição do multicurso antes de seguir (o processo só fica "encerrado com inscritos" depois dele). Ele cria os dois processos pela API, com as 56
   inscrições das personas no multicurso e nenhuma na Medicina.
4. **Gerar os arquivos.** Pare a API, o verificador de arquivos e o MinIO, para nada gravar durante a cópia
   (`docker compose -p base-local stop uniplus-api verificador minio`, na pasta `docker`). Depois, na raiz:
   ```bash
   docker exec base-local-postgres-1 pg_dump -U uniplus -Fc -d uniplus > docker/base-local/dumps/uniplus.dump
   docker exec base-local-postgres-1 pg_dump -U uniplus -Fc -d keycloak > docker/base-local/dumps/keycloak.dump
   pasta="$(mktemp -d)" && docker cp base-local-minio-1:/data/. "$pasta/" && tar czf docker/base-local/minio/minio-data.tar.gz -C "$pasta" . && rm -rf "$pasta"
   ```
   Os dois dumps são gravados sempre juntos, porque as concessões guardam o id das contas do Keycloak. O Geo (`geo/geo-api-database.tar.xz`)
   só é gerado de novo se o banco do Geo mudou.
5. **Prova em projeto limpo.** Derrube o ambiente com `docker compose -p base-local down -v`, e então rode `cp .env.example .env` e
   `docker compose up` numa cópia limpa do repositório, com os arquivos novos e outro nome de projeto. Confira e anexe ao PR:
   - a API responde `/health/ready` com 200, e o Geo responde em `http://localhost:5400/api/cep/79096730` com 200 e o token de uma persona (porta `GEO_API_PORT`; a API do Uni+ na porta 5200 não tem essa rota);
   - a migration em `plataforma."__EFMigrationsHistory"` é a da branch;
   - os 60 acessos de teste existem no Keycloak, e o login de uma persona funciona;
   - os perfis de operação existem, cada um com ao menos uma concessão ativa, e `diogo.souza` tem o perfil Gestor de acessos;
   - os dois processos existem, com 56 inscrições no multicurso e nenhuma na Medicina;
   - o MinIO tem os documentos dos dois processos.
6. **Atualizar o texto.** O período de inscrição citado em "Base local pronta" depende do dia da geração: atualize-o neste mesmo arquivo, no
   mesmo PR.

## Serviços e portas

Cada porta publicada vem de uma variável (coluna "Variável"), com o padrão abaixo. Para trocá-la sem editar
arquivo versionado, edite a variável no `docker/.env` (o `docker/.env.example` já traz todas, com o valor padrão)
ou defina-a no shell, que vence o arquivo.

| Serviço | Variável | Padrão | URL de acesso | Credenciais (.env.example) |
|---|---|---|---|---|
| PostgreSQL 18 | `POSTGRES_PORT` | 5432 | `Host=localhost;Port=5432` | `uniplus` / `uniplus_dev` |
| Redis 8 | `REDIS_PORT` | 6379 | `localhost:6379` | — |
| Kafka 4.2 (KRaft) | `KAFKA_PORT` | 9092 | `localhost:9092` | — |
| MinIO (API S3) | `MINIO_PORT` | 9000 | http://localhost:9000 | `minioadmin` / `minioadmin` |
| MinIO (Console) | `MINIO_CONSOLE_PORT` | 9001 | http://localhost:9001 | `minioadmin` / `minioadmin` |
| RabbitMQ (AMQP / painel) | `RABBITMQ_PORT` / `RABBITMQ_MANAGEMENT_PORT` | 5672 / 15672 | http://localhost:15672 | `uniplus` / `uniplus_dev` |
| Mailpit (SMTP / interface) | `MAILPIT_SMTP_PORT` / `MAILPIT_UI_PORT` | 1025 / 8025 | http://localhost:8025 | — (o e-mail do código de instalação cai aqui) |
| Keycloak 26.7 | `KEYCLOAK_PORT` | 8080 | http://localhost:8080 | `admin` / `admin` (master); personas do realm `unifesspa` com a senha `Uni+Teste26` |
| Apicurio Schema Registry | `APICURIO_PORT` | 8081 | http://localhost:8081 | — |
| Uni+ API (Host com os módulos internos) | `API_PORT` | 5200 (via Docker) | http://localhost:5200/health | — |
| Geo API | `GEO_API_PORT` | 5400 | http://localhost:5400 | — |

Só no stack completo (com o override de exemplo): `portal-api` (`PORTAL_API_PORT`, 5302), documentação OpenAPI
(`OPENAPI_DOCS_PORT`, 5299), gateway Traefik (`GATEWAY_PORT`, 5000; painel em `GATEWAY_DASHBOARD_PORT`, 5080) e
os frontends (`SELECAO_WEB_PORT` 4200, `INGRESSO_WEB_PORT` 4201, `PORTAL_WEB_PORT` 4202,
`CONFIGURACAO_WEB_PORT` 4203). Os frontends só autenticam nas portas padrão.

Sem porta publicada: o **verificador de arquivos** (`clamd-verificador` e `verificador`, que varrem o PDF de
designação e os arquivos do candidato), o `kafka-init` (cria o tópico do registro de acesso), o `minio-init`
(cria os buckets) e o `ambiente-init`, que terminam depois de cumprir a tarefa.

> Via `dotnet run` local (Modo 1), o `Unifesspa.UniPlus.Host` não tem `launchSettings.json` próprio — a porta usada é a padrão do Kestrel, exibida no console ao subir (`Now listening on: ...`).

## Databases PostgreSQL

O script `docker/init-db.sql` cria automaticamente:

| Database | Módulo | Extensões |
|---|---|---|
| `uniplus` | Database padrão (criado pelo Compose) | — |
| `uniplus_selecao` | Módulo Seleção | `uuid-ossp`, `pg_trgm` |
| `uniplus_ingresso` | Módulo Ingresso | `uuid-ossp`, `pg_trgm` |
| `keycloak` | Keycloak (tabelas gerenciadas pelo Keycloak) | — |

## Modos de execução

### Modo 1: API local (`dotnet run`) + infraestrutura Docker

```bash
cd docker
# Só a infraestrutura e o verificador de arquivos, sem a API do compose
docker compose up -d postgres redis kafka kafka-init minio minio-init rabbitmq mailpit apicurio keycloak \
  clamd-verificador verificador
cd ..

# Executar a API — processo único, o Host compõe Seleção, Ingresso,
# Configuração e Organização (os módulos internos são class libraries,
# não são executáveis isoladamente)
dotnet run --project src/host/Unifesspa.UniPlus.Host
```

A API usa o `appsettings.Development.json` do Host, com a connection string única (`UniPlusDb`) dos módulos internos apontando para `localhost`.

A instalação dos gestores só aceita e-mail em domínio institucional: antes do `dotnet run`, exporte no mesmo shell
as três chaves `Autorizacao__Instalacao__DominiosInstitucionais__0..2` com `unifesspa.edu.br`,
`teste.unifesspa.edu.br` e `uniplus-test.local` (comandos no
[guia da instalação dos gestores](guia-instalacao-gestores-ambiente-local.md#22-domínio-institucional-aceito-pela-api)).
Sem isso o passo 1 responde 422 `instalacao.gestores_invalidos`.

Neste modo o `ambiente-init` não roda (ele depende da API do compose). Configure o Keycloak e instale os gestores
uma vez, e de novo a cada banco recriado, com `jq` e `curl` instalados:

```bash
scripts/setup-keycloak-dev.sh
API_URL=http://localhost:<porta da API> scripts/dev-instalar-gestores.sh
```

O e-mail do código vai para o Mailpit na porta que o `appsettings.Development.json` configura; com portas
diferentes das padrão, a chave do SMTP é `Email__Smtp__Porta`.

### Modo 2: Tudo via Docker

É o setup rápido acima: `cd docker && cp .env.example .env && docker compose up --build`. As APIs rodam dentro
da rede Docker e se comunicam com os serviços pelo nome do container (`postgres`, `redis`, `kafka`, `minio`,
`keycloak`).

## Comandos úteis

Os comandos do compose rodam em `docker/`.

```bash
# Ver status dos serviços
docker compose ps

# Ver logs de um serviço específico
docker compose logs -f postgres
docker compose logs -f keycloak

# Parar todos os serviços (mantém dados)
docker compose down

# Parar e remover volumes (reset completo). O próximo `up` reconfigura o Keycloak
# e reinstala os gestores sozinho
docker compose down -v

# Rebuild da API UniPlus (monólito) após alteração de código
docker compose up -d --build uniplus-api

# Build da solution
dotnet build UniPlus.slnx

# Executar todos os testes
dotnet test UniPlus.slnx

# Executar apenas testes de arquitetura
dotnet test tests/Unifesspa.UniPlus.Selecao.ArchTests
dotnet test tests/Unifesspa.UniPlus.ArchTests
```

## Troubleshooting

### Porta já em uso

```
Error: Bind for 0.0.0.0:5432 failed: port is already allocated
```

Outro processo está usando a porta. Identifique e pare:

```bash
# Verificar qual processo usa a porta
sudo lsof -i :5432

# Parar um PostgreSQL local, por exemplo
sudo systemctl stop postgresql
```

Para manter dois ambientes na mesma máquina, ou desviar de uma porta ocupada, troque a porta no `docker/.env`
em vez de parar o outro processo (`API_PORT=5201`, `KEYCLOAK_PORT=8180`...). Num segundo ambiente, troque todas
as portas e defina `COMPOSE_PROJECT_NAME` diferente, para contêineres e volumes não se misturarem.

### O compose recusa o `.env` ou o override

`docker/.env` e `docker/docker-compose.override.yml` não são versionados: um `git pull` não os atualiza. Chave
obrigatória ausente no `.env` faz o compose parar antes de subir qualquer serviço, com a mensagem
`defina X no docker/.env`. Copie a linha do `.env.example`, ou recopie o exemplo (guarde uma cópia):

```bash
cp docker/.env docker/.env.bak && cp docker/.env.example docker/.env
```

Um `docker-compose.override.yml` de antes da API ir para o `docker-compose.yml` sobrepõe os valores dela: apague-o
(só é necessário para o stack completo, e então recopie `docker-compose.override.example.yml`).

### O ambiente sobe, mas toda rota protegida responde 403 ou 503

O `ambiente-init` falhou ou não rodou. Veja o motivo:

```bash
docker compose logs ambiente-init
```

Os casos mais comuns, o verificador parado e o tópico do registro de acesso ausente, estão na tabela do
[guia da instalação dos gestores](guia-instalacao-gestores-ambiente-local.md#3-problemas-comuns).

### PostgreSQL não inicializa (volume corrompido)

```bash
# Remover volume e recriar
docker compose down -v
docker compose up -d
```

**Atenção:** isso apaga todos os dados locais, inclusive as concessões e o realm do Keycloak. O `docker compose up -d`
seguinte reconfigura o Keycloak e reinstala os gestores.

### Keycloak demora para ficar healthy

O Keycloak leva ~50 segundos na primeira inicialização (executa migrations Liquibase). O `start_period: 60s` do health check acomoda esse tempo. Se persistir:

```bash
# Verificar logs
docker compose logs -f keycloak

# Verificar health manualmente
docker compose exec keycloak bash -c "exec 3<>/dev/tcp/localhost/9000 && echo -e 'GET /health/ready HTTP/1.1\r\nHost: localhost\r\nConnection: close\r\n\r\n' >&3 && timeout 1 cat <&3"
```

### Build Docker das APIs falha

Se a imagem `sdk:10.0` não contiver o SDK GA, o build falhará. Os Dockerfiles usam a tag pinada `sdk:10.0.400` para evitar isso — a mesma versão declarada em `global.json`, que restringe a resolução à feature band `10.0.4xx` (`rollForward: latestPatch`). Uma tag de outra band faz o `dotnet restore` falhar dentro da imagem antes do build. Se precisar atualizar:

1. Verifique a versão local: `dotnet --version`
2. Atualize `global.json` e a tag nos Dockerfiles juntos: `FROM mcr.microsoft.com/dotnet/sdk:<versão>` — as duas referências precisam ficar na mesma feature band

### Connection string inválida

Se a API não conectar no PostgreSQL, verifique:

1. A key da connection string no `appsettings.json` do host é `UniPlusDb`, uma só para todos os módulos internos (ADR-0170); ela aponta para o banco `uniplus` (schema por módulo, ADR-0097)
2. O `appsettings.Development.json` deve incluir `Username` e `Password`
3. Os valores devem coincidir com o `docker/.env`

### Permissão negada no Docker

```bash
# Adicionar usuário ao grupo docker
sudo usermod -aG docker $USER

# Relogar para aplicar
newgrp docker
```
