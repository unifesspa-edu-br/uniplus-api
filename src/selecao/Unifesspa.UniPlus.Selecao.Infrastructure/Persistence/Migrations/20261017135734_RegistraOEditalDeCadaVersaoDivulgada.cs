using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RegistraOEditalDeCadaVersaoDivulgada : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "editais_divulgados",
                schema: "selecao",
                table: "certames_divulgados",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb",
                comment: "O edital de cada versão divulgada, com o ato que a criou e o endereço no acervo público.");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "editais_divulgados",
                schema: "selecao",
                table: "certames_divulgados");
        }
    }
}
