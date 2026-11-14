-- Duas versões vivas do mesmo código não podem valer no mesmo dia: a pergunta "o que este código significava nesta
-- data" tem de ter uma resposta. Um guard no handler não sustenta a invariante, porque entre a consulta e o SaveChanges
-- cabe uma transação concorrente. A janela é semiaberta `[inicio, fim)`, então encerrar uma versão no dia em que a
-- sucessora começa é aceito; `vigencia_fim` nula produz `[inicio,)`. O EXCLUDE não é expressável pelo modelo do EF.
ALTER TABLE publicacoes.tipo_ato_publicado
    DROP CONSTRAINT IF EXISTS ex_tipo_ato_publicado_codigo_vigencia;

ALTER TABLE publicacoes.tipo_ato_publicado
    ADD CONSTRAINT ex_tipo_ato_publicado_codigo_vigencia
    EXCLUDE USING gist (
        codigo WITH =,
        daterange(vigencia_inicio, vigencia_fim, '[)') WITH &&
    ) WHERE (is_deleted = false)
    DEFERRABLE INITIALLY IMMEDIATE;
