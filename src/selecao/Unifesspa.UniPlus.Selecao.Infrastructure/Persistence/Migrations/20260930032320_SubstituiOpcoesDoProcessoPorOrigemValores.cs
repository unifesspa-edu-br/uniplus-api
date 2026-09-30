using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SubstituiOpcoesDoProcessoPorOrigemValores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "opcoes_do_processo",
                schema: "selecao",
                table: "fatos_coletados");

            migrationBuilder.AddColumn<int>(
                name: "origem_valores",
                schema: "selecao",
                table: "fatos_coletados",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "origem_valores",
                schema: "selecao",
                table: "fatos_coletados");

            migrationBuilder.AddColumn<bool>(
                name: "opcoes_do_processo",
                schema: "selecao",
                table: "fatos_coletados",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
