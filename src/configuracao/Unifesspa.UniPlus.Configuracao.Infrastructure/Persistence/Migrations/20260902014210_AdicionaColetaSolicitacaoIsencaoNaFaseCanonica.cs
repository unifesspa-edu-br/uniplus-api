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

            // A fase de isenção é a única do catálogo semeado que abre a janela de pedido de
            // isenção; as demais ficam com o default da coluna.
            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "fase_canonica",
                keyColumn: "id",
                keyValue: new Guid("f45e0000-0000-7000-8000-000000000002"),
                column: "coleta_solicitacao_isencao",
                value: true);
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
