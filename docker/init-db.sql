-- Provisionamento dos databases dos módulos do UniPlus + extensões.
--
-- Topologia (docs/guia-banco-de-dados.md §1): um banco PostgreSQL por módulo,
-- todos no mesmo host PG (custo de dev local) mas isolados por database.
-- Selecao/Ingresso/Portal compartilham o superusuário `uniplus` (legado);
-- Parametrizacao e Organizacao (Sprint 3) usam usuários `_app` dedicados,
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
CREATE ROLE uniplus_parametrizacao_app LOGIN PASSWORD 'uniplus_dev';
CREATE DATABASE uniplus_parametrizacao OWNER uniplus_parametrizacao_app;

CREATE ROLE uniplus_organizacao_app LOGIN PASSWORD 'uniplus_dev';
CREATE DATABASE uniplus_organizacao OWNER uniplus_organizacao_app;

-- Extensões dos databases de aplicação:
--   uuid-ossp  — geração de UUIDs
--   pg_trgm    — busca por similaridade (trigram matching)
--   btree_gist — exclusion constraints GIST das junction tables de
--                AreasDeInteresse (ADR-0060). Habilitada nos bancos que
--                hospedam entidades com governança por área na Sprint 3:
--                selecao, parametrizacao e organizacao.

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

\c uniplus_parametrizacao
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pg_trgm";
CREATE EXTENSION IF NOT EXISTS btree_gist;

\c uniplus_organizacao
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pg_trgm";
CREATE EXTENSION IF NOT EXISTS btree_gist;
