using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaONomeDoDocumentoNaApresentacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Sem DEFAULT de propósito: o nome vazio não é um valor válido. Com apresentações já gravadas a coluna não
            // se adiciona; o deploy faz o backup e apaga as inscrições antes de aplicar a migration.
            migrationBuilder.AddColumn<string>(
                name: "nome_do_documento",
                schema: "selecao",
                table: "apresentacoes_de_documento",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                comment: "O nome do documento como a exigência o chamava no envio; dá nome ao arquivo baixado.");

            migrationBuilder.AddCheckConstraint(
                name: "ck_apresentacoes_de_documento_nome_do_documento_preenchido",
                schema: "selecao",
                table: "apresentacoes_de_documento",
                sql: "btrim(nome_do_documento) <> ''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_apresentacoes_de_documento_nome_do_documento_preenchido",
                schema: "selecao",
                table: "apresentacoes_de_documento");

            migrationBuilder.DropColumn(
                name: "nome_do_documento",
                schema: "selecao",
                table: "apresentacoes_de_documento");
        }
    }
}
