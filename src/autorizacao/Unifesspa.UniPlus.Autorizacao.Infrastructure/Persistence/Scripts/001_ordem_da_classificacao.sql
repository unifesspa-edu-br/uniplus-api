-- A posição da classificação na escala de proteção, na ordem do enum ClassificacaoDeProtecao. As consultas de acesso e
-- as reclassificações a usam para comparar duas classificações.
CREATE OR REPLACE FUNCTION autorizacao.ordem_da_classificacao(classificacao text) RETURNS integer
LANGUAGE sql IMMUTABLE STRICT PARALLEL SAFE
RETURN CASE classificacao
    WHEN 'PUBLICO' THEN 1
    WHEN 'INTERNO' THEN 2
    WHEN 'PESSOAL' THEN 3
    WHEN 'IDENTIFICADOR' THEN 4
    WHEN 'SENSIVEL' THEN 5
END;
