-- Índices GIN trigram por campo pesquisável da Unidade, sobre immutable_unaccent.
CREATE INDEX IF NOT EXISTS idx_unidade_nome_trgm
    ON organizacao.unidade USING GIN (immutable_unaccent(nome) gin_trgm_ops);

CREATE INDEX IF NOT EXISTS idx_unidade_sigla_trgm
    ON organizacao.unidade USING GIN (immutable_unaccent(sigla) gin_trgm_ops);

CREATE INDEX IF NOT EXISTS idx_unidade_codigo_trgm
    ON organizacao.unidade USING GIN (immutable_unaccent(codigo) gin_trgm_ops);

CREATE INDEX IF NOT EXISTS idx_unidade_slug_trgm
    ON organizacao.unidade USING GIN (immutable_unaccent(slug) gin_trgm_ops);

CREATE INDEX IF NOT EXISTS idx_unidade_alias_trgm
    ON organizacao.unidade USING GIN (immutable_unaccent(alias) gin_trgm_ops);
