using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaIndiceDeVencimentoDoRascunho : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_rascunhos_publicacao_expira_em",
                schema: "selecao",
                table: "rascunhos_publicacao",
                column: "expira_em");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_rascunhos_publicacao_expira_em",
                schema: "selecao",
                table: "rascunhos_publicacao");
        }
    }
}
