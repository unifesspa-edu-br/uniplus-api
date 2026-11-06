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

Um clone novo chega a **API no ar, Keycloak configurado e dois gestores de acessos instalados** com um comando,
a partir da raiz do repositório:

```bash
git clone https://github.com/unifesspa-edu-br/uniplus-api.git
cd uniplus-api
scripts/dev-up.sh
```

O script cria `docker/.env` e `docker/docker-compose.override.yml` a partir dos exemplos (só valores de
desenvolvimento), acusa a chave ou o serviço que faltar numa cópia antiga, sobe o compose e acompanha o serviço
`ambiente-init` até o ambiente ficar pronto. Rodar de novo não altera nada; no fim ele mostra as URLs da API, do
Keycloak e do Mailpit. O primeiro `up` constrói imagens e o clamd baixa a base de assinaturas: conte alguns minutos.

O `ambiente-init` é um serviço do próprio compose, que configura o Keycloak (senha do `admin`, personas
fictícias, papel de instalação) e instala os dois gestores pela API. Por isso, com os dois arquivos locais já
criados, **o `docker compose up -d` sozinho dá o mesmo resultado**:

```bash
cp docker/.env.example docker/.env
cp docker/docker-compose.override.example.yml docker/docker-compose.override.yml
docker compose -f docker/docker-compose.yml -f docker/docker-compose.override.yml up -d --build
# acompanhar a configuração e a instalação:
docker compose -f docker/docker-compose.yml -f docker/docker-compose.override.yml logs -f ambiente-init
```

Sem a instalação dos gestores ninguém acessa nada além da área do candidato: o token não concede acesso, toda
permissão vem de uma concessão gravada no Uni+ ([ADR-0152](adrs/0152-governanca-das-concessoes-de-acesso.md)).
Detalhes, o procedimento manual e a solução dos problemas comuns estão no
[guia da instalação dos gestores](guia-instalacao-gestores-ambiente-local.md).

Variações de `scripts/dev-up.sh`:

| Opção | Efeito |
|---|---|
| `--completo` | Sobe também `portal-api`, gateway Traefik e os quatro frontends (exige o `uniplus-web` clonado ao lado). |
| `--sem-instalacao` | Configura o Keycloak, sem instalar os gestores. |
| `--deslocamento-de-portas N` | Soma `N` a todas as portas publicadas e usa um projeto do compose próprio: um segundo ambiente na mesma máquina. |
| `--so-validar` | Só confere `.env`, override e compose, sem subir nada. |

## Serviços e portas

Cada porta publicada vem de uma variável (coluna "Variável"), com o padrão abaixo. Para trocá-la sem editar
arquivo versionado, edite a variável no `docker/.env` (o `docker/.env.example` já traz todas, com o valor padrão)
ou defina-a no shell, que vence o arquivo; ou use `scripts/dev-up.sh --deslocamento-de-portas N`.

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

Só no modo completo (`--completo`): `portal-api` (`PORTAL_API_PORT`, 5302), documentação OpenAPI
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

### Modo 1: APIs locais + infraestrutura Docker (recomendado para desenvolvimento)

```bash
# Subir infraestrutura e verificador de arquivos (sem o override, não há API nem ambiente-init no Docker)
docker compose -f docker/docker-compose.yml up -d

# Executar a API — processo único, o Host compõe Seleção, Ingresso,
# Configuração e Organização (os módulos internos são class libraries,
# não são executáveis isoladamente)
dotnet run --project src/host/Unifesspa.UniPlus.Host
```

A API usa o `appsettings.Development.json` do Host, com connection strings de todos os módulos apontando para `localhost`.

Neste modo nada configura o Keycloak nem instala os gestores (o `ambiente-init` pertence ao override e depende
da API do compose). Faça-o uma vez, e de novo a cada banco recriado, com `jq` e `curl` instalados:

```bash
scripts/setup-keycloak-dev.sh
API_URL=http://localhost:<porta da API> scripts/dev-instalar-gestores.sh
```

O e-mail do código vai para o Mailpit na porta que o `appsettings.Development.json` configura; com portas
diferentes das padrão, a chave do SMTP é `Email__Smtp__Porta`. O verificador de arquivos, que a instalação exige
para liberar o PDF, já sobe com a infraestrutura (`clamd-verificador` e `verificador` estão no
`docker-compose.yml`).

### Modo 2: Tudo via Docker (útil para testes de integração)

```bash
# Infraestrutura + APIs + verificador + ambiente-init (ou scripts/dev-up.sh)
docker compose -f docker/docker-compose.yml -f docker/docker-compose.override.yml up -d --build
```

As APIs rodam dentro da rede Docker e se comunicam com os serviços pelo nome do container (`postgres`, `redis`, `kafka`, `minio`, `keycloak`).

## Comandos úteis

```bash
# Ver status dos serviços
docker compose -f docker/docker-compose.yml ps

# Ver logs de um serviço específico
docker compose -f docker/docker-compose.yml logs -f postgres
docker compose -f docker/docker-compose.yml logs -f keycloak

# Parar todos os serviços (mantém dados)
docker compose -f docker/docker-compose.yml down

# Parar e remover volumes (reset completo). Com o override, o próximo `up -d` (ou
# scripts/dev-up.sh) reconfigura o Keycloak e reinstala os gestores sozinho
docker compose -f docker/docker-compose.yml -f docker/docker-compose.override.yml down -v

# Rebuild da API UniPlus (monólito) após alteração de código
docker compose -f docker/docker-compose.yml -f docker/docker-compose.override.yml up -d --build uniplus-api

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

Para manter dois ambientes na mesma máquina, ou desviar de uma porta ocupada, troque a porta por variável em
vez de parar o outro processo:

```bash
scripts/dev-up.sh --deslocamento-de-portas 100   # tudo +100, projeto do compose próprio
API_PORT=5201 KEYCLOAK_PORT=8180 docker compose ... up -d   # ou só as que colidem
```

### `scripts/dev-up.sh` aponta `docker/.env` ou o override desatualizado

`docker/.env` e `docker/docker-compose.override.yml` não são versionados: um `git pull` não os atualiza. O script
para antes de subir qualquer serviço e nomeia a chave, a variável de configuração ou o serviço (`ambiente-init`)
que falta. Copie o trecho do `.env.example` ou do override de exemplo, ou recopie o exemplo (guarde uma cópia):

```bash
cp docker/.env docker/.env.bak && cp docker/.env.example docker/.env
cp docker/docker-compose.override.yml docker/docker-compose.override.yml.bak
cp docker/docker-compose.override.example.yml docker/docker-compose.override.yml
```

### O ambiente sobe, mas toda rota protegida responde 403 ou 503

O `ambiente-init` falhou ou não rodou (override antigo sem o serviço). Veja o motivo:

```bash
docker compose -f docker/docker-compose.yml -f docker/docker-compose.override.yml logs ambiente-init
```

Os casos mais comuns, o verificador parado e o tópico do registro de acesso ausente, estão na tabela do
[guia da instalação dos gestores](guia-instalacao-gestores-ambiente-local.md#3-problemas-comuns).

### PostgreSQL não inicializa (volume corrompido)

```bash
# Remover volume e recriar
docker compose -f docker/docker-compose.yml down -v
docker compose -f docker/docker-compose.yml up -d
```

**Atenção:** isso apaga todos os dados locais, inclusive as concessões e o realm do Keycloak. Com o override, rode
`scripts/dev-up.sh` (ou `docker compose ... up -d`) para reconfigurar e reinstalar os gestores.

### Keycloak demora para ficar healthy

O Keycloak leva ~50 segundos na primeira inicialização (executa migrations Liquibase). O `start_period: 60s` do health check acomoda esse tempo. Se persistir:

```bash
# Verificar logs
docker compose -f docker/docker-compose.yml logs -f keycloak

# Verificar health manualmente
docker compose -f docker/docker-compose.yml exec keycloak bash -c "exec 3<>/dev/tcp/localhost/9000 && echo -e 'GET /health/ready HTTP/1.1\r\nHost: localhost\r\nConnection: close\r\n\r\n' >&3 && timeout 1 cat <&3"
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
