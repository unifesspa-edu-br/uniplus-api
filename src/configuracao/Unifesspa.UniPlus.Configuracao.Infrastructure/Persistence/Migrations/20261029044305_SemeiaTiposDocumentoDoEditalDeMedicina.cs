using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SemeiaTiposDocumentoDoEditalDeMedicina : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "configuracao",
                table: "tipo_documento",
                columns: new[] { "id", "categoria", "classificacao_protecao", "codigo", "created_at", "created_by", "deleted_at", "deleted_by", "descricao", "finalidade_tratamento", "formatos_aceitos", "hipotese_legal", "is_deleted", "nome", "tamanho_maximo_mb", "tipo_equivalente", "updated_at", "updated_by" },
                values: new object[,]
                {
                    { new Guid("d0c00000-0000-7000-8000-000000000071"), "IDENTIFICACAO", "IDENTIFICADOR", "CIN", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Carteira de Identidade Nacional", null, "RG", null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000072"), "IDENTIFICACAO", "IDENTIFICADOR", "CNH", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Carteira Nacional de Habilitação", null, null, null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000073"), "IDENTIFICACAO", "IDENTIFICADOR", "PASSAPORTE", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Passaporte", null, null, null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000074"), "IDENTIFICACAO", "IDENTIFICADOR", "RNM", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Registro Nacional Migratório", null, null, null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000075"), "IDENTIFICACAO", "SENSIVEL", "BOLETIM_OCORRENCIA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Boletim de ocorrência", null, null, null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000076"), "IDENTIFICACAO", "SENSIVEL", "DOCUMENTO_JUDICIAL_GUARDA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Documento judicial de guarda", null, null, null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000077"), "ESCOLARIDADE", "PESSOAL", "DECLARACAO_CONCLUSAO_ENSINO_MEDIO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Atestado ou declaração de conclusão do ensino médio", null, null, null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000078"), "RENDA", "PESSOAL", "CNIS", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Extrato do Cadastro Nacional de Informações Sociais (CNIS)", null, null, null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000079"), "RENDA", "PESSOAL", "DECLARACAO_IRPJ", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Declaração de IRPJ", null, null, null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000080"), "RENDA", "PESSOAL", "ECF", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Escrituração Contábil Fiscal (ECF)", null, null, null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000081"), "RENDA", "PESSOAL", "DECORE", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Declaração Comprobatória de Percepção de Rendimentos (DECORE)", null, null, null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000082"), "RENDA", "PESSOAL", "DEFIS", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Declaração de Informações Socioeconômicas e Fiscais (DEFIS)", null, null, null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000083"), "RENDA", "PESSOAL", "DASN_SIMEI", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Declaração Anual do Simples Nacional do MEI (DASN-SIMEI)", null, null, null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000084"), "RENDA", "PESSOAL", "DECLARACAO_INATIVIDADE_EMPRESA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Declaração de inatividade da empresa", null, null, null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000085"), "RENDA", "PESSOAL", "DECLARACAO_RENDIMENTOS_AUFERIDOS", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Declaração de rendimentos auferidos", null, null, null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000086"), "RENDA", "PESSOAL", "DECLARACAO_AUSENCIA_RENDIMENTOS_ALUGUEL", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Declaração de ausência de rendimentos de aluguel", null, null, null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000087"), "RENDA", "PESSOAL", "EXTRATO_PREVIDENCIA_PRIVADA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Extrato de benefício de previdência privada", null, null, null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000088"), "RENDA", "PESSOAL", "TRCT", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Termo de rescisão do contrato de trabalho (TRCT)", null, null, null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000089"), "SAUDE", "SENSIVEL", "LAUDO_PSICOLOGICO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Laudo psicológico", null, null, null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000090"), "SAUDE", "SENSIVEL", "EXAME_AUDIOMETRIA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Exame de audiometria", null, null, null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000091"), "SAUDE", "SENSIVEL", "EXAME_OFTALMOLOGICO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Exame oftalmológico com acuidade visual", null, null, null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000092"), "DOCUMENTO_PROCESSUAL", "PESSOAL", "TERMO_RESPONSABILIDADE_CERTIFICADO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Termo de responsabilidade pela entrega do certificado de conclusão", null, null, null, null }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000071"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000072"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000073"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000074"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000075"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000076"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000077"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000078"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000079"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000080"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000081"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000082"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000083"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000084"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000085"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000086"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000087"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000088"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000089"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000090"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000091"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000092"));
        }
    }
}
