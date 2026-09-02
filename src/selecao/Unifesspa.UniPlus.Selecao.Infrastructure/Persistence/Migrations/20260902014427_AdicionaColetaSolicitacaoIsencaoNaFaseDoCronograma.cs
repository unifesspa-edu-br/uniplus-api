using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaColetaSolicitacaoIsencaoNaFaseDoCronograma : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "coleta_solicitacao_isencao",
                schema: "selecao",
                table: "fases_cronograma",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "coleta_solicitacao_isencao",
                schema: "selecao",
                table: "fases_cronograma");
        }
    }
}
