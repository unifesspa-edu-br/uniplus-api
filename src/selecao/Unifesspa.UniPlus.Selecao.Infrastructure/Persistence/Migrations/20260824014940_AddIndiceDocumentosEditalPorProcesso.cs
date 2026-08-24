using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIndiceDocumentosEditalPorProcesso : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_documentos_edital_processo_seletivo_id",
                schema: "selecao",
                table: "documentos_edital");

            migrationBuilder.CreateIndex(
                name: "ix_documentos_edital_processo_recentes",
                schema: "selecao",
                table: "documentos_edital",
                columns: new[] { "processo_seletivo_id", "created_at", "id" },
                descending: new[] { false, true, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_documentos_edital_processo_recentes",
                schema: "selecao",
                table: "documentos_edital");

            migrationBuilder.CreateIndex(
                name: "ix_documentos_edital_processo_seletivo_id",
                schema: "selecao",
                table: "documentos_edital",
                column: "processo_seletivo_id");
        }
    }
}
