using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TornaUnicoOAtoCorrigidoDoProcesso : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_atos_do_processo_ato_corrigido",
                schema: "selecao",
                table: "atos_do_processo");

            migrationBuilder.CreateIndex(
                name: "ux_atos_do_processo_ato_corrigido",
                schema: "selecao",
                table: "atos_do_processo",
                column: "ato_corrigido_id",
                unique: true,
                filter: "ato_corrigido_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_atos_do_processo_ato_corrigido",
                schema: "selecao",
                table: "atos_do_processo");

            migrationBuilder.CreateIndex(
                name: "ix_atos_do_processo_ato_corrigido",
                schema: "selecao",
                table: "atos_do_processo",
                column: "ato_corrigido_id");
        }
    }
}
