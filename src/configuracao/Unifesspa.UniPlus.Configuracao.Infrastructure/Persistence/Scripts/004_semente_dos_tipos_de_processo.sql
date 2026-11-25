-- Os tipos de processo seletivo que o sistema traz. Cadastro administrável: a linha que já existe, ou cujo código a
-- administração já ocupou, nunca é sobrescrita. O instante fixo mantém a semente reproduzível.
INSERT INTO configuracao.tipos_processo (id, codigo, nome, ativo, created_at)
VALUES
    ('019fe8f8-1400-7000-8000-000000000001', 'SISU', 'SiSU', true, TIMESTAMPTZ '2026-08-10 00:00:00+00'),
    ('019fe8f8-1400-7000-8000-000000000002', 'INDIGENA_E_QUILOMBOLA', 'Processo Seletivo Indígena e Quilombola', true, TIMESTAMPTZ '2026-08-10 00:00:00+00'),
    ('019fe8f8-1400-7000-8000-000000000003', 'EDUCACAO_DO_CAMPO', 'Processo Seletivo Especial do Campo', true, TIMESTAMPTZ '2026-08-10 00:00:00+00'),
    ('019fe8f8-1400-7000-8000-000000000004', 'VAGAS_REMANESCENTES', 'Processo Seletivo de Vagas Remanescentes', true, TIMESTAMPTZ '2026-08-10 00:00:00+00'),
    ('019fe8f8-1400-7000-8000-000000000005', 'TRANSFERENCIA_INTERNA', 'Transferência Interna', true, TIMESTAMPTZ '2026-08-10 00:00:00+00'),
    ('019fe8f8-1400-7000-8000-000000000006', 'TRANSFERENCIA_EXTERNA', 'Transferência Externa', true, TIMESTAMPTZ '2026-08-10 00:00:00+00'),
    ('019fe8f8-1400-7000-8000-000000000007', 'PORTADOR_DE_DIPLOMA', 'Portador de Diploma', true, TIMESTAMPTZ '2026-08-10 00:00:00+00'),
    ('019fe8f8-1400-7000-8000-000000000008', 'REOPCAO', 'Reopção', true, TIMESTAMPTZ '2026-08-10 00:00:00+00')
ON CONFLICT DO NOTHING;
