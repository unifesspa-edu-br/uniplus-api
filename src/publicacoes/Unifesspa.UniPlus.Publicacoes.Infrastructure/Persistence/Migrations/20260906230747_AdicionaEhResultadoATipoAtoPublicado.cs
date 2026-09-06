using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Publicacoes.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaEhResultadoATipoAtoPublicado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "eh_resultado",
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
                name: "eh_resultado",
                schema: "publicacoes",
                table: "tipo_ato_publicado");
        }
    }
}
