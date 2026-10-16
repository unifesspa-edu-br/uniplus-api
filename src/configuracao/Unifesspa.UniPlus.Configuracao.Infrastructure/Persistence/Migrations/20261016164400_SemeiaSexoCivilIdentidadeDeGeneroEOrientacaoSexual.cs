using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SemeiaSexoCivilIdentidadeDeGeneroEOrientacaoSexual : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000007"),
                column: "nome",
                value: "Sexo biológico");

            migrationBuilder.InsertData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                columns: new[] { "id", "ativo", "binding", "cardinalidade", "classificacao_protecao", "codigo", "created_at", "created_by", "dependencias", "descricao", "dominio", "escopo", "finalidade_tratamento", "fonte_valores", "formato", "hipotese_legal", "nome", "origem", "ponto_resolucao", "regras_padrao", "sistema", "updated_at", "updated_by" },
                values: new object[,]
                {
                    { new Guid("fa700000-0000-7000-8000-000000000057"), true, "CAMPO_FORMULARIO:SEXO_CIVIL", "ESCALAR", "PESSOAL", "SEXO_CIVIL", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, Array.Empty<string>(), null, "CATEGORICO", "CANDIDATO", "Identificação do candidato no processo seletivo.", "GLOBAL", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Sexo civil", "DECLARADO", "INSCRICAO", "[]", true, null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000058"), true, "CAMPO_FORMULARIO:IDENTIDADE_GENERO", "ESCALAR", "SENSIVEL", "IDENTIDADE_GENERO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, Array.Empty<string>(), null, "CATEGORICO", "CANDIDATO", "Identificação do candidato no processo seletivo.", "GLOBAL", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Identidade de gênero", "DECLARADO", "INSCRICAO", "[]", true, null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000059"), true, "CAMPO_FORMULARIO:ORIENTACAO_SEXUAL", "ESCALAR", "SENSIVEL", "ORIENTACAO_SEXUAL", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, Array.Empty<string>(), null, "CATEGORICO", "CANDIDATO", "Identificação do candidato no processo seletivo.", "GLOBAL", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Orientação sexual", "DECLARADO", "INSCRICAO", "[]", true, null, null }
                });

            migrationBuilder.InsertData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                columns: new[] { "id", "ativo", "codigo", "created_at", "descricao", "fato_candidato_id", "ordem", "orientacao", "updated_at" },
                values: new object[,]
                {
                    { new Guid("fa70d000-0000-7000-8000-000000000069"), true, "FEMININO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Feminino", new Guid("fa700000-0000-7000-8000-000000000057"), 0, null, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000070"), true, "MASCULINO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Masculino", new Guid("fa700000-0000-7000-8000-000000000057"), 1, null, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000071"), true, "PESSOA_CIS", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Pessoa cisgênero", new Guid("fa700000-0000-7000-8000-000000000058"), 0, null, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000072"), true, "PESSOA_TRANSGENERO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Pessoa transgênero", new Guid("fa700000-0000-7000-8000-000000000058"), 1, null, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000073"), true, "TRAVESTI", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Travesti", new Guid("fa700000-0000-7000-8000-000000000058"), 2, null, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000074"), true, "TRANSEXUAL", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Transexual", new Guid("fa700000-0000-7000-8000-000000000058"), 3, null, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000075"), true, "PESSOA_NAO_BINARIA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Pessoa não binária", new Guid("fa700000-0000-7000-8000-000000000058"), 4, null, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000076"), true, "PREFIRO_NAO_INFORMAR", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Prefiro não informar", new Guid("fa700000-0000-7000-8000-000000000058"), 5, null, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000077"), true, "HETEROSSEXUAL", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Heterossexual", new Guid("fa700000-0000-7000-8000-000000000059"), 0, null, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000078"), true, "HOMOSSEXUAL", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Homossexual", new Guid("fa700000-0000-7000-8000-000000000059"), 1, null, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000079"), true, "BISSEXUAL", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Bissexual", new Guid("fa700000-0000-7000-8000-000000000059"), 2, null, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000080"), true, "ASSEXUAL", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Assexual", new Guid("fa700000-0000-7000-8000-000000000059"), 3, null, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000081"), true, "PANSEXUAL", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Pansexual", new Guid("fa700000-0000-7000-8000-000000000059"), 4, null, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000082"), true, "PREFIRO_NAO_INFORMAR", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Prefiro não informar", new Guid("fa700000-0000-7000-8000-000000000059"), 5, null, null }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000069"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000070"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000071"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000072"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000073"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000074"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000075"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000076"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000077"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000078"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000079"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000080"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000081"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000082"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000057"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000058"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000059"));

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000007"),
                column: "nome",
                value: "Sexo");
        }
    }
}
