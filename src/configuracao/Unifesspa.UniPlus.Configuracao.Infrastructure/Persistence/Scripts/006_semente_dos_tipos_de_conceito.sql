-- Os tipos de conceito que o sistema traz e os tipos de etapa que os admitem. O NOTA mede numa escala de 0 a 1000, que
-- comporta a nota de uma etapa de até 100 pontos e a do Enem; o APTO_INAPTO escolhe entre dois valores, e só o apto conta
-- como aprovação. Toda etapa que pontua admite o NOTA, e toda etapa que elimina admite o APTO_INAPTO. Cadastro
-- administrável: o tipo que já existe, ou cujo código a administração já ocupou, nunca é sobrescrito, e os valores e os
-- vínculos só são criados junto com o tipo que este script acabou de criar. Assim, o que a administração removeu ou
-- editou depois não volta na reaplicação.
WITH tipos_novos AS (
    INSERT INTO configuracao.tipos_conceito
        (id, codigo, nome, ativo, natureza, escala_minima, escala_maxima, casas_decimais, created_at)
    VALUES
        ('019fee1f-7000-7000-8000-000000000001', 'NOTA', 'Nota', true, 'NUMERICO', 0, 1000, 2, TIMESTAMPTZ '2026-11-14 00:00:00+00'),
        ('019fee1f-7000-7000-8000-000000000002', 'APTO_INAPTO', 'Apto ou inapto', true, 'CATEGORICO', NULL, NULL, NULL, TIMESTAMPTZ '2026-11-14 00:00:00+00')
    ON CONFLICT DO NOTHING
    RETURNING id, codigo
), valores_novos AS (
    INSERT INTO configuracao.valores_tipo_conceito
        (tipo_conceito_id, codigo, rotulo, conta_como_aprovacao, posicao)
    SELECT tipo.id, valor.codigo, valor.rotulo, valor.conta_como_aprovacao, valor.posicao
    FROM (VALUES
        ('APTO_INAPTO', 'APTO', 'Apto', true, 1),
        ('APTO_INAPTO', 'INAPTO', 'Inapto', false, 2)
    ) AS valor (tipo_codigo, codigo, rotulo, conta_como_aprovacao, posicao)
    JOIN tipos_novos tipo ON tipo.codigo = valor.tipo_codigo
)
INSERT INTO configuracao.tipos_etapa_conceitos (tipo_etapa_id, tipo_conceito_id)
SELECT etapa.id, tipo.id
FROM tipos_novos tipo
JOIN configuracao.tipos_etapa etapa
    ON etapa.codigo IN (
        'PROVA_OBJETIVA', 'REDACAO', 'ENTREVISTA', 'ANALISE_HISTORICO', 'BANCA_HETEROIDENTIFICACAO', 'ANALISE_DOCUMENTAL', 'NOTA_ENEM')
WHERE (tipo.codigo = 'NOTA' AND etapa.admite_pontuacao)
   OR (tipo.codigo = 'APTO_INAPTO' AND etapa.admite_eliminacao);
