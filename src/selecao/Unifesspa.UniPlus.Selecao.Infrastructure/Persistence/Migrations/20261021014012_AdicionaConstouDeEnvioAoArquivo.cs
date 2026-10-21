using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaConstouDeEnvioAoArquivo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "constou_de_envio",
                schema: "selecao",
                table: "arquivos_da_apresentacao",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                comment: "O arquivo confirmado já constou de um envio da inscrição; a limpeza dos envios seguintes nunca o apaga (UNI-REQ-0168).");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "constou_de_envio",
                schema: "selecao",
                table: "arquivos_da_apresentacao");
        }
    }
}
