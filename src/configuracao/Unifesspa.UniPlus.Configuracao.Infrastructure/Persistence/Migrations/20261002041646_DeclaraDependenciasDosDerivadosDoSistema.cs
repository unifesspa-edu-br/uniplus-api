using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DeclaraDependenciasDosDerivadosDoSistema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string[]>(
                name: "dependencias",
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'::text[]");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000006"),
                column: "dependencias",
                value: new[] { "DATA_NASCIMENTO" });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000020"),
                column: "dependencias",
                value: new[] { "ENDERECO_RESIDENCIAL" });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000021"),
                column: "dependencias",
                value: new[] { "ENDERECO_RESIDENCIAL" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "dependencias",
                schema: "configuracao",
                table: "rol_de_fatos_candidato");
        }
    }
}
