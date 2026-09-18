using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class IndiceDaRevisaoDaVitrine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_certames_divulgados_revisao",
                schema: "selecao",
                table: "certames_divulgados",
                column: "divulgado_em");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_certames_divulgados_revisao",
                schema: "selecao",
                table: "certames_divulgados");
        }
    }
}
