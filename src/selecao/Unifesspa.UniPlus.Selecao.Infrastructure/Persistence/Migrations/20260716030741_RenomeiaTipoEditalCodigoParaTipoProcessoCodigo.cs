using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenomeiaTipoEditalCodigoParaTipoProcessoCodigo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "tipo_edital_codigo",
                schema: "selecao",
                table: "obrigatoriedades_legais",
                newName: "tipo_processo_codigo");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "tipo_processo_codigo",
                schema: "selecao",
                table: "obrigatoriedades_legais",
                newName: "tipo_edital_codigo");
        }
    }
}
