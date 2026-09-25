using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RegistraIdentificadorLegivelDaVersaoBaseNaRetificacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "versao_base_com_identificador_legivel",
                schema: "selecao",
                table: "rascunhos_retificacao",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "versao_base_com_identificador_legivel",
                schema: "selecao",
                table: "rascunhos_retificacao");
        }
    }
}
