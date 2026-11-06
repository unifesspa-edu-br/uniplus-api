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

A API usa o `appsettings.Development.json` do Host, com connection strings de todos os módulos apontando para `localhost`.

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
dotnet test tests/Unifesspa.UniPlus.Ingresso.ArchTests
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

1. A key da connection string no `appsettings.json` deve ser a do módulo: `SelecaoDb`, `IngressoDb`, `ConfiguracaoDb`, `OrganizacaoDb` ou `PublicacoesDb` — todas apontam para o banco `uniplus` (schema por módulo, ADR-0097)
2. O `appsettings.Development.json` deve incluir `Username` e `Password`
3. Os valores devem coincidir com o `docker/.env`

### Permissão negada no Docker

```bash
# Adicionar usuário ao grupo docker
sudo usermod -aG docker $USER

# Relogar para aplicar
newgrp docker
```
