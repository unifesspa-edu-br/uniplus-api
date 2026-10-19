using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Publicacoes.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaOsAtributosDoDocumentoAvulsoAoTipoDeAto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "exige_ato_corrigido",
                schema: "publicacoes",
                table: "tipo_ato_publicado",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "publicavel_como_avulso",
                schema: "publicacoes",
                table: "tipo_ato_publicado",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "exige_ato_corrigido",
                schema: "publicacoes",
                table: "tipo_ato_publicado");

            migrationBuilder.DropColumn(
                name: "publicavel_como_avulso",
                schema: "publicacoes",
                table: "tipo_ato_publicado");
        }
    }
}
