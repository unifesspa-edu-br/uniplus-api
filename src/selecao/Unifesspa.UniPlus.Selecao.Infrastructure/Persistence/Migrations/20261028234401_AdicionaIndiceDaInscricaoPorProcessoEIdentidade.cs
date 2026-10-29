using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaIndiceDaInscricaoPorProcessoEIdentidade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_inscricoes_processo_sub",
                schema: "selecao",
                table: "inscricoes",
                columns: new[] { "processo_seletivo_id", "sub" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_inscricoes_processo_sub",
                schema: "selecao",
                table: "inscricoes");
        }
    }
}
