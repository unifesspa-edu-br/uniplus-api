-- A posição da classificação na escala de proteção, na ordem do enum ClassificacaoDeProtecao. As consultas de acesso e
-- as reclassificações a usam para comparar duas classificações.
CREATE OR REPLACE FUNCTION autorizacao.ordem_da_classificacao(classificacao text) RETURNS integer
LANGUAGE sql IMMUTABLE STRICT PARALLEL SAFE
RETURN CASE classificacao
    WHEN 'nenhuma' THEN 0
    WHEN 'publico' THEN 1
    WHEN 'interno' THEN 2
    WHEN 'pessoal' THEN 3
    WHEN 'identificador' THEN 4
    WHEN 'sensivel' THEN 5
END;
