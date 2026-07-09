-- Provisionamento dos databases das 3 APIs do UniPlus + extensões.
--
-- Topologia (3 APIs executáveis):
--   * uniplus       — MONÓLITO modular (Selecao + Ingresso + Configuracao +
--                     OrganizacaoInstitucional), banco ÚNICO com schema-por-módulo
--                     (HasDefaultSchema) + schema `wolverine` para o outbox. Dono:
--                     superusuário `uniplus`. Criado pelo entrypoint do container
--                     (POSTGRES_DB=uniplus); aqui só instalamos as extensões. Os
--                     schemas são materializados pelas migrations on startup do host.
--   * uniplus_portal — Portal (deploy autônomo), dono: superusuário `uniplus`.
--   * uniplus_geo    — Geo (deploy autônomo, read-mostly com PostGIS), dono:
--                     usuário `_app` dedicado (ADR-0090/0091).
--
-- Este script roda como `POSTGRES_USER` (superusuário) no primeiro boot do
-- container, via /docker-entrypoint-initdb.d.

-- Bancos das APIs autônomas + auth.
CREATE DATABASE uniplus_portal;
CREATE DATABASE keycloak;

-- Módulo Geo (Epic Geo) — banco isolado read-mostly com PostGIS (ADR-0090/0091).
-- A extensão `postgis` NÃO é trusted (exige superusuário para criar). Por isso é
-- criada AQUI, no init-db, que roda como superusuário (POSTGRES_USER). A migration
-- do Geo (rodada na subida da API pelo usuário não-superusuário `uniplus_geo_app`)
-- também emite `CREATE EXTENSION IF NOT EXISTS postgis` — que é no-op seguro
-- quando a extensão já existe (o `IF NOT EXISTS` retorna cedo, antes do check de
-- privilégio). Em Testcontainers (sem este init-db) a conexão é superusuário, então
-- a migration cria de fato. Ver ADR-0091.
--
-- A senha `uniplus_dev` abaixo é dev-only (mesmo padrão de POSTGRES_PASSWORD).
-- Em standalone/HML/PROD os usuários `_app` são provisionados com segredos
-- reais via o RUNBOOK do uniplus-infra — nunca esta senha.
CREATE ROLE uniplus_geo_app LOGIN PASSWORD 'uniplus_dev';
CREATE DATABASE uniplus_geo OWNER uniplus_geo_app;

-- Banco de staging da DNE (unifesspa-geo-api#12): recebe os 15 dumps brutos
-- (Correios/IBGE, gerados via Navicat) via `psql -f` sem nenhuma reescrita de
-- schema — os dumps qualificam "public"."tbl_cep_..." explicitamente, então só
-- um "public" NATIVO (banco próprio, não um schema secundário do uniplus_geo)
-- os recebe de forma transparente. Role própria (não uniplus_geo_app) por
-- menor privilégio: o app do domínio nunca precisa enxergar o dataset bruto.
CREATE ROLE uniplus_geo_staging_app LOGIN PASSWORD 'uniplus_dev';
CREATE DATABASE uniplus_geo_staging OWNER uniplus_geo_staging_app;

-- Extensões dos databases de aplicação:
--   uuid-ossp  — geração de UUIDs
--   pg_trgm    — busca por similaridade (trigram matching)
--   unaccent   — busca acento-insensível (Organizacao)
--   btree_gist — pré-requisito de exclusion constraints GIST em junction
--                tables temporais (ADR-0060), provisionado para uso futuro.

-- Monólito: união das extensões que os 5 módulos internos assumem nas migrations.
\c uniplus
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pg_trgm";
CREATE EXTENSION IF NOT EXISTS unaccent;
CREATE EXTENSION IF NOT EXISTS btree_gist;

\c uniplus_portal
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pg_trgm";
CREATE EXTENSION IF NOT EXISTS unaccent;

\c uniplus_geo
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pg_trgm";
CREATE EXTENSION IF NOT EXISTS unaccent;
CREATE EXTENSION IF NOT EXISTS btree_gist;
-- PostGIS: georreferência nacional do Geo (ADR-0091). Criada pelo superusuário
-- do init-db; é o pré-requisito do tipo geography(Point,4326) da migration.
CREATE EXTENSION IF NOT EXISTS postgis;
