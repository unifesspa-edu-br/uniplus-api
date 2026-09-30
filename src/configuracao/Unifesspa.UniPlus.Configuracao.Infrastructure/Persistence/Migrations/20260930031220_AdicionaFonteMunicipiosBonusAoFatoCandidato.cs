using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaFonteMunicipiosBonusAoFatoCandidato : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_rol_de_fatos_candidato_fonte_valores_coerente",
                schema: "configuracao",
                table: "rol_de_fatos_candidato");

            migrationBuilder.AddCheckConstraint(
                name: "ck_rol_de_fatos_candidato_fonte_valores_coerente",
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                sql: "(dominio = 'CATEGORICO' AND fonte_valores IN ('GLOBAL', 'PROCESSO', 'MODALIDADE', 'MUNICIPIOS_BONUS')) OR (dominio <> 'CATEGORICO' AND fonte_valores IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_rol_de_fatos_candidato_fonte_valores_coerente",
                schema: "configuracao",
                table: "rol_de_fatos_candidato");

            migrationBuilder.AddCheckConstraint(
                name: "ck_rol_de_fatos_candidato_fonte_valores_coerente",
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                sql: "(dominio = 'CATEGORICO' AND fonte_valores IN ('GLOBAL', 'PROCESSO', 'MODALIDADE')) OR (dominio <> 'CATEGORICO' AND fonte_valores IS NULL)");
        }
    }
}
