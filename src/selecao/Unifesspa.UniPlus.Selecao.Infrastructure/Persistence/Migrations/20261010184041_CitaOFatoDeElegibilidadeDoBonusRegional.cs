using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CitaOFatoDeElegibilidadeDoBonusRegional : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "fato_elegibilidade_codigo",
                schema: "selecao",
                table: "configuracoes_bonus_regional",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "fato_elegibilidade_codigo",
                schema: "selecao",
                table: "configuracoes_bonus_regional");
        }
    }
}
