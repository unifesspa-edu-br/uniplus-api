using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GravaAConfirmacaoDoCepDoEndereco : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "cep_confirmado",
                schema: "selecao",
                table: "enderecos_da_inscricao",
                type: "boolean",
                nullable: true,
                comment: "O candidato confirmou que o CEP é o geral da cidade ou do bairro, quando o Geo só o resolveu por faixa.");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "cep_confirmado",
                schema: "selecao",
                table: "enderecos_da_inscricao");
        }
    }
}
