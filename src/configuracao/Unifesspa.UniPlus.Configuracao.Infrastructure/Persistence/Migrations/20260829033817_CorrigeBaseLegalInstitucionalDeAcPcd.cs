using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CorrigeBaseLegalInstitucionalDeAcPcd : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "modalidade",
                keyColumn: "id",
                keyValue: new Guid("70da1000-0000-7000-8000-000000000010"),
                column: "base_legal",
                value: "Res. Unifesspa 532/2021, art. 1º (reserva de vaga para pessoa com deficiência)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "modalidade",
                keyColumn: "id",
                keyValue: new Guid("70da1000-0000-7000-8000-000000000010"),
                column: "base_legal",
                value: "Lei 12.711/2012 (red. Lei 14.723/2023)");
        }
    }
}
