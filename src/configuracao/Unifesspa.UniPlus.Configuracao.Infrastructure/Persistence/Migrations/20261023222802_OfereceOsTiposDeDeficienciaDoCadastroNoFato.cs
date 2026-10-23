using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OfereceOsTiposDeDeficienciaDoCadastroNoFato : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_rol_de_fatos_candidato_fonte_valores_coerente",
                schema: "configuracao",
                table: "rol_de_fatos_candidato");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000011"),
                column: "fonte_valores",
                value: "TIPOS_DEFICIENCIA");

            migrationBuilder.AddCheckConstraint(
                name: "ck_rol_de_fatos_candidato_fonte_valores_coerente",
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                sql: "(dominio = 'CATEGORICO' AND fonte_valores IS NOT NULL AND fonte_valores IN ('GLOBAL', 'PROCESSO', 'MODALIDADE', 'MUNICIPIOS_BONUS', 'GEO_UF', 'GEO_MUNICIPIO', 'TIPOS_DEFICIENCIA')) OR (dominio <> 'CATEGORICO' AND fonte_valores IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_rol_de_fatos_candidato_fonte_valores_coerente",
                schema: "configuracao",
                table: "rol_de_fatos_candidato");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000011"),
                column: "fonte_valores",
                value: "PROCESSO");

            migrationBuilder.AddCheckConstraint(
                name: "ck_rol_de_fatos_candidato_fonte_valores_coerente",
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                sql: "(dominio = 'CATEGORICO' AND fonte_valores IS NOT NULL AND fonte_valores IN ('GLOBAL', 'PROCESSO', 'MODALIDADE', 'MUNICIPIOS_BONUS', 'GEO_UF', 'GEO_MUNICIPIO')) OR (dominio <> 'CATEGORICO' AND fonte_valores IS NULL)");
        }
    }
}
