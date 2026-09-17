using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaPeriodoVigenteDoCertame : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "periodo_inscricao_fim_vigente",
                schema: "selecao",
                table: "processos_seletivos",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "periodo_inscricao_inicio_vigente",
                schema: "selecao",
                table: "processos_seletivos",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_processos_seletivos_vitrine_prazo",
                schema: "selecao",
                table: "processos_seletivos",
                columns: new[] { "periodo_inscricao_fim_vigente", "id" },
                filter: "periodo_inscricao_fim_vigente IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_processos_seletivos_vitrine_prazo",
                schema: "selecao",
                table: "processos_seletivos");

            migrationBuilder.DropColumn(
                name: "periodo_inscricao_fim_vigente",
                schema: "selecao",
                table: "processos_seletivos");

            migrationBuilder.DropColumn(
                name: "periodo_inscricao_inicio_vigente",
                schema: "selecao",
                table: "processos_seletivos");
        }
    }
}
