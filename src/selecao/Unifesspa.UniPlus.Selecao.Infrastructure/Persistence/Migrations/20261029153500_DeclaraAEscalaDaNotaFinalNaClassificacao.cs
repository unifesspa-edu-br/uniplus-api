using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DeclaraAEscalaDaNotaFinalNaClassificacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "escala_nota_final",
                schema: "selecao",
                table: "configuracoes_classificacao",
                type: "numeric(9,4)",
                precision: 9,
                scale: 4,
                nullable: true,
                comment: "Escala em que a nota final é publicada (10, 100, 1.000...); a média ponderada das notas convertidas é multiplicada por ela. Nula na classificação importada e no rascunho que ainda não a declarou.");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "escala_nota_final",
                schema: "selecao",
                table: "configuracoes_classificacao");
        }
    }
}
