using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CongelaNotaDeOrigemNoEnemDoTipoDeEtapa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // A coluna nasce com DEFAULT só para dar valor às etapas já gravadas, e o DEFAULT é
            // derrubado em seguida: quem declara a origem da nota é a gravação da etapa, que
            // copia do cadastro, não o banco.
            migrationBuilder.AddColumn<bool>(
                name: "tipo_etapa_nota_de_origem_no_enem",
                schema: "selecao",
                table: "etapas_processo",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                comment: "Nota da etapa vem do ENEM, congelado do tipo junto com a identidade.");

            migrationBuilder.Sql("""
                ALTER TABLE selecao.etapas_processo
                    ALTER COLUMN tipo_etapa_nota_de_origem_no_enem DROP DEFAULT;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "tipo_etapa_nota_de_origem_no_enem",
                schema: "selecao",
                table: "etapas_processo");
        }
    }
}
