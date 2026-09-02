using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaColetaSolicitacaoIsencaoNaFaseCanonica : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "coleta_solicitacao_isencao",
                schema: "configuracao",
                table: "fase_canonica",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // A fase de isenção é a única do catálogo que abre a janela de pedido de isenção; as
            // demais ficam com o default da coluna.
            //
            // Por CÓDIGO, e não pelo id do seed: a migration que semeou o catálogo usa
            // ON CONFLICT DO NOTHING, então uma base onde o operador já havia criado a fase manteve
            // a linha dele, com id próprio. Mirar no id determinístico não alcançaria essa linha, e
            // a fase viva ficaria sem a marca — com ela, todo cronograma novo passaria pelas
            // validações da janela sem que nenhuma se aplicasse.
            migrationBuilder.Sql(
                """
                UPDATE configuracao.fase_canonica
                   SET coleta_solicitacao_isencao = true
                 WHERE codigo = 'SOLICITACAO_ISENCAO'
                   AND is_deleted = false;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "coleta_solicitacao_isencao",
                schema: "configuracao",
                table: "fase_canonica");
        }
    }
}
