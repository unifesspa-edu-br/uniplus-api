-- Os tipos de etapa que o sistema traz, com o que cada um admite: a análise documental e a banca de heteroidentificação
-- eliminam ou aprovam, sem atribuir nota; a nota do Enem vem do exame, não da etapa. Cadastro administrável: a linha que
-- já existe, ou cujo código a administração já ocupou, nunca é sobrescrita.
INSERT INTO configuracao.tipos_etapa
    (id, codigo, nome, ativo, admite_pontuacao, admite_eliminacao, nota_de_origem_no_enem, created_at)
VALUES
    ('019fee1e-7000-7000-8000-000000000001', 'PROVA_OBJETIVA', 'Prova Objetiva', true, true, true, false, TIMESTAMPTZ '2026-08-11 00:00:00+00'),
    ('019fee1e-7000-7000-8000-000000000002', 'REDACAO', 'Redação', true, true, true, false, TIMESTAMPTZ '2026-08-11 00:00:00+00'),
    ('019fee1e-7000-7000-8000-000000000003', 'ENTREVISTA', 'Entrevista', true, true, true, false, TIMESTAMPTZ '2026-08-11 00:00:00+00'),
    ('019fee1e-7000-7000-8000-000000000004', 'ANALISE_HISTORICO', 'Análise de Histórico', true, true, true, false, TIMESTAMPTZ '2026-08-11 00:00:00+00'),
    ('019fee1e-7000-7000-8000-000000000005', 'BANCA_HETEROIDENTIFICACAO', 'Banca de Heteroidentificação', true, false, true, false, TIMESTAMPTZ '2026-08-11 00:00:00+00'),
    ('019fee1e-7000-7000-8000-000000000006', 'ANALISE_DOCUMENTAL', 'Análise Documental', true, false, true, false, TIMESTAMPTZ '2026-08-11 00:00:00+00'),
    ('019fee1e-7000-7000-8000-000000000007', 'NOTA_ENEM', 'Nota do Enem', true, true, true, true, TIMESTAMPTZ '2026-08-11 00:00:00+00')
ON CONFLICT DO NOTHING;
