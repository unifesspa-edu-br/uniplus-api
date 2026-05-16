using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddObrigatoriedadeLegalRegraCodigoUniquePartial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ux_obrigatoriedades_legais_regra_codigo_ativos",
                table: "obrigatoriedades_legais",
                column: "regra_codigo",
                unique: true,
                filter: "is_deleted = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_obrigatoriedades_legais_regra_codigo_ativos",
                table: "obrigatoriedades_legais");
        }
    }
}
