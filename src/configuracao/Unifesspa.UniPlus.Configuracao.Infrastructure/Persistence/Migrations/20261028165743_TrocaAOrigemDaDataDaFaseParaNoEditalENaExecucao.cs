using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TrocaAOrigemDaDataDaFaseParaNoEditalENaExecucao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_fase_canonica_origem_data",
                schema: "configuracao",
                table: "fase_canonica");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "fase_canonica",
                keyColumn: "id",
                keyValue: new Guid("f45e0000-0000-7000-8000-000000000001"),
                column: "origem_data",
                value: "NO_EDITAL");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "fase_canonica",
                keyColumn: "id",
                keyValue: new Guid("f45e0000-0000-7000-8000-000000000002"),
                column: "origem_data",
                value: "NO_EDITAL");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "fase_canonica",
                keyColumn: "id",
                keyValue: new Guid("f45e0000-0000-7000-8000-000000000003"),
                column: "origem_data",
                value: "NO_EDITAL");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "fase_canonica",
                keyColumn: "id",
                keyValue: new Guid("f45e0000-0000-7000-8000-000000000004"),
                column: "origem_data",
                value: "NO_EDITAL");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "fase_canonica",
                keyColumn: "id",
                keyValue: new Guid("f45e0000-0000-7000-8000-000000000005"),
                column: "origem_data",
                value: "NO_EDITAL");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "fase_canonica",
                keyColumn: "id",
                keyValue: new Guid("f45e0000-0000-7000-8000-000000000006"),
                column: "origem_data",
                value: "NO_EDITAL");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "fase_canonica",
                keyColumn: "id",
                keyValue: new Guid("f45e0000-0000-7000-8000-000000000007"),
                column: "origem_data",
                value: "NO_EDITAL");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "fase_canonica",
                keyColumn: "id",
                keyValue: new Guid("f45e0000-0000-7000-8000-000000000008"),
                column: "origem_data",
                value: "NO_EDITAL");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "fase_canonica",
                keyColumn: "id",
                keyValue: new Guid("f45e0000-0000-7000-8000-000000000009"),
                column: "origem_data",
                value: "NO_EDITAL");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "fase_canonica",
                keyColumn: "id",
                keyValue: new Guid("f45e0000-0000-7000-8000-000000000010"),
                column: "origem_data",
                value: "NO_EDITAL");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "fase_canonica",
                keyColumn: "id",
                keyValue: new Guid("f45e0000-0000-7000-8000-000000000011"),
                column: "origem_data",
                value: "NO_EDITAL");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "fase_canonica",
                keyColumn: "id",
                keyValue: new Guid("f45e0000-0000-7000-8000-000000000012"),
                column: "origem_data",
                value: "NO_EDITAL");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "fase_canonica",
                keyColumn: "id",
                keyValue: new Guid("f45e0000-0000-7000-8000-000000000013"),
                column: "origem_data",
                value: "NO_EDITAL");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "fase_canonica",
                keyColumn: "id",
                keyValue: new Guid("f45e0000-0000-7000-8000-000000000014"),
                column: "origem_data",
                value: "NA_EXECUCAO");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "fase_canonica",
                keyColumn: "id",
                keyValue: new Guid("f45e0000-0000-7000-8000-000000000015"),
                column: "origem_data",
                value: "NA_EXECUCAO");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "fase_canonica",
                keyColumn: "id",
                keyValue: new Guid("f45e0000-0000-7000-8000-000000000016"),
                column: "origem_data",
                value: "NO_EDITAL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_fase_canonica_origem_data",
                schema: "configuracao",
                table: "fase_canonica",
                sql: "origem_data IN ('NO_EDITAL', 'NA_EXECUCAO')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_fase_canonica_origem_data",
                schema: "configuracao",
                table: "fase_canonica");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "fase_canonica",
                keyColumn: "id",
                keyValue: new Guid("f45e0000-0000-7000-8000-000000000001"),
                column: "origem_data",
                value: "PROPRIA");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "fase_canonica",
                keyColumn: "id",
                keyValue: new Guid("f45e0000-0000-7000-8000-000000000002"),
                column: "origem_data",
                value: "PROPRIA");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "fase_canonica",
                keyColumn: "id",
                keyValue: new Guid("f45e0000-0000-7000-8000-000000000003"),
                column: "origem_data",
                value: "PROPRIA");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "fase_canonica",
                keyColumn: "id",
                keyValue: new Guid("f45e0000-0000-7000-8000-000000000004"),
                column: "origem_data",
                value: "PROPRIA");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "fase_canonica",
                keyColumn: "id",
                keyValue: new Guid("f45e0000-0000-7000-8000-000000000005"),
                column: "origem_data",
                value: "PROPRIA");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "fase_canonica",
                keyColumn: "id",
                keyValue: new Guid("f45e0000-0000-7000-8000-000000000006"),
                column: "origem_data",
                value: "PROPRIA");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "fase_canonica",
                keyColumn: "id",
                keyValue: new Guid("f45e0000-0000-7000-8000-000000000007"),
                column: "origem_data",
                value: "PROPRIA");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "fase_canonica",
                keyColumn: "id",
                keyValue: new Guid("f45e0000-0000-7000-8000-000000000008"),
                column: "origem_data",
                value: "PROPRIA");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "fase_canonica",
                keyColumn: "id",
                keyValue: new Guid("f45e0000-0000-7000-8000-000000000009"),
                column: "origem_data",
                value: "PROPRIA");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "fase_canonica",
                keyColumn: "id",
                keyValue: new Guid("f45e0000-0000-7000-8000-000000000010"),
                column: "origem_data",
                value: "PROPRIA");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "fase_canonica",
                keyColumn: "id",
                keyValue: new Guid("f45e0000-0000-7000-8000-000000000011"),
                column: "origem_data",
                value: "PROPRIA");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "fase_canonica",
                keyColumn: "id",
                keyValue: new Guid("f45e0000-0000-7000-8000-000000000012"),
                column: "origem_data",
                value: "PROPRIA");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "fase_canonica",
                keyColumn: "id",
                keyValue: new Guid("f45e0000-0000-7000-8000-000000000013"),
                column: "origem_data",
                value: "PROPRIA");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "fase_canonica",
                keyColumn: "id",
                keyValue: new Guid("f45e0000-0000-7000-8000-000000000014"),
                column: "origem_data",
                value: "DELEGADA");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "fase_canonica",
                keyColumn: "id",
                keyValue: new Guid("f45e0000-0000-7000-8000-000000000015"),
                column: "origem_data",
                value: "DELEGADA");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "fase_canonica",
                keyColumn: "id",
                keyValue: new Guid("f45e0000-0000-7000-8000-000000000016"),
                column: "origem_data",
                value: "PROPRIA");

            migrationBuilder.AddCheckConstraint(
                name: "ck_fase_canonica_origem_data",
                schema: "configuracao",
                table: "fase_canonica",
                sql: "origem_data IN ('PROPRIA', 'DELEGADA')");
        }
    }
}
