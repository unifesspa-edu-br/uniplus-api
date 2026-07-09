-- Provisionamento do banco do MONÓLITO MODULAR — variante do init-db.sql.
--
-- Topologia: banco ÚNICO `uniplus` com schema-por-módulo (vs. um banco por
-- módulo no init-db.sql padrão). Os 5 módulos internos (Selecao, Ingresso,
-- Configuracao, OrganizacaoInstitucional, Publicacoes) coabitam no banco `uniplus`, cada um
-- no seu schema; o Wolverine usa o schema `wolverine`. Geo permanece deploy
-- separado (banco `uniplus_geo` próprio, ADR-0090/0091) e não é tocado aqui.
--
-- O banco `uniplus` já é criado pelo entrypoint do container (POSTGRES_DB=uniplus,
-- dono = superusuário `uniplus`). Os 5 schemas + `wolverine` NÃO são criados aqui:
-- são materializados pelas migrations on startup do host (EnsureSchema via
-- HasDefaultSchema) e pelo AutoBuildMessageStorageOnStartup do Wolverine. Este
-- script apenas instala as extensões que as migrations assumem.
--
-- Roda como `POSTGRES_USER` (superusuário) no primeiro boot, via
-- /docker-entrypoint-initdb.d.

\c uniplus

-- Extensões usadas pelos módulos (uuid-ossp; pg_trgm/unaccent para busca
-- acento-insensível na Organizacao; btree_gist para exclusion constraints
-- temporais — ADR-0060).
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pg_trgm";
CREATE EXTENSION IF NOT EXISTS unaccent;
CREATE EXTENSION IF NOT EXISTS btree_gist;

-- Banco do Keycloak (auth) — fora do escopo do monólito, mantido por paridade
-- com o init-db.sql padrão para quem subir a stack completa.
CREATE DATABASE keycloak;

-- ISOLAMENTO — decisão em aberto para o ADR ("role-por-schema vs role única"):
-- por ora, as 5 connection strings do host usam o superusuário `uniplus` (dono
-- do banco), o que basta para bootar e provar o monólito. A variante recomendada
-- (role-por-schema: `uniplus_<modulo>` com USAGE/CREATE apenas no seu schema +
-- `search_path` dedicado, e um role de outbox no schema `wolverine`) fica para o
-- rollout — exige conceder ao role do módulo CREATE no banco para o EnsureSchema
-- da primeira migration, depois restringir ao schema.
