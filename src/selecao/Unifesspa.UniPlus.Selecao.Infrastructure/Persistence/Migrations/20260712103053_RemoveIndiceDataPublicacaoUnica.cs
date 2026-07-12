using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Remove a unicidade de <c>data_publicacao</c> por processo (ADR-0104): ela
    /// existia só para dar ordem total entre editais, e a ordem passa a vir de
    /// <c>UNIQUE(processo, numero_versao)</c> sobre as versões da configuração.
    /// Dois atos publicados no mesmo instante — e a retificação que republica a
    /// data do ato original — deixam de colidir.
    /// </summary>
    /// <remarks>
    /// O <c>Down</c> recria o índice e, por isso, <b>falha</b> se o banco já
    /// contiver dois editais publicados com a mesma data no mesmo processo — um
    /// estado que passa a ser válido a partir do <c>Up</c>. Reverter exige
    /// remediar essas linhas antes; a alternativa seria recriar o índice sem
    /// unicidade, o que não seria reverter, e sim inventar um terceiro estado.
    /// </remarks>
    public partial class RemoveIndiceDataPublicacaoUnica : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_editais_processo_data_publicacao",
                schema: "selecao",
                table: "editais");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ux_editais_processo_data_publicacao",
                schema: "selecao",
                table: "editais",
                columns: new[] { "processo_seletivo_id", "data_publicacao" },
                unique: true,
                filter: "data_publicacao IS NOT NULL");
        }
    }
}
