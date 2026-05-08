-- Criar databases adicionais para os módulos do UniPlus
CREATE DATABASE uniplus_selecao;
CREATE DATABASE uniplus_ingresso;
CREATE DATABASE uniplus_portal;
CREATE DATABASE keycloak;

-- Instalar extensões nos databases de aplicação
-- uuid-ossp: geração de UUIDs como chave primária
-- pg_trgm: busca por similaridade (trigram matching)

\c uniplus_selecao
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pg_trgm";

\c uniplus_ingresso
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pg_trgm";

\c uniplus_portal
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pg_trgm";
