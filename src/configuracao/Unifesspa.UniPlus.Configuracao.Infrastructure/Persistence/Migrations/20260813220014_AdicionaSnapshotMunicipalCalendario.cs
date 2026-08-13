using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaSnapshotMunicipalCalendario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_dia_nao_util_municipio_coerente",
                schema: "configuracao",
                table: "dia_nao_util");

            migrationBuilder.AddColumn<string>(
                name: "municipio_nome",
                schema: "configuracao",
                table: "dia_nao_util",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "municipio_uf",
                schema: "configuracao",
                table: "dia_nao_util",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_dia_nao_util_municipio_coerente",
                schema: "configuracao",
                table: "dia_nao_util",
                sql: "(abrangencia = 'MUNICIPAL') = (municipio_ibge IS NOT NULL) AND (abrangencia = 'MUNICIPAL') = (municipio_nome IS NOT NULL) AND (abrangencia = 'MUNICIPAL') = (municipio_uf IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_dia_nao_util_municipio_coerente",
                schema: "configuracao",
                table: "dia_nao_util");

            migrationBuilder.DropColumn(
                name: "municipio_nome",
                schema: "configuracao",
                table: "dia_nao_util");

            migrationBuilder.DropColumn(
                name: "municipio_uf",
                schema: "configuracao",
                table: "dia_nao_util");

            migrationBuilder.AddCheckConstraint(
                name: "ck_dia_nao_util_municipio_coerente",
                schema: "configuracao",
                table: "dia_nao_util",
                sql: "(abrangencia = 'MUNICIPAL') = (municipio_ibge IS NOT NULL)");
        }
    }
}
