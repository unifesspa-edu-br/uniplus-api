using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExigenciaDocumentalPorEtapa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "exigido_na_etapa_id",
                schema: "selecao",
                table: "documentos_exigidos",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_documentos_exigidos_exigido_na_etapa_id",
                schema: "selecao",
                table: "documentos_exigidos",
                column: "exigido_na_etapa_id");

            migrationBuilder.AddForeignKey(
                name: "fk_documentos_exigidos_etapas_processo_exigido_na_etapa_id",
                schema: "selecao",
                table: "documentos_exigidos",
                column: "exigido_na_etapa_id",
                principalSchema: "selecao",
                principalTable: "etapas_processo",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_documentos_exigidos_etapas_processo_exigido_na_etapa_id",
                schema: "selecao",
                table: "documentos_exigidos");

            migrationBuilder.DropIndex(
                name: "ix_documentos_exigidos_exigido_na_etapa_id",
                schema: "selecao",
                table: "documentos_exigidos");

            migrationBuilder.DropColumn(
                name: "exigido_na_etapa_id",
                schema: "selecao",
                table: "documentos_exigidos");
        }
    }
}
