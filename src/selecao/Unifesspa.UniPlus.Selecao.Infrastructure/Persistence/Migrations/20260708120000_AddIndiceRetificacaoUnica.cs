using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIndiceRetificacaoUnica : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Cadeia de retificação linear (ADR-0101/ADR-0102): cada Edital é
            // retificado no máximo uma vez. O índice de convenção da FK sobre
            // edital_retificado_id (não único) é promovido a índice único
            // parcial — simétrico a ux_editais_processo_abertura_unica. Fecha a
            // corrida de duas retificações concorrentes do mesmo Edital como
            // invariante de banco, sem manter um índice redundante: o parcial
            // continua cobrindo as buscas por FK nas linhas de retificação
            // (as de abertura têm edital_retificado_id nulo e nunca são alvo).
            migrationBuilder.DropIndex(
                name: "ix_editais_edital_retificado_id",
                schema: "selecao",
                table: "editais");

            migrationBuilder.CreateIndex(
                name: "ux_editais_edital_retificado_unico",
                schema: "selecao",
                table: "editais",
                column: "edital_retificado_id",
                unique: true,
                filter: "edital_retificado_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_editais_edital_retificado_unico",
                schema: "selecao",
                table: "editais");

            migrationBuilder.CreateIndex(
                name: "ix_editais_edital_retificado_id",
                schema: "selecao",
                table: "editais",
                column: "edital_retificado_id");
        }
    }
}
