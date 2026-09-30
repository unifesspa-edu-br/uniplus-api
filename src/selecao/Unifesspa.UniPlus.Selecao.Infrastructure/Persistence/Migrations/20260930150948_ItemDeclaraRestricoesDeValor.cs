using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ItemDeclaraRestricoesDeValor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Sem produção, os itens existentes saem, e a coluna entra sem default persistido.
            migrationBuilder.Sql("DELETE FROM selecao.fatos_coletados;");

            migrationBuilder.AddColumn<string>(
                name: "restricoes",
                schema: "selecao",
                table: "fatos_coletados",
                type: "jsonb",
                nullable: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "restricoes",
                schema: "selecao",
                table: "fatos_coletados");
        }
    }
}
