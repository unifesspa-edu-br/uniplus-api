using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaNotaMaximaEPassoDaEtapa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "nota_maxima",
                schema: "selecao",
                table: "etapas_processo",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true,
                comment: "Nota máxima da escala da etapa com nota; ausente na etapa sem nota e na do ENEM.");

            migrationBuilder.AddColumn<decimal>(
                name: "passo_do_lancamento",
                schema: "selecao",
                table: "etapas_processo",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true,
                comment: "De quanto em quanto a nota pode ser lançada; vale só para a nota digitada.");

            migrationBuilder.AddCheckConstraint(
                name: "ck_etapas_processo_nota_maxima_positiva",
                schema: "selecao",
                table: "etapas_processo",
                sql: "nota_maxima IS NULL OR nota_maxima > 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_etapas_processo_passo_positivo",
                schema: "selecao",
                table: "etapas_processo",
                sql: "passo_do_lancamento IS NULL OR passo_do_lancamento > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_etapas_processo_nota_maxima_positiva",
                schema: "selecao",
                table: "etapas_processo");

            migrationBuilder.DropCheckConstraint(
                name: "ck_etapas_processo_passo_positivo",
                schema: "selecao",
                table: "etapas_processo");

            migrationBuilder.DropColumn(
                name: "nota_maxima",
                schema: "selecao",
                table: "etapas_processo");

            migrationBuilder.DropColumn(
                name: "passo_do_lancamento",
                schema: "selecao",
                table: "etapas_processo");
        }
    }
}
