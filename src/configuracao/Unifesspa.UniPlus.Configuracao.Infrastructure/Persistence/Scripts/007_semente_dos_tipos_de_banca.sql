-- Os tipos de banca que o sistema traz, com o papel, as duas marcas e os tipos de decisão que cada um julga. Só o papel
-- que avalia o mérito declara tipos de decisão. A heteroidentificação e a avaliação biopsicossocial exigem duas ou mais
-- bancas, e só a heteroidentificação encerra o recurso na primeira instância. Cadastro administrável: a linha que já
-- existe, ou cujo código a administração já ocupou (inclusive o que ela removeu), nunca é sobrescrita nem recriada, e o
-- que a administração editou ou removeu depois não volta na reaplicação. O script roda a cada preparação do banco.
INSERT INTO configuracao.tipos_de_banca
    (id, codigo, nome, papel, exige_duas_ou_mais_bancas, encerra_recurso_na_primeira_instancia, tipos_de_decisao_julgados, created_at, is_deleted)
SELECT semente.id::uuid, semente.codigo, semente.nome, semente.papel, semente.exige_duas_ou_mais_bancas,
       semente.encerra_recurso_na_primeira_instancia, semente.tipos_de_decisao_julgados, semente.created_at, semente.is_deleted
FROM (VALUES
    ('019fee20-7000-7000-8000-000000000001', 'BANCA_ANALISE_DOCUMENTAL', 'Banca de análise documental', 'AVALIA_O_MERITO', false, false, ARRAY['ANALISE_DOCUMENTAL', 'ISENCAO_DA_TAXA']::text[], TIMESTAMPTZ '2026-11-21 00:00:00+00', false),
    ('019fee20-7000-7000-8000-000000000002', 'BANCA_ENTREVISTA', 'Banca de entrevista', 'AVALIA_O_MERITO', false, false, ARRAY['NOTA_DA_ETAPA']::text[], TIMESTAMPTZ '2026-11-21 00:00:00+00', false),
    ('019fee20-7000-7000-8000-000000000003', 'BANCA_CORRECAO_REDACOES', 'Banca de correção de redações', 'AVALIA_O_MERITO', false, false, ARRAY['NOTA_DA_ETAPA']::text[], TIMESTAMPTZ '2026-11-21 00:00:00+00', false),
    ('019fee20-7000-7000-8000-000000000004', 'BANCA_ANALISE_RECURSOS', 'Banca de análise de recursos', 'JULGA_A_PRIMEIRA_INSTANCIA', false, false, ARRAY[]::text[], TIMESTAMPTZ '2026-11-21 00:00:00+00', false),
    ('019fee20-7000-7000-8000-000000000005', 'BANCA_HETEROIDENTIFICACAO', 'Banca de heteroidentificação', 'AVALIA_O_MERITO', true, true, ARRAY['HETEROIDENTIFICACAO']::text[], TIMESTAMPTZ '2026-11-21 00:00:00+00', false),
    ('019fee20-7000-7000-8000-000000000006', 'BANCA_BIOPSICOSSOCIAL', 'Banca de avaliação biopsicossocial', 'AVALIA_O_MERITO', true, false, ARRAY['AVALIACAO_BIOPSICOSSOCIAL']::text[], TIMESTAMPTZ '2026-11-21 00:00:00+00', false),
    ('019fee20-7000-7000-8000-000000000007', 'BANCA_HOMOLOGACAO_INSCRICOES', 'Banca de homologação das inscrições', 'AVALIA_O_MERITO', false, false, ARRAY['HOMOLOGACAO_DAS_INSCRICOES']::text[], TIMESTAMPTZ '2026-11-21 00:00:00+00', false),
    ('019fee20-7000-7000-8000-000000000008', 'BANCA_IMPORTACAO_NOTAS_INEP', 'Banca de importação das notas do INEP', 'IMPORTA_AS_NOTAS_DO_INEP', false, false, ARRAY[]::text[], TIMESTAMPTZ '2026-11-21 00:00:00+00', false),
    ('019fee20-7000-7000-8000-000000000009', 'BANCA_IMPORTACAO_CANDIDATOS', 'Banca de importação de candidatos', 'IMPORTA_CANDIDATOS', false, false, ARRAY[]::text[], TIMESTAMPTZ '2026-11-21 00:00:00+00', false)
) AS semente (id, codigo, nome, papel, exige_duas_ou_mais_bancas, encerra_recurso_na_primeira_instancia, tipos_de_decisao_julgados, created_at, is_deleted)
WHERE NOT EXISTS (
    SELECT 1
    FROM configuracao.tipos_de_banca existente
    WHERE existente.codigo = semente.codigo OR existente.id = semente.id::uuid);
