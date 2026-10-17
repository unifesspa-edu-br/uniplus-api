using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GravaQuemConfirmouAPoliticaEPermiteDocumentoReaproveitado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_atos_do_processo_documento_principal",
                schema: "selecao",
                table: "atos_do_processo");

            migrationBuilder.AddColumn<string>(
                name: "politica_de_divulgacao_confirmada_por_sub",
                schema: "selecao",
                table: "atos_do_processo",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_atos_do_processo_documento_principal_id",
                schema: "selecao",
                table: "atos_do_processo",
                column: "documento_principal_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_atos_do_processo_documento_principal_id",
                schema: "selecao",
                table: "atos_do_processo");

            migrationBuilder.DropColumn(
                name: "politica_de_divulgacao_confirmada_por_sub",
                schema: "selecao",
                table: "atos_do_processo");

            migrationBuilder.CreateIndex(
                name: "ux_atos_do_processo_documento_principal",
                schema: "selecao",
                table: "atos_do_processo",
                column: "documento_principal_id",
                unique: true);
        }
    }
}
