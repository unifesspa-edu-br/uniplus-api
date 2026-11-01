using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CongelaADescricaoEOComponenteDeExibicaoNoFatoColetado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "componente_exibicao",
                schema: "selecao",
                table: "fatos_coletados",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "descricao",
                schema: "selecao",
                table: "fatos_coletados",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "componente_exibicao",
                schema: "selecao",
                table: "fatos_coletados");

            migrationBuilder.DropColumn(
                name: "descricao",
                schema: "selecao",
                table: "fatos_coletados");
        }
    }
}
