-- Uma data não útil por calendário, abrangência e localidade. NULLS NOT DISTINCT faz duas datas nacionais na mesma data
-- (município e UF nulos) colidirem, como a igualdade estrutural de tupla do domínio; o índice do EF não expressa isso.
DROP INDEX IF EXISTS configuracao.ix_dia_nao_util_unicidade;

CREATE UNIQUE INDEX ix_dia_nao_util_unicidade
    ON configuracao.dia_nao_util (calendario_dias_uteis_id, data, abrangencia, municipio_ibge, uf)
    NULLS NOT DISTINCT;
