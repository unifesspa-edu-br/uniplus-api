using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FatosDasVersoesPublicadasNoRascunhoDeRetificacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Sem default: só a abertura sabe o que as versões publicadas coletaram, e o texto vazio
            // nem é jsonb válido. Sessão aberta antes desta migration é descartada pela API antes do
            // deploy; apagar a linha aqui deixaria a configuração editada sem a sessão que a repõe.
            migrationBuilder.AddColumn<string>(
                name: "fatos_das_versoes_publicadas",
                schema: "selecao",
                table: "rascunhos_retificacao",
                type: "jsonb",
                nullable: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "fatos_das_versoes_publicadas",
                schema: "selecao",
                table: "rascunhos_retificacao");
        }
    }
}
