using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PermiteCancelarAInscricaoEnviada : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_inscricoes_processo_cpf_indice",
                schema: "selecao",
                table: "inscricoes");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "cancelada_em",
                schema: "selecao",
                table: "inscricoes",
                type: "timestamp with time zone",
                nullable: true,
                comment: "O instante em que o candidato cancelou a inscrição enviada; nulo se não foi cancelada.");

            migrationBuilder.AddColumn<string>(
                name: "cancelada_por",
                schema: "selecao",
                table: "inscricoes",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true,
                comment: "O sub do token de quem cancelou; nulo se não foi cancelada.");

            migrationBuilder.AddColumn<string>(
                name: "motivo_do_cancelamento",
                schema: "selecao",
                table: "inscricoes",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                comment: "O motivo do cancelamento; 'a pedido do candidato' quando o candidato não o disse. Nulo se não foi cancelada.");

            migrationBuilder.CreateIndex(
                name: "ux_inscricoes_processo_cpf_indice",
                schema: "selecao",
                table: "inscricoes",
                columns: new[] { "processo_seletivo_id", "cpf_indice" },
                unique: true,
                filter: "situacao <> 3");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_inscricoes_processo_cpf_indice",
                schema: "selecao",
                table: "inscricoes");

            migrationBuilder.DropColumn(
                name: "cancelada_em",
                schema: "selecao",
                table: "inscricoes");

            migrationBuilder.DropColumn(
                name: "cancelada_por",
                schema: "selecao",
                table: "inscricoes");

            migrationBuilder.DropColumn(
                name: "motivo_do_cancelamento",
                schema: "selecao",
                table: "inscricoes");

            migrationBuilder.CreateIndex(
                name: "ux_inscricoes_processo_cpf_indice",
                schema: "selecao",
                table: "inscricoes",
                columns: new[] { "processo_seletivo_id", "cpf_indice" },
                unique: true);
        }
    }
}
