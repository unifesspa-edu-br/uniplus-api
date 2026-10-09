using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DeclaraSeOProcessoAplicaOBonusRegional : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "aplica_bonus_regional",
                schema: "selecao",
                table: "processos_seletivos",
                type: "boolean",
                nullable: true,
                comment: "Declaração de que o processo aplica (true) ou não aplica (false) o bônus regional. Nulo = ainda não declarado.");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "aplica_bonus_regional",
                schema: "selecao",
                table: "processos_seletivos");
        }
    }
}
