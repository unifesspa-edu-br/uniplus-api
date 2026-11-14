-- Índices GIN trigram por campo pesquisável da Unidade, sobre immutable_unaccent.
CREATE INDEX IF NOT EXISTS idx_unidades_nome_trgm
    ON organizacao.unidades USING GIN (immutable_unaccent(nome) gin_trgm_ops);

CREATE INDEX IF NOT EXISTS idx_unidades_sigla_trgm
    ON organizacao.unidades USING GIN (immutable_unaccent(sigla) gin_trgm_ops);

CREATE INDEX IF NOT EXISTS idx_unidades_codigo_trgm
    ON organizacao.unidades USING GIN (immutable_unaccent(codigo) gin_trgm_ops);

CREATE INDEX IF NOT EXISTS idx_unidades_slug_trgm
    ON organizacao.unidades USING GIN (immutable_unaccent(slug) gin_trgm_ops);

CREATE INDEX IF NOT EXISTS idx_unidades_alias_trgm
    ON organizacao.unidades USING GIN (immutable_unaccent(alias) gin_trgm_ops);
