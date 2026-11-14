-- Os tipos de processo seletivo que o sistema traz. Cadastro administrável: a linha que já existe, ou cujo código a
-- administração já ocupou, nunca é sobrescrita. O instante fixo mantém a semente reproduzível.
INSERT INTO configuracao.tipos_processo (id, codigo, nome, ativo, created_at)
VALUES
    ('019fe8f8-1400-7000-8000-000000000001', 'SiSU', 'SiSU', true, TIMESTAMPTZ '2026-08-10 00:00:00+00'),
    ('019fe8f8-1400-7000-8000-000000000002', 'PSIQ', 'Processo Seletivo Indígena e Quilombola', true, TIMESTAMPTZ '2026-08-10 00:00:00+00'),
    ('019fe8f8-1400-7000-8000-000000000003', 'PSECampo', 'Processo Seletivo Especial do Campo', true, TIMESTAMPTZ '2026-08-10 00:00:00+00'),
    ('019fe8f8-1400-7000-8000-000000000004', 'PSVR', 'Processo Seletivo Via Reserva', true, TIMESTAMPTZ '2026-08-10 00:00:00+00'),
    ('019fe8f8-1400-7000-8000-000000000005', 'TransferenciaInterna', 'Transferência Interna', true, TIMESTAMPTZ '2026-08-10 00:00:00+00'),
    ('019fe8f8-1400-7000-8000-000000000006', 'TransferenciaExterna', 'Transferência Externa', true, TIMESTAMPTZ '2026-08-10 00:00:00+00'),
    ('019fe8f8-1400-7000-8000-000000000007', 'PortadorDiploma', 'Portador de Diploma', true, TIMESTAMPTZ '2026-08-10 00:00:00+00'),
    ('019fe8f8-1400-7000-8000-000000000008', 'Reopcao', 'Reopção', true, TIMESTAMPTZ '2026-08-10 00:00:00+00')
ON CONFLICT DO NOTHING;
