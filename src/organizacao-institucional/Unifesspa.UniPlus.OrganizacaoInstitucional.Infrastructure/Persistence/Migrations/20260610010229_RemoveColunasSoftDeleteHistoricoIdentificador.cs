using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveColunasSoftDeleteHistoricoIdentificador : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Issue #629: soft-delete passa a ser opt-in via ISoftDeletable.
            // UnidadeIdentificadorHistorico é append-only (não implementa a
            // interface) e perde as colunas antes herdadas de EntityBase.
            // Destrutivo, mas seguro: o histórico nunca era soft-deletado em
            // fluxo normal (is_deleted sempre false) e a vigência continua
            // encerrada por vigencia_fim — comportamento preservado.
            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "unidade_identificador_historico");

            migrationBuilder.DropColumn(
                name: "deleted_by",
                table: "unidade_identificador_historico");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "unidade_identificador_historico");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Forward-only per ADR-0054 §J: nova migration "Reverte..." é o
            // mecanismo canônico de revert. Re-adicionar as colunas aqui
            // reintroduziria soft-delete numa entidade append-only — caminho
            // proibido.
            throw new NotSupportedException("Forward-only migration per ADR-0054 §J.");
        }
    }
}
