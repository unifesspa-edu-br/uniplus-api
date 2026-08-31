using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SemeiaCatalogoTiposDocumento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // INSERT tolerante em vez do InsertData gerado pelo EF: o cadastro
            // administrativo existe desde antes desta carga, então um ambiente pode já
            // ter um tipo vivo ocupando um destes códigos. O índice único parcial
            // rejeitaria a linha do seed e a falha travaria a migração inteira — e, com
            // ela, o deploy. Pular a linha em conflito preserva o que o operador
            // cadastrou e mantém o catálogo completo em toda base que ainda não o tinha.
            // Mesmo tratamento de SemeiaCategoriasDocumento.
            migrationBuilder.Sql(
                """
                INSERT INTO configuracao.tipo_documento
                       (id, codigo, nome, categoria, created_at, is_deleted)
                VALUES
                       ('d0c00000-0000-7000-8000-000000000001', 'RG', 'RG', 'IDENTIFICACAO', TIMESTAMPTZ '2026-01-01 00:00:00+00', false),
                       ('d0c00000-0000-7000-8000-000000000002', 'CPF', 'CPF', 'IDENTIFICACAO', TIMESTAMPTZ '2026-01-01 00:00:00+00', false),
                       ('d0c00000-0000-7000-8000-000000000003', 'TITULO_ELEITOR', 'Título de eleitor', 'IDENTIFICACAO', TIMESTAMPTZ '2026-01-01 00:00:00+00', false),
                       ('d0c00000-0000-7000-8000-000000000004', 'QUITACAO_ELEITORAL', 'Comprovante de quitação eleitoral', 'IDENTIFICACAO', TIMESTAMPTZ '2026-01-01 00:00:00+00', false),
                       ('d0c00000-0000-7000-8000-000000000005', 'QUITACAO_SERVICO_MILITAR', 'Quitação com o serviço militar', 'IDENTIFICACAO', TIMESTAMPTZ '2026-01-01 00:00:00+00', false),
                       ('d0c00000-0000-7000-8000-000000000006', 'CERTIDAO_NASCIMENTO', 'Certidão de nascimento', 'IDENTIFICACAO', TIMESTAMPTZ '2026-01-01 00:00:00+00', false),
                       ('d0c00000-0000-7000-8000-000000000007', 'CERTIDAO_CASAMENTO', 'Certidão de casamento', 'IDENTIFICACAO', TIMESTAMPTZ '2026-01-01 00:00:00+00', false),
                       ('d0c00000-0000-7000-8000-000000000008', 'FOTO_3X4', 'Foto 3x4', 'IDENTIFICACAO', TIMESTAMPTZ '2026-01-01 00:00:00+00', false),
                       ('d0c00000-0000-7000-8000-000000000009', 'FOTO_FRENTE', 'Foto de frente', 'IDENTIFICACAO', TIMESTAMPTZ '2026-01-01 00:00:00+00', false),
                       ('d0c00000-0000-7000-8000-000000000010', 'FOTO_PERFIL_DIREITO', 'Foto de perfil do lado direito', 'IDENTIFICACAO', TIMESTAMPTZ '2026-01-01 00:00:00+00', false),
                       ('d0c00000-0000-7000-8000-000000000011', 'FOTO_PERFIL_ESQUERDO', 'Foto de perfil do lado esquerdo', 'IDENTIFICACAO', TIMESTAMPTZ '2026-01-01 00:00:00+00', false),
                       ('d0c00000-0000-7000-8000-000000000012', 'HISTORICO_ENSINO_FUNDAMENTAL', 'Histórico do ensino fundamental', 'ESCOLARIDADE', TIMESTAMPTZ '2026-01-01 00:00:00+00', false),
                       ('d0c00000-0000-7000-8000-000000000013', 'HISTORICO_ENSINO_MEDIO', 'Histórico do ensino médio', 'ESCOLARIDADE', TIMESTAMPTZ '2026-01-01 00:00:00+00', false),
                       ('d0c00000-0000-7000-8000-000000000014', 'CERTIFICADO_ENSINO_MEDIO', 'Certificado de conclusão do ensino médio', 'ESCOLARIDADE', TIMESTAMPTZ '2026-01-01 00:00:00+00', false),
                       ('d0c00000-0000-7000-8000-000000000015', 'HISTORICO_GRADUACAO', 'Histórico de graduação', 'ESCOLARIDADE', TIMESTAMPTZ '2026-01-01 00:00:00+00', false),
                       ('d0c00000-0000-7000-8000-000000000016', 'DIPLOMA_GRADUACAO', 'Diploma de graduação', 'ESCOLARIDADE', TIMESTAMPTZ '2026-01-01 00:00:00+00', false),
                       ('d0c00000-0000-7000-8000-000000000017', 'DIPLOMA_POS_GRADUACAO', 'Diploma de mestrado ou doutorado', 'ESCOLARIDADE', TIMESTAMPTZ '2026-01-01 00:00:00+00', false),
                       ('d0c00000-0000-7000-8000-000000000018', 'MATRIZ_CURRICULAR', 'Matriz curricular', 'ESCOLARIDADE', TIMESTAMPTZ '2026-01-01 00:00:00+00', false),
                       ('d0c00000-0000-7000-8000-000000000019', 'DECLARACAO_VINCULO_INSTITUCIONAL', 'Declaração de vínculo institucional', 'ESCOLARIDADE', TIMESTAMPTZ '2026-01-01 00:00:00+00', false),
                       ('d0c00000-0000-7000-8000-000000000020', 'CONTRACHEQUE', 'Contracheque', 'RENDA', TIMESTAMPTZ '2026-01-01 00:00:00+00', false),
                       ('d0c00000-0000-7000-8000-000000000021', 'CARTEIRA_TRABALHO', 'Carteira de trabalho', 'RENDA', TIMESTAMPTZ '2026-01-01 00:00:00+00', false),
                       ('d0c00000-0000-7000-8000-000000000022', 'EXTRATO_FGTS', 'Extrato do FGTS', 'RENDA', TIMESTAMPTZ '2026-01-01 00:00:00+00', false),
                       ('d0c00000-0000-7000-8000-000000000023', 'EXTRATO_BANCARIO_PF', 'Extrato bancário de pessoa física', 'RENDA', TIMESTAMPTZ '2026-01-01 00:00:00+00', false),
                       ('d0c00000-0000-7000-8000-000000000024', 'EXTRATO_BANCARIO_PJ', 'Extrato bancário de pessoa jurídica', 'RENDA', TIMESTAMPTZ '2026-01-01 00:00:00+00', false),
                       ('d0c00000-0000-7000-8000-000000000025', 'EXTRATO_PAGAMENTO_BENEFICIO', 'Extrato de pagamento de benefício', 'RENDA', TIMESTAMPTZ '2026-01-01 00:00:00+00', false),
                       ('d0c00000-0000-7000-8000-000000000026', 'NOTA_FISCAL_VENDAS', 'Nota fiscal de vendas', 'RENDA', TIMESTAMPTZ '2026-01-01 00:00:00+00', false),
                       ('d0c00000-0000-7000-8000-000000000027', 'GUIA_RECOLHIMENTO_INSS', 'Guia de recolhimento ao INSS', 'RENDA', TIMESTAMPTZ '2026-01-01 00:00:00+00', false),
                       ('d0c00000-0000-7000-8000-000000000028', 'CONTRATO_LOCACAO_ARRENDAMENTO', 'Contrato de locação ou arrendamento', 'RENDA', TIMESTAMPTZ '2026-01-01 00:00:00+00', false),
                       ('d0c00000-0000-7000-8000-000000000029', 'REGISTRATO_BACEN', 'Registrato do Banco Central', 'RENDA', TIMESTAMPTZ '2026-01-01 00:00:00+00', false),
                       ('d0c00000-0000-7000-8000-000000000030', 'CADASTRO_UNICO', 'Cadastro Único', 'RENDA', TIMESTAMPTZ '2026-01-01 00:00:00+00', false),
                       ('d0c00000-0000-7000-8000-000000000031', 'DECLARACAO_IRPF', 'Declaração de IRPF', 'RENDA', TIMESTAMPTZ '2026-01-01 00:00:00+00', false),
                       ('d0c00000-0000-7000-8000-000000000032', 'DECLARACAO_ISENCAO_IRPF', 'Declaração de isenção de IRPF', 'RENDA', TIMESTAMPTZ '2026-01-01 00:00:00+00', false),
                       ('d0c00000-0000-7000-8000-000000000033', 'DECLARACAO_TRIBUTARIA_PJ', 'Declaração tributária de pessoa jurídica', 'RENDA', TIMESTAMPTZ '2026-01-01 00:00:00+00', false),
                       ('d0c00000-0000-7000-8000-000000000034', 'DECLARACAO_TRABALHADOR_RURAL', 'Declaração de trabalhador rural', 'RENDA', TIMESTAMPTZ '2026-01-01 00:00:00+00', false),
                       ('d0c00000-0000-7000-8000-000000000035', 'DECLARACAO_TRABALHADOR_AUTONOMO', 'Declaração de trabalhador autônomo', 'RENDA', TIMESTAMPTZ '2026-01-01 00:00:00+00', false),
                       ('d0c00000-0000-7000-8000-000000000036', 'DECLARACAO_ATIVIDADE_DO_LAR', 'Declaração de atividade do lar', 'RENDA', TIMESTAMPTZ '2026-01-01 00:00:00+00', false),
                       ('d0c00000-0000-7000-8000-000000000037', 'DECLARACAO_AUSENCIA_RENDIMENTOS', 'Declaração de ausência de rendimentos', 'RENDA', TIMESTAMPTZ '2026-01-01 00:00:00+00', false),
                       ('d0c00000-0000-7000-8000-000000000038', 'DECLARACAO_PENSAO_ALIMENTICIA', 'Declaração de recebimento de pensão alimentícia', 'RENDA', TIMESTAMPTZ '2026-01-01 00:00:00+00', false),
                       ('d0c00000-0000-7000-8000-000000000039', 'DECLARACAO_RENDIMENTO_ALUGUEL', 'Declaração de rendimentos de aluguel', 'RENDA', TIMESTAMPTZ '2026-01-01 00:00:00+00', false),
                       ('d0c00000-0000-7000-8000-000000000040', 'DECLARACAO_SEM_CONTA_BANCARIA', 'Declaração de que não possui conta bancária', 'RENDA', TIMESTAMPTZ '2026-01-01 00:00:00+00', false),
                       ('d0c00000-0000-7000-8000-000000000041', 'COMPROVANTE_PAGAMENTO_TAXA', 'Comprovante de pagamento da taxa de inscrição', 'RENDA', TIMESTAMPTZ '2026-01-01 00:00:00+00', false),
                       ('d0c00000-0000-7000-8000-000000000042', 'AUTODECLARACAO_ETNICO_RACIAL', 'Autodeclaração étnico-racial justificada', 'RACA_ETNIA', TIMESTAMPTZ '2026-01-01 00:00:00+00', false),
                       ('d0c00000-0000-7000-8000-000000000043', 'AUTODECLARACAO_INDIGENA', 'Autodeclaração indígena', 'RACA_ETNIA', TIMESTAMPTZ '2026-01-01 00:00:00+00', false),
                       ('d0c00000-0000-7000-8000-000000000044', 'AUTODECLARACAO_QUILOMBOLA', 'Autodeclaração quilombola', 'RACA_ETNIA', TIMESTAMPTZ '2026-01-01 00:00:00+00', false),
                       ('d0c00000-0000-7000-8000-000000000045', 'DECLARACAO_PERTENCIMENTO_INDIGENA', 'Declaração de pertencimento indígena', 'RACA_ETNIA', TIMESTAMPTZ '2026-01-01 00:00:00+00', false),
                       ('d0c00000-0000-7000-8000-000000000046', 'DECLARACAO_PERTENCIMENTO_QUILOMBOLA', 'Declaração de pertencimento quilombola', 'RACA_ETNIA', TIMESTAMPTZ '2026-01-01 00:00:00+00', false),
                       ('d0c00000-0000-7000-8000-000000000047', 'LAUDO_MEDICO', 'Laudo médico', 'SAUDE', TIMESTAMPTZ '2026-01-01 00:00:00+00', false),
                       ('d0c00000-0000-7000-8000-000000000048', 'COMPROVANTE_VACINACAO', 'Comprovante de vacinação', 'SAUDE', TIMESTAMPTZ '2026-01-01 00:00:00+00', false),
                       ('d0c00000-0000-7000-8000-000000000049', 'COMPROVANTE_RESIDENCIA', 'Comprovante de residência', 'RESIDENCIA', TIMESTAMPTZ '2026-01-01 00:00:00+00', false),
                       ('d0c00000-0000-7000-8000-000000000050', 'DECLARACAO_RESIDENCIA', 'Declaração de residência', 'RESIDENCIA', TIMESTAMPTZ '2026-01-01 00:00:00+00', false),
                       ('d0c00000-0000-7000-8000-000000000051', 'REQUERIMENTO_INSCRICAO', 'Requerimento de inscrição', 'DOCUMENTO_PROCESSUAL', TIMESTAMPTZ '2026-01-01 00:00:00+00', false),
                       ('d0c00000-0000-7000-8000-000000000052', 'REQUERIMENTO_NOME_SOCIAL', 'Requerimento de inclusão de nome social', 'DOCUMENTO_PROCESSUAL', TIMESTAMPTZ '2026-01-01 00:00:00+00', false),
                       ('d0c00000-0000-7000-8000-000000000053', 'REQUERIMENTO_DESISTENCIA_VAGA', 'Requerimento de desistência de vaga', 'DOCUMENTO_PROCESSUAL', TIMESTAMPTZ '2026-01-01 00:00:00+00', false),
                       ('d0c00000-0000-7000-8000-000000000054', 'RECURSO_ADMINISTRATIVO', 'Recurso administrativo', 'DOCUMENTO_PROCESSUAL', TIMESTAMPTZ '2026-01-01 00:00:00+00', false),
                       ('d0c00000-0000-7000-8000-000000000055', 'PROCURACAO', 'Procuração', 'DOCUMENTO_PROCESSUAL', TIMESTAMPTZ '2026-01-01 00:00:00+00', false),
                       ('d0c00000-0000-7000-8000-000000000056', 'TERMO_ACEITE', 'Termo de aceite', 'DOCUMENTO_PROCESSUAL', TIMESTAMPTZ '2026-01-01 00:00:00+00', false),
                       ('d0c00000-0000-7000-8000-000000000057', 'TERMO_COMPROMISSO', 'Termo de compromisso', 'DOCUMENTO_PROCESSUAL', TIMESTAMPTZ '2026-01-01 00:00:00+00', false),
                       ('d0c00000-0000-7000-8000-000000000058', 'DECLARACAO_AUTENTICIDADE', 'Declaração de autenticidade dos documentos', 'DOCUMENTO_PROCESSUAL', TIMESTAMPTZ '2026-01-01 00:00:00+00', false),
                       ('d0c00000-0000-7000-8000-000000000059', 'DECLARACAO_DISPONIBILIDADE', 'Declaração de disponibilidade', 'DOCUMENTO_PROCESSUAL', TIMESTAMPTZ '2026-01-01 00:00:00+00', false),
                       ('d0c00000-0000-7000-8000-000000000060', 'FORMULARIO_OPCAO_ENTREVISTA', 'Formulário de escolha do formato de entrevista', 'DOCUMENTO_PROCESSUAL', TIMESTAMPTZ '2026-01-01 00:00:00+00', false),
                       ('d0c00000-0000-7000-8000-000000000061', 'DECLARACAO_FUNCIONAL_SIG', 'Declaração funcional emitida pelo SIG', 'TITULACAO_EXPERIENCIA', TIMESTAMPTZ '2026-01-01 00:00:00+00', false),
                       ('d0c00000-0000-7000-8000-000000000062', 'COMPROVACAO_EXPERIENCIA_PROFISSIONAL', 'Comprovação de experiência profissional', 'TITULACAO_EXPERIENCIA', TIMESTAMPTZ '2026-01-01 00:00:00+00', false),
                       ('d0c00000-0000-7000-8000-000000000063', 'COMPROVACAO_EXPERIENCIA_ESCOLA_CAMPO', 'Comprovação de experiência profissional em escola do campo', 'TITULACAO_EXPERIENCIA', TIMESTAMPTZ '2026-01-01 00:00:00+00', false),
                       ('d0c00000-0000-7000-8000-000000000064', 'COMPROVACAO_ATIVIDADE_POVOS_TRADICIONAIS', 'Comprovação de atividade junto a povos tradicionais', 'TITULACAO_EXPERIENCIA', TIMESTAMPTZ '2026-01-01 00:00:00+00', false),
                       ('d0c00000-0000-7000-8000-000000000065', 'COMPROVACAO_VINCULO_DOCENTE_REDE_PUBLICA', 'Comprovação de vínculo docente na rede pública', 'TITULACAO_EXPERIENCIA', TIMESTAMPTZ '2026-01-01 00:00:00+00', false),
                       ('d0c00000-0000-7000-8000-000000000066', 'COMPROVACAO_ATUACAO_APOIO_EDUCACAO_ESPECIAL', 'Comprovação de atuação como profissional de apoio da educação especial', 'TITULACAO_EXPERIENCIA', TIMESTAMPTZ '2026-01-01 00:00:00+00', false),
                       ('d0c00000-0000-7000-8000-000000000067', 'COMPROVACAO_LATTES_PESQUISA', 'Comprovação de pesquisa concluída no currículo Lattes', 'TITULACAO_EXPERIENCIA', TIMESTAMPTZ '2026-01-01 00:00:00+00', false),
                       ('d0c00000-0000-7000-8000-000000000068', 'COMPROVACAO_LATTES_EXTENSAO_ENSINO', 'Comprovação de extensão e ensino no currículo Lattes', 'TITULACAO_EXPERIENCIA', TIMESTAMPTZ '2026-01-01 00:00:00+00', false),
                       ('d0c00000-0000-7000-8000-000000000069', 'CARTA_INTENCAO', 'Carta de intenção', 'PRODUCAO_AVALIATIVA', TIMESTAMPTZ '2026-01-01 00:00:00+00', false),
                       ('d0c00000-0000-7000-8000-000000000070', 'RELATO_HISTORIA_VIDA', 'Relato de história de vida', 'PRODUCAO_AVALIATIVA', TIMESTAMPTZ '2026-01-01 00:00:00+00', false)
                ON CONFLICT DO NOTHING;
                """);
        }

        /// <inheritdoc />
        /// <remarks>
        /// Remove apenas as linhas desta carga, pelos identificadores determinísticos:
        /// o que o operador tiver cadastrado por conta própria permanece.
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE FROM configuracao.tipo_documento
                 WHERE id::text LIKE 'd0c00000-0000-7000-8000-%';
                """);
        }
    }
}
