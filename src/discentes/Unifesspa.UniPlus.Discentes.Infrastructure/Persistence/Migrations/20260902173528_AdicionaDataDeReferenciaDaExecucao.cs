using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Discentes.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaDataDeReferenciaDaExecucao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "data_de_referencia",
                schema: "discentes",
                table: "sync_run",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1),
                comment: "Dia a que a execução se refere — não o instante em que rodou. Uma execução disparada de madrugada refere-se ao dia que começou.");

            migrationBuilder.CreateIndex(
                name: "ix_sync_run_data_de_referencia",
                schema: "discentes",
                table: "sync_run",
                column: "data_de_referencia");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_sync_run_data_de_referencia",
                schema: "discentes",
                table: "sync_run");

            migrationBuilder.DropColumn(
                name: "data_de_referencia",
                schema: "discentes",
                table: "sync_run");
        }
    }
}
