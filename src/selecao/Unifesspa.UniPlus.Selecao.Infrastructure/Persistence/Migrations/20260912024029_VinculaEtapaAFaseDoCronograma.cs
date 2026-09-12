using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class VinculaEtapaAFaseDoCronograma : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "fase_codigo",
                schema: "selecao",
                table: "etapas_processo",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true,
                comment: "Código canônico da fase declarada pelo cliente; a raiz o resolve para fase_cronograma_id.");

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_etapas_processo_fase_cronograma_id",
                schema: "selecao",
                table: "etapas_processo");

            migrationBuilder.DropColumn(
                name: "fase_codigo",
                schema: "selecao",
                table: "etapas_processo");

            migrationBuilder.DropColumn(
                name: "fase_cronograma_id",
                schema: "selecao",
                table: "etapas_processo");
        }
    }
}
