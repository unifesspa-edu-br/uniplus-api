using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AtualizaOComentarioDaVersaoDoFormularioDaInscricao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "versao_formulario_id",
                schema: "selecao",
                table: "inscricoes",
                type: "uuid",
                nullable: false,
                comment: "A versão do formulário da inscrição (UNI-REQ-0084): a divulgada que o rascunho adotou por último; o primeiro envio a congela.",
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldComment: "A versão do formulário divulgada no instante do início (UNI-REQ-0084) — fixa, nunca recalculada.");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "versao_formulario_id",
                schema: "selecao",
                table: "inscricoes",
                type: "uuid",
                nullable: false,
                comment: "A versão do formulário divulgada no instante do início (UNI-REQ-0084) — fixa, nunca recalculada.",
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldComment: "A versão do formulário da inscrição (UNI-REQ-0084): a divulgada que o rascunho adotou por último; o primeiro envio a congela.");
        }
    }
}
