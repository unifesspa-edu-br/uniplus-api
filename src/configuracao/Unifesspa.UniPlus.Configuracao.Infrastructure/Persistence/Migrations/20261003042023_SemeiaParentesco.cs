using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SemeiaParentesco : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                columns: new[] { "id", "ativo", "binding", "cardinalidade", "classificacao_protecao", "codigo", "created_at", "created_by", "dependencias", "descricao", "dominio", "escopo", "finalidade_tratamento", "fonte_valores", "formato", "hipotese_legal", "nome", "origem", "ponto_resolucao", "regras_padrao", "sistema", "updated_at", "updated_by" },
                values: new object[] { new Guid("fa700000-0000-7000-8000-000000000026"), true, "CAMPO_INSCRICAO:PARENTESCO", "ESCALAR", "PESSOAL", "PARENTESCO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, Array.Empty<string>(), null, "CATEGORICO", "MEMBRO_GRUPO", "Comprovação documental dos membros da composição familiar do candidato no processo seletivo.", "GLOBAL", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Parentesco", "DECLARADO", "INSCRICAO", "[]", true, null, null });

            migrationBuilder.InsertData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                columns: new[] { "id", "ativo", "codigo", "created_at", "descricao", "fato_candidato_id", "ordem", "updated_at" },
                values: new object[,]
                {
                    { new Guid("fa70d000-0000-7000-8000-000000000013"), true, "PROPRIO_CANDIDATO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "O próprio candidato.", new Guid("fa700000-0000-7000-8000-000000000026"), 0, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000014"), true, "CONJUGE_OU_COMPANHEIRO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Cônjuge ou companheiro(a) do candidato.", new Guid("fa700000-0000-7000-8000-000000000026"), 1, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000015"), true, "FILHO_OU_ENTEADO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Filho(a) ou enteado(a) do candidato.", new Guid("fa700000-0000-7000-8000-000000000026"), 2, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000016"), true, "PAI_OU_MAE", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Pai ou mãe do candidato.", new Guid("fa700000-0000-7000-8000-000000000026"), 3, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000017"), true, "PADRASTO_OU_MADRASTA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Padrasto ou madrasta do candidato.", new Guid("fa700000-0000-7000-8000-000000000026"), 4, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000018"), true, "IRMAO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Irmão ou irmã do candidato.", new Guid("fa700000-0000-7000-8000-000000000026"), 5, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000019"), true, "AVO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Avô ou avó do candidato.", new Guid("fa700000-0000-7000-8000-000000000026"), 6, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000020"), true, "NETO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Neto(a) do candidato.", new Guid("fa700000-0000-7000-8000-000000000026"), 7, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000021"), true, "SOGRO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Sogro ou sogra do candidato.", new Guid("fa700000-0000-7000-8000-000000000026"), 8, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000022"), true, "GENRO_OU_NORA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Genro ou nora do candidato.", new Guid("fa700000-0000-7000-8000-000000000026"), 9, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000023"), true, "OUTRO_PARENTE", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Outro parente do candidato.", new Guid("fa700000-0000-7000-8000-000000000026"), 10, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000024"), true, "NAO_PARENTE", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Pessoa sem parentesco com o candidato que integra o grupo familiar.", new Guid("fa700000-0000-7000-8000-000000000026"), 11, null }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000013"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000014"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000015"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000016"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000017"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000018"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000019"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000020"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000021"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000022"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000023"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000024"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000026"));
        }
    }
}
