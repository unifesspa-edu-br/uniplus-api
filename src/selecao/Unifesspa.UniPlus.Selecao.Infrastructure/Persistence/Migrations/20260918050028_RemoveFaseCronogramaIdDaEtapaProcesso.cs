using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveFaseCronogramaIdDaEtapaProcesso : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_etapas_processo_fase_cronograma_id",
                schema: "selecao",
                table: "etapas_processo");

            migrationBuilder.DropColumn(
                name: "fase_cronograma_id",
                schema: "selecao",
                table: "etapas_processo");

            migrationBuilder.AlterColumn<string>(
                name: "fase_codigo",
                schema: "selecao",
                table: "etapas_processo",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true,
                comment: "Código canônico da fase em que a etapa acontece; é por ele que a raiz a resolve no cronograma.",
                oldClrType: typeof(string),
                oldType: "character varying(60)",
                oldMaxLength: 60,
                oldNullable: true,
                oldComment: "Código canônico da fase declarada pelo cliente; a raiz o resolve para fase_cronograma_id.");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "fase_codigo",
                schema: "selecao",
                table: "etapas_processo",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true,
                comment: "Código canônico da fase declarada pelo cliente; a raiz o resolve para fase_cronograma_id.",
                oldClrType: typeof(string),
                oldType: "character varying(60)",
                oldMaxLength: 60,
                oldNullable: true,
                oldComment: "Código canônico da fase em que a etapa acontece; é por ele que a raiz a resolve no cronograma.");

            migrationBuilder.AddColumn<Guid>(
                name: "fase_cronograma_id",
                schema: "selecao",
                table: "etapas_processo",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                comment: "Fase do cronograma a que a etapa pertence, resolvida a partir de fase_codigo.");

            migrationBuilder.CreateIndex(
                name: "ix_etapas_processo_fase_cronograma_id",
                schema: "selecao",
                table: "etapas_processo",
                column: "fase_cronograma_id");
        }
    }
}
