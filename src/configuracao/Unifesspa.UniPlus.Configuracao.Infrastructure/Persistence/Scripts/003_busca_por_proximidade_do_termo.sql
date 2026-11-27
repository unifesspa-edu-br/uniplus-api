-- Busca por proximidade do catálogo de termos de consentimento por nome, sem varredura completa. O ILIKE da busca já
-- ignora a caixa, então o índice é sobre a própria coluna; ele é parcial por is_deleted, como ix_termos_de_consentimento_nome.
-- O índice parcial não é expressável pelo HasIndex do EF; gin_trgm_ops vem da extensão pg_trgm, que o modelo declara.

-- Um índice sobre lower(nome) com esse nome não atende o ILIKE: sai antes do CREATE, para que o banco que já o tem fique
-- com o índice certo.
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_indexes
               WHERE schemaname = 'configuracao' AND indexname = 'ix_termos_de_consentimento_nome_trgm'
                 AND indexdef LIKE '%lower(%') THEN
        DROP INDEX configuracao.ix_termos_de_consentimento_nome_trgm;
    END IF;
END
$$;

CREATE INDEX IF NOT EXISTS ix_termos_de_consentimento_nome_trgm
    ON configuracao.termos_de_consentimento USING GIN (nome gin_trgm_ops)
    WHERE is_deleted = false;
