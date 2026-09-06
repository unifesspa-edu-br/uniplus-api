using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveAtoProduzidoEfeitoIrreversivelDaFaseCronograma : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ato_produzido_efeito_irreversivel",
                schema: "selecao",
                table: "fases_cronograma");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ato_produzido_efeito_irreversivel",
                schema: "selecao",
                table: "fases_cronograma",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
