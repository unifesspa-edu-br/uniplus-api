using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaDocumentosAoComprovante : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "documentos",
                schema: "selecao",
                table: "comprovantes_inscricao",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]",
                comment: "As exigências documentais da inscrição neste envio, com a situação, a consequência escrita e, de cada arquivo, SHA-256, tamanho e formato — nunca nome nem local de armazenamento.");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "documentos",
                schema: "selecao",
                table: "comprovantes_inscricao");
        }
    }
}
