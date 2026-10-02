using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RepetePorGrupoDoFormulario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // O tipo de entidade fixo dá lugar ao código do grupo do formulário: os valores antigos
            // não nomeiam grupo nenhum, então a coluna é recriada vazia.
            migrationBuilder.DropColumn(
                name: "repete_por_entidade",
                schema: "selecao",
                table: "nos_exigencia");

            migrationBuilder.AddColumn<string>(
                name: "repete_por_entidade",
                schema: "selecao",
                table: "nos_exigencia",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "repete_por_entidade",
                schema: "selecao",
                table: "nos_exigencia");

            migrationBuilder.AddColumn<int>(
                name: "repete_por_entidade",
                schema: "selecao",
                table: "nos_exigencia",
                type: "integer",
                nullable: true);
        }
    }
}
