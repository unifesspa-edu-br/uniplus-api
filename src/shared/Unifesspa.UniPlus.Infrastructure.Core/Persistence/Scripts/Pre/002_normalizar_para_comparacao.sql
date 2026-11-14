-- A função precede as tabelas dos módulos cujas colunas geradas de ordenação a chamam: coluna gerada não referencia
-- função inexistente. IMMUTABLE é o que a habilita a servir coluna gerada e índice; STRICT dispensa tratar o nulo no
-- corpo. A tabela de substituição espelha NormalizacaoTextual.
CREATE SCHEMA IF NOT EXISTS plataforma;

CREATE OR REPLACE FUNCTION plataforma.normalizar_para_comparacao(text)
    RETURNS text
    LANGUAGE sql
    IMMUTABLE
    PARALLEL SAFE
    STRICT
    RETURN lower(translate(normalize($1, NFC),
        'ÁÀÂÃÄÅÉÈÊËÍÌÎÏÓÒÔÕÖÚÙÛÜÇÑÝáàâãäåéèêëíìîïóòôõöúùûüçñý',
        'AAAAAAEEEEIIIIOOOOOUUUUCNYaaaaaaeeeeiiiiooooouuuucny') COLLATE "C");
