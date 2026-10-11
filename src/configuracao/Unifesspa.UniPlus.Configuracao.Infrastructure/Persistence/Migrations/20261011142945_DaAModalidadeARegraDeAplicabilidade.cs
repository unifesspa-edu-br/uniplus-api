using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DaAModalidadeARegraDeAplicabilidade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "criterios_cumulativos",
                schema: "configuracao",
                table: "modalidade");

            migrationBuilder.AddColumn<string>(
                name: "regra_de_aplicabilidade",
                schema: "configuracao",
                table: "modalidade",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            // O default só preenche as linhas que já existiam; a regra de cada modalidade passa a
            // vir sempre do cadastro ou da semente, como o modelo declara.
            migrationBuilder.Sql("ALTER TABLE configuracao.modalidade ALTER COLUMN regra_de_aplicabilidade DROP DEFAULT;");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "modalidade",
                keyColumn: "id",
                keyValue: new Guid("70da1000-0000-7000-8000-000000000001"),
                column: "regra_de_aplicabilidade",
                value: "[]");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "modalidade",
                keyColumn: "id",
                keyValue: new Guid("70da1000-0000-7000-8000-000000000002"),
                column: "regra_de_aplicabilidade",
                value: "[[{\"fato\":\"OPTOU_PELAS_COTAS_DA_LEI\",\"operador\":\"IGUAL\",\"valor\":true},{\"fato\":\"COR_RACA\",\"operador\":\"EM\",\"valor\":[\"PRETA\",\"PARDA\"]},{\"fato\":\"CONCORRER_PPI\",\"operador\":\"IGUAL\",\"valor\":true},{\"fato\":\"BAIXA_RENDA\",\"operador\":\"IGUAL\",\"valor\":true},{\"fato\":\"CONCORRER_RENDA\",\"operador\":\"IGUAL\",\"valor\":true}],[{\"fato\":\"OPTOU_PELAS_COTAS_DA_LEI\",\"operador\":\"IGUAL\",\"valor\":true},{\"fato\":\"COR_RACA\",\"operador\":\"IGUAL\",\"valor\":\"INDIGENA\"},{\"fato\":\"INDIGENA\",\"operador\":\"IGUAL\",\"valor\":true},{\"fato\":\"CONCORRER_PPI\",\"operador\":\"IGUAL\",\"valor\":true},{\"fato\":\"BAIXA_RENDA\",\"operador\":\"IGUAL\",\"valor\":true},{\"fato\":\"CONCORRER_RENDA\",\"operador\":\"IGUAL\",\"valor\":true}]]");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "modalidade",
                keyColumn: "id",
                keyValue: new Guid("70da1000-0000-7000-8000-000000000003"),
                column: "regra_de_aplicabilidade",
                value: "[[{\"fato\":\"OPTOU_PELAS_COTAS_DA_LEI\",\"operador\":\"IGUAL\",\"valor\":true},{\"fato\":\"QUILOMBOLA\",\"operador\":\"IGUAL\",\"valor\":true},{\"fato\":\"CONCORRER_Q\",\"operador\":\"IGUAL\",\"valor\":true},{\"fato\":\"BAIXA_RENDA\",\"operador\":\"IGUAL\",\"valor\":true},{\"fato\":\"CONCORRER_RENDA\",\"operador\":\"IGUAL\",\"valor\":true}]]");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "modalidade",
                keyColumn: "id",
                keyValue: new Guid("70da1000-0000-7000-8000-000000000004"),
                column: "regra_de_aplicabilidade",
                value: "[[{\"fato\":\"OPTOU_PELAS_COTAS_DA_LEI\",\"operador\":\"IGUAL\",\"valor\":true},{\"fato\":\"PCD\",\"operador\":\"IGUAL\",\"valor\":true},{\"fato\":\"CONCORRER_PCD\",\"operador\":\"IGUAL\",\"valor\":true},{\"fato\":\"BAIXA_RENDA\",\"operador\":\"IGUAL\",\"valor\":true},{\"fato\":\"CONCORRER_RENDA\",\"operador\":\"IGUAL\",\"valor\":true}]]");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "modalidade",
                keyColumn: "id",
                keyValue: new Guid("70da1000-0000-7000-8000-000000000005"),
                column: "regra_de_aplicabilidade",
                value: "[[{\"fato\":\"OPTOU_PELAS_COTAS_DA_LEI\",\"operador\":\"IGUAL\",\"valor\":true},{\"fato\":\"BAIXA_RENDA\",\"operador\":\"IGUAL\",\"valor\":true},{\"fato\":\"CONCORRER_RENDA\",\"operador\":\"IGUAL\",\"valor\":true}]]");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "modalidade",
                keyColumn: "id",
                keyValue: new Guid("70da1000-0000-7000-8000-000000000006"),
                column: "regra_de_aplicabilidade",
                value: "[[{\"fato\":\"OPTOU_PELAS_COTAS_DA_LEI\",\"operador\":\"IGUAL\",\"valor\":true},{\"fato\":\"COR_RACA\",\"operador\":\"EM\",\"valor\":[\"PRETA\",\"PARDA\"]},{\"fato\":\"CONCORRER_PPI\",\"operador\":\"IGUAL\",\"valor\":true}],[{\"fato\":\"OPTOU_PELAS_COTAS_DA_LEI\",\"operador\":\"IGUAL\",\"valor\":true},{\"fato\":\"COR_RACA\",\"operador\":\"IGUAL\",\"valor\":\"INDIGENA\"},{\"fato\":\"INDIGENA\",\"operador\":\"IGUAL\",\"valor\":true},{\"fato\":\"CONCORRER_PPI\",\"operador\":\"IGUAL\",\"valor\":true}]]");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "modalidade",
                keyColumn: "id",
                keyValue: new Guid("70da1000-0000-7000-8000-000000000007"),
                column: "regra_de_aplicabilidade",
                value: "[[{\"fato\":\"OPTOU_PELAS_COTAS_DA_LEI\",\"operador\":\"IGUAL\",\"valor\":true},{\"fato\":\"QUILOMBOLA\",\"operador\":\"IGUAL\",\"valor\":true},{\"fato\":\"CONCORRER_Q\",\"operador\":\"IGUAL\",\"valor\":true}]]");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "modalidade",
                keyColumn: "id",
                keyValue: new Guid("70da1000-0000-7000-8000-000000000008"),
                column: "regra_de_aplicabilidade",
                value: "[[{\"fato\":\"OPTOU_PELAS_COTAS_DA_LEI\",\"operador\":\"IGUAL\",\"valor\":true},{\"fato\":\"PCD\",\"operador\":\"IGUAL\",\"valor\":true},{\"fato\":\"CONCORRER_PCD\",\"operador\":\"IGUAL\",\"valor\":true}]]");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "modalidade",
                keyColumn: "id",
                keyValue: new Guid("70da1000-0000-7000-8000-000000000009"),
                column: "regra_de_aplicabilidade",
                value: "[[{\"fato\":\"OPTOU_PELAS_COTAS_DA_LEI\",\"operador\":\"IGUAL\",\"valor\":true}]]");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "modalidade",
                keyColumn: "id",
                keyValue: new Guid("70da1000-0000-7000-8000-000000000010"),
                column: "regra_de_aplicabilidade",
                value: "[[{\"fato\":\"OPTOU_PELAS_COTAS_DA_LEI\",\"operador\":\"IGUAL\",\"valor\":false},{\"fato\":\"PCD\",\"operador\":\"IGUAL\",\"valor\":true},{\"fato\":\"CONCORRER_PCD\",\"operador\":\"IGUAL\",\"valor\":true}]]");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "modalidade",
                keyColumn: "id",
                keyValue: new Guid("70da1000-0000-7000-8000-000000000012"),
                column: "regra_de_aplicabilidade",
                value: "[[{\"fato\":\"OPTOU_PELAS_COTAS_DA_LEI\",\"operador\":\"IGUAL\",\"valor\":false},{\"fato\":\"INDIGENA\",\"operador\":\"IGUAL\",\"valor\":true},{\"fato\":\"CONCORRER_VAGA_POR_ACRESCIMO\",\"operador\":\"IGUAL\",\"valor\":true}]]");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "modalidade",
                keyColumn: "id",
                keyValue: new Guid("70da1000-0000-7000-8000-000000000013"),
                column: "regra_de_aplicabilidade",
                value: "[[{\"fato\":\"OPTOU_PELAS_COTAS_DA_LEI\",\"operador\":\"IGUAL\",\"valor\":false},{\"fato\":\"QUILOMBOLA\",\"operador\":\"IGUAL\",\"valor\":true},{\"fato\":\"CONCORRER_VAGA_POR_ACRESCIMO\",\"operador\":\"IGUAL\",\"valor\":true}]]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "regra_de_aplicabilidade",
                schema: "configuracao",
                table: "modalidade");

            migrationBuilder.AddColumn<string>(
                name: "criterios_cumulativos",
                schema: "configuracao",
                table: "modalidade",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "modalidade",
                keyColumn: "id",
                keyValue: new Guid("70da1000-0000-7000-8000-000000000001"),
                column: "criterios_cumulativos",
                value: "[]");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "modalidade",
                keyColumn: "id",
                keyValue: new Guid("70da1000-0000-7000-8000-000000000002"),
                column: "criterios_cumulativos",
                value: "[]");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "modalidade",
                keyColumn: "id",
                keyValue: new Guid("70da1000-0000-7000-8000-000000000003"),
                column: "criterios_cumulativos",
                value: "[]");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "modalidade",
                keyColumn: "id",
                keyValue: new Guid("70da1000-0000-7000-8000-000000000004"),
                column: "criterios_cumulativos",
                value: "[]");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "modalidade",
                keyColumn: "id",
                keyValue: new Guid("70da1000-0000-7000-8000-000000000005"),
                column: "criterios_cumulativos",
                value: "[]");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "modalidade",
                keyColumn: "id",
                keyValue: new Guid("70da1000-0000-7000-8000-000000000006"),
                column: "criterios_cumulativos",
                value: "[]");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "modalidade",
                keyColumn: "id",
                keyValue: new Guid("70da1000-0000-7000-8000-000000000007"),
                column: "criterios_cumulativos",
                value: "[]");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "modalidade",
                keyColumn: "id",
                keyValue: new Guid("70da1000-0000-7000-8000-000000000008"),
                column: "criterios_cumulativos",
                value: "[]");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "modalidade",
                keyColumn: "id",
                keyValue: new Guid("70da1000-0000-7000-8000-000000000009"),
                column: "criterios_cumulativos",
                value: "[]");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "modalidade",
                keyColumn: "id",
                keyValue: new Guid("70da1000-0000-7000-8000-000000000010"),
                column: "criterios_cumulativos",
                value: "[]");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "modalidade",
                keyColumn: "id",
                keyValue: new Guid("70da1000-0000-7000-8000-000000000012"),
                column: "criterios_cumulativos",
                value: "[]");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "modalidade",
                keyColumn: "id",
                keyValue: new Guid("70da1000-0000-7000-8000-000000000013"),
                column: "criterios_cumulativos",
                value: "[]");
        }
    }
}
