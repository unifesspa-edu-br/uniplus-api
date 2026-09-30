using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ItemDeclaraObrigatoriedadeAjudaEConfirmacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Um booleano não se converte em obrigatoriedade com predicado; sem produção, os itens
            // saem em vez de migrar, e as colunas novas entram sem default persistido.
            migrationBuilder.Sql("DELETE FROM selecao.fatos_coletados;");

            migrationBuilder.DropColumn(
                name: "obrigatorio",
                schema: "selecao",
                table: "fatos_coletados");

            migrationBuilder.AddColumn<string>(
                name: "obrigatoriedade",
                schema: "selecao",
                table: "fatos_coletados",
                type: "jsonb",
                nullable: false);

            migrationBuilder.AddColumn<string>(
                name: "ajuda",
                schema: "selecao",
                table: "fatos_coletados",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "pedir_confirmacao",
                schema: "selecao",
                table: "fatos_coletados",
                type: "boolean",
                nullable: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM selecao.fatos_coletados;");

            migrationBuilder.DropColumn(
                name: "pedir_confirmacao",
                schema: "selecao",
                table: "fatos_coletados");

            migrationBuilder.DropColumn(
                name: "ajuda",
                schema: "selecao",
                table: "fatos_coletados");

            migrationBuilder.DropColumn(
                name: "obrigatoriedade",
                schema: "selecao",
                table: "fatos_coletados");

            migrationBuilder.AddColumn<bool>(
                name: "obrigatorio",
                schema: "selecao",
                table: "fatos_coletados",
                type: "boolean",
                nullable: false);
        }
    }
}
