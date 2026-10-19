using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TornaUnicaAPublicacaoOriginalDoProduto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ux_atos_do_processo_produto_original",
                schema: "selecao",
                table: "atos_do_processo",
                columns: new[] { "processo_seletivo_id", "produto_do_edital_id" },
                unique: true,
                filter: "produto_do_edital_id IS NOT NULL AND ato_corrigido_id IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_atos_do_processo_produto_original",
                schema: "selecao",
                table: "atos_do_processo");
        }
    }
}
