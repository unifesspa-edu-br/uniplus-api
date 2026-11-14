-- Uma data não útil por calendário, abrangência e localidade. NULLS NOT DISTINCT faz duas datas nacionais na mesma data
-- (município e UF nulos) colidirem, como a igualdade estrutural de tupla do domínio; o índice do EF não expressa isso.

CREATE UNIQUE INDEX IF NOT EXISTS ix_dias_nao_uteis_unicidade
    ON configuracao.dias_nao_uteis (calendario_dias_uteis_id, data, abrangencia, municipio_ibge, uf)
    NULLS NOT DISTINCT;
