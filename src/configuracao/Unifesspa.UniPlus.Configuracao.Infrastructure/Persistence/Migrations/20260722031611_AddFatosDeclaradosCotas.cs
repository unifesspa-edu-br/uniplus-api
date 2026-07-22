using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFatosDeclaradosCotas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                columns: new[] { "id", "binding", "cardinalidade", "codigo", "created_at", "descricao", "dominio", "nome", "origem", "ponto_resolucao", "updated_at", "valores_dominio" },
                values: new object[,]
                {
                    { new Guid("fa700000-0000-7000-8000-000000000012"), "CAMPO_INSCRICAO:BAIXA_RENDA", "ESCALAR", "BAIXA_RENDA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "BOOLEANO", "Renda familiar per capita igual ou inferior a um salário mínimo", "DECLARADO", "INSCRICAO", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000013"), "CAMPO_INSCRICAO:CONCORRER_PCD", "ESCALAR", "CONCORRER_PCD", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "BOOLEANO", "Deseja concorrer às vagas reservadas a pessoas com deficiência", "DECLARADO", "INSCRICAO", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000014"), "CAMPO_INSCRICAO:CONCORRER_EP", "ESCALAR", "CONCORRER_EP", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "BOOLEANO", "Deseja concorrer às vagas reservadas a egressos de escola pública", "DECLARADO", "INSCRICAO", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000015"), "CAMPO_INSCRICAO:CONCORRER_PPI", "ESCALAR", "CONCORRER_PPI", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "BOOLEANO", "Deseja concorrer às vagas reservadas a pretos, pardos e indígenas", "DECLARADO", "INSCRICAO", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000016"), "CAMPO_INSCRICAO:CONCORRER_Q", "ESCALAR", "CONCORRER_Q", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "BOOLEANO", "Deseja concorrer às vagas reservadas a quilombolas", "DECLARADO", "INSCRICAO", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000017"), "CAMPO_INSCRICAO:CONCORRER_RENDA", "ESCALAR", "CONCORRER_RENDA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "BOOLEANO", "Deseja concorrer às vagas reservadas por renda familiar per capita", "DECLARADO", "INSCRICAO", null, null }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000012"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000013"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000014"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000015"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000016"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000017"));
        }
    }
}
