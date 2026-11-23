-- Um motivo do cadastro tem no máximo uma escolha atual (ainda sem versão de saída) por fase ou etapa do processo. O índice
-- é parcial e trata os nulos como iguais, o que o modelo do EF não expressa: a fase e a etapa são colunas alternativas, e a
-- ausente é nula.

CREATE UNIQUE INDEX IF NOT EXISTS ux_motivos_de_decisao_do_processo_vigente
    ON selecao.motivos_de_decisao_do_processo (processo_seletivo_id, tipo_fase_origem_id, etapa_processo_id, motivo_de_decisao_origem_id)
    NULLS NOT DISTINCT
    WHERE versao_do_edital_de_saida IS NULL;
