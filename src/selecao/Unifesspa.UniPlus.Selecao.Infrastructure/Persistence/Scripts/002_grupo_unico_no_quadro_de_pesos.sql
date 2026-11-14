-- Um grupo de área do ENEM aparece uma vez por classificação. O índice combina a chave estrangeira com uma coluna do tipo
-- owned, o que o modelo do EF não expressa.

CREATE UNIQUE INDEX IF NOT EXISTS ix_grupos_do_quadro_de_pesos_classificacao_grupo
    ON selecao.grupos_do_quadro_de_pesos (configuracao_classificacao_id, grupo_area_enem_codigo);
