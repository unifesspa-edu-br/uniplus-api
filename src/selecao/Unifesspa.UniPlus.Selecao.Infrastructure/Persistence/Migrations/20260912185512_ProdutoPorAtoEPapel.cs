using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProdutoPorAtoEPapel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_produtos_da_fase_ato",
                schema: "selecao",
                table: "produtos_da_fase");

            migrationBuilder.DropIndex(
                name: "ux_produtos_da_etapa_ato",
                schema: "selecao",
                table: "produtos_da_etapa");

            migrationBuilder.CreateIndex(
                name: "ux_produtos_da_fase_ato",
                schema: "selecao",
                table: "produtos_da_fase",
                columns: new[] { "fase_cronograma_id", "ato_codigo", "papel" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ux_produtos_da_etapa_ato",
                schema: "selecao",
                table: "produtos_da_etapa",
                columns: new[] { "etapa_processo_id", "ato_codigo", "papel" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_produtos_da_fase_ato",
                schema: "selecao",
                table: "produtos_da_fase");

            migrationBuilder.DropIndex(
                name: "ux_produtos_da_etapa_ato",
                schema: "selecao",
                table: "produtos_da_etapa");

            migrationBuilder.CreateIndex(
                name: "ux_produtos_da_fase_ato",
                schema: "selecao",
                table: "produtos_da_fase",
                columns: new[] { "fase_cronograma_id", "ato_codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_produtos_da_etapa_ato",
                schema: "selecao",
                table: "produtos_da_etapa",
                columns: new[] { "etapa_processo_id", "ato_codigo" },
                unique: true);
        }
    }
}
