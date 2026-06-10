using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RestringeFkHistoricoIdentificador : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Issue #629: troca a FK do histórico de identificadores de ON DELETE
            // CASCADE para NO ACTION (DeleteBehavior.ClientNoAction). O histórico é
            // append-only e não implementa ISoftDeletable; com CASCADE, soft-deletar
            // a Unidade (com o histórico carregado por ObterPorIdAsync) hard-deletava
            // as linhas via cascade em memória do EF. Com NO ACTION o cascade não
            // ocorre e a Unidade é soft-deletada (UPDATE, nunca DELETE físico),
            // preservando a trilha de auditoria.
            migrationBuilder.DropForeignKey(
                name: "fk_unidade_identificador_historico_unidade_unidade_id",
                table: "unidade_identificador_historico");

            migrationBuilder.AddForeignKey(
                name: "fk_unidade_identificador_historico_unidade_unidade_id",
                table: "unidade_identificador_historico",
                column: "unidade_id",
                principalTable: "unidade",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Forward-only per ADR-0054 §J: nova migration "Reverte..." é o
            // mecanismo canônico de revert. Voltar a FK para CASCADE reintroduziria
            // o hard-delete do histórico append-only — caminho proibido.
            throw new NotSupportedException("Forward-only migration per ADR-0054 §J.");
        }
    }
}
