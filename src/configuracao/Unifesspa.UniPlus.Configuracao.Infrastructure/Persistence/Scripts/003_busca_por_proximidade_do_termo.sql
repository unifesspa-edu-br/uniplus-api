-- Busca por proximidade do catálogo de termos de consentimento por nome, sem varredura completa. lower() é imutável sem
-- wrapper; o índice é parcial por is_deleted, como ix_termo_consentimento_nome. O índice de expressão não é expressável
-- pelo HasIndex do EF; gin_trgm_ops vem da extensão pg_trgm, que o modelo declara.

CREATE INDEX IF NOT EXISTS ix_termo_consentimento_nome_trgm
    ON configuracao.termo_consentimento USING GIN (lower(nome) gin_trgm_ops)
    WHERE is_deleted = false;
