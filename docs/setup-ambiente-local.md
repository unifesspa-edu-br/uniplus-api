# Setup do ambiente de desenvolvimento local

Guia para configurar e executar o ambiente de desenvolvimento da Uni+ API.

## Pré-requisitos

| Ferramenta | Versão mínima | Verificação |
|---|---|---|
| Docker | 24+ | `docker --version` |
| Docker Compose | 2.20+ | `docker compose version` |
| .NET SDK | 10.0.100 | `dotnet --version` |
| Git | 2.40+ | `git --version` |

## Setup rápido

```bash
# 1. Clonar o repositório
git clone https://github.com/unifesspa-edu-br/uniplus-api.git
cd uniplus-api

# 2. Criar arquivo de variáveis de ambiente
cp docker/.env.example docker/.env

# 3. Subir a infraestrutura
docker compose -f docker/docker-compose.yml up -d

# 4. Verificar que todos os serviços estão healthy
docker compose -f docker/docker-compose.yml ps

# 5. (Opcional) Subir as APIs via Docker
cp docker/docker-compose.override.example.yml docker/docker-compose.override.yml
docker compose -f docker/docker-compose.yml -f docker/docker-compose.override.yml up -d --build
```

## Serviços e portas

| Serviço | Porta | URL de acesso | Credenciais (.env.example) |
|---|---|---|---|
| PostgreSQL 18 | 5432 | `Host=localhost;Port=5432` | `uniplus` / `uniplus_dev` |
| Redis 8 | 6379 | `localhost:6379` | — |
| Kafka 4.2 (KRaft) | 9092 | `localhost:9092` | — |
| MinIO (API S3) | 9000 | http://localhost:9000 | `minioadmin` / `minioadmin` |
| MinIO (Console) | 9001 | http://localhost:9001 | `minioadmin` / `minioadmin` |
| Keycloak 26.5 | 8080 | http://localhost:8080 | `admin` / `admin` |
| Seleção API | 5202 | http://localhost:5202/health | — |
| Ingresso API | 5262 | http://localhost:5262/health | — |

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
# Subir infraestrutura
docker compose -f docker/docker-compose.yml up -d

# Em um terminal — Seleção API
dotnet run --project src/selecao/Unifesspa.UniPlus.Selecao.API

# Em outro terminal — Ingresso API
dotnet run --project src/ingresso/Unifesspa.UniPlus.Ingresso.API
```

As APIs usam `appsettings.Development.json` com connection strings apontando para `localhost`.

### Modo 2: Tudo via Docker (útil para testes de integração)

```bash
# Subir infraestrutura + APIs
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

# Parar e remover volumes (reset completo)
docker compose -f docker/docker-compose.yml down -v

# Rebuild das APIs após alteração de código
docker compose -f docker/docker-compose.yml -f docker/docker-compose.override.yml up -d --build selecao-api ingresso-api

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

### PostgreSQL não inicializa (volume corrompido)

```bash
# Remover volume e recriar
docker compose -f docker/docker-compose.yml down -v
docker compose -f docker/docker-compose.yml up -d
```

**Atenção:** isso apaga todos os dados locais.

### Keycloak demora para ficar healthy

O Keycloak leva ~50 segundos na primeira inicialização (executa migrations Liquibase). O `start_period: 60s` do health check acomoda esse tempo. Se persistir:

```bash
# Verificar logs
docker compose -f docker/docker-compose.yml logs -f keycloak

# Verificar health manualmente
docker exec docker-keycloak-1 bash -c "exec 3<>/dev/tcp/localhost/9000 && echo -e 'GET /health/ready HTTP/1.1\r\nHost: localhost\r\nConnection: close\r\n\r\n' >&3 && timeout 1 cat <&3"
```

### Build Docker das APIs falha

Se a imagem `sdk:10.0` não contiver o SDK GA, o build falhará. Os Dockerfiles usam a tag pinada `sdk:10.0.100` para evitar isso. Se precisar atualizar:

1. Verifique a versão local: `dotnet --version`
2. Atualize a tag nos Dockerfiles: `FROM mcr.microsoft.com/dotnet/sdk:<versão>`

### Connection string inválida

Se a API não conectar no PostgreSQL, verifique:

1. A key da connection string no `appsettings.json` deve ser `SelecaoDb` (Seleção) ou `IngressoDb` (Ingresso)
2. O `appsettings.Development.json` deve incluir `Username` e `Password`
3. Os valores devem coincidir com o `docker/.env`

### Permissão negada no Docker

```bash
# Adicionar usuário ao grupo docker
sudo usermod -aG docker $USER

# Relogar para aplicar
newgrp docker
```
