using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CongelaAHipoteseLegalEAFinalidadeNoFatoColetado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "finalidade_tratamento",
                schema: "selecao",
                table: "fatos_coletados",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "hipotese_legal",
                schema: "selecao",
                table: "fatos_coletados",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "finalidade_tratamento",
                schema: "selecao",
                table: "fatos_coletados");

            migrationBuilder.DropColumn(
                name: "hipotese_legal",
                schema: "selecao",
                table: "fatos_coletados");
        }
    }
}
