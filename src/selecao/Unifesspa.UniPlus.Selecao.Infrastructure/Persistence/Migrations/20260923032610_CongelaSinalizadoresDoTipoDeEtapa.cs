using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CongelaSinalizadoresDoTipoDeEtapa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // As colunas nascem permissivas para que as etapas já gravadas tenham valor, e o
            // DEFAULT é derrubado em seguida: quem declara o que o tipo admitia é a gravação da
            // etapa, que copia do cadastro, não o banco.
            migrationBuilder.AddColumn<bool>(
                name: "tipo_etapa_admite_eliminacao",
                schema: "selecao",
                table: "etapas_processo",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "tipo_etapa_admite_pontuacao",
                schema: "selecao",
                table: "etapas_processo",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.Sql("""
                ALTER TABLE selecao.etapas_processo
                    ALTER COLUMN tipo_etapa_admite_pontuacao DROP DEFAULT,
                    ALTER COLUMN tipo_etapa_admite_eliminacao DROP DEFAULT;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "tipo_etapa_admite_eliminacao",
                schema: "selecao",
                table: "etapas_processo");

            migrationBuilder.DropColumn(
                name: "tipo_etapa_admite_pontuacao",
                schema: "selecao",
                table: "etapas_processo");
        }
    }
}
