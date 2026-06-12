-- Provisionamento dos databases dos módulos do UniPlus + extensões.
--
-- Topologia (docs/guia-banco-de-dados.md §1): um banco PostgreSQL por módulo,
-- todos no mesmo host PG (custo de dev local) mas isolados por database.
-- Selecao/Ingresso/Portal compartilham o superusuário `uniplus` (legado);
-- Configuracao e Organizacao (Sprint 3) usam usuários `_app` dedicados,
-- cada um dono (OWNER) do seu próprio banco.
--
-- Este script roda como `POSTGRES_USER` (superusuário) no primeiro boot do
-- container, via /docker-entrypoint-initdb.d.

-- Bancos legados (módulos pré-Sprint 3) — dono: superusuário `uniplus`.
CREATE DATABASE uniplus_selecao;
CREATE DATABASE uniplus_ingresso;
CREATE DATABASE uniplus_portal;
CREATE DATABASE keycloak;

-- Bancos da Sprint 3 — usuários isolados, dono do próprio banco.
-- OWNER (não apenas GRANT) é necessário: a partir do PostgreSQL 15 o schema
-- `public` deixou de conceder CREATE implicitamente, então um usuário com
-- apenas GRANT no database não consegue criar tabelas/índices. Como dono, o
-- usuário `_app` tem DDL completo no seu banco e instala extensões trusted
-- (btree_gist, uuid-ossp, pg_trgm) sem precisar de superusuário.
--
-- A senha `uniplus_dev` abaixo é dev-only (mesmo padrão de POSTGRES_PASSWORD).
-- Em standalone/HML/PROD os usuários `_app` são provisionados com segredos
-- reais via o RUNBOOK do uniplus-infra — nunca esta senha.
CREATE ROLE uniplus_configuracao_app LOGIN PASSWORD 'uniplus_dev';
CREATE DATABASE uniplus_configuracao OWNER uniplus_configuracao_app;

CREATE ROLE uniplus_organizacao_app LOGIN PASSWORD 'uniplus_dev';
CREATE DATABASE uniplus_organizacao OWNER uniplus_organizacao_app;

-- Extensões dos databases de aplicação:
--   uuid-ossp  — geração de UUIDs
--   pg_trgm    — busca por similaridade (trigram matching)
--   btree_gist — pré-requisito de exclusion constraints GIST em junction
--                tables temporais (ADR-0060). A junction de áreas de interesse
--                (primeira aplicação) saiu no KILL do eixo de Área (Epic #600);
--                a extensão segue provisionada em selecao, configuracao e
--                organizacao para junctions temporais futuras.

\c uniplus_selecao
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pg_trgm";
CREATE EXTENSION IF NOT EXISTS btree_gist;

\c uniplus_ingresso
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pg_trgm";

\c uniplus_portal
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pg_trgm";

\c uniplus_configuracao
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pg_trgm";
CREATE EXTENSION IF NOT EXISTS btree_gist;

\c uniplus_organizacao
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pg_trgm";
CREATE EXTENSION IF NOT EXISTS btree_gist;
