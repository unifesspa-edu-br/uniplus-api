-- Um grupo de área do ENEM aparece uma vez por classificação. O índice combina a chave estrangeira com uma coluna do tipo
-- owned, o que o modelo do EF não expressa.
DROP INDEX IF EXISTS selecao.ix_grupos_peso_area_enem_congelados_classificacao_grupo;

CREATE UNIQUE INDEX ix_grupos_peso_area_enem_congelados_classificacao_grupo
    ON selecao.grupos_peso_area_enem_congelados (configuracao_classificacao_id, grupo_area_enem_codigo);
