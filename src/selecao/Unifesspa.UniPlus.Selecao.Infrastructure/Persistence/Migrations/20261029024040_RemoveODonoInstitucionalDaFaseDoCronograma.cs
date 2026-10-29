using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveODonoInstitucionalDaFaseDoCronograma : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "dono_institucional",
                schema: "selecao",
                table: "fases_cronograma");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "dono_institucional",
                schema: "selecao",
                table: "fases_cronograma",
                type: "character varying(60)",
                maxLength: 60,
                nullable: false,
                defaultValue: "");
        }
    }
}
