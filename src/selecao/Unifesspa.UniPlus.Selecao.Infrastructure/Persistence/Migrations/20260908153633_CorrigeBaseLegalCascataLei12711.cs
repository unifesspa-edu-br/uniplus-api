using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CorrigeBaseLegalCascataLei12711 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                schema: "selecao",
                table: "rol_de_regras",
                keyColumn: "id",
                keyValue: new Guid("d0a00000-0000-7000-8000-000000000019"),
                columns: new[] { "base_legal", "hash" },
                values: new object[] { "Portaria MEC nº 704/2025 (DOU 20/10/2025, Seção 1, p. 36-37), art. 2º e Anexo — insere o art. 20-A na Portaria Normativa MEC nº 21/2012; Lei 12.711/2012 art. 3º §1º (red. Lei 14.723/2023)", "70f96f9fa5b2c5282adba8028070fdf0a82bef99408693d17d913c51d0c5078a" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                schema: "selecao",
                table: "rol_de_regras",
                keyColumn: "id",
                keyValue: new Guid("d0a00000-0000-7000-8000-000000000019"),
                columns: new[] { "base_legal", "hash" },
                values: new object[] { "ADR-0120; Portaria MEC nº 704/2025 (DOU 20/10/2025, Seção 1, p. 36-37), art. 20-A e Anexo — insere o art. 20-A na Portaria Normativa MEC nº 18/2012; Lei 12.711/2012 art. 3º §1º (red. Lei 14.723/2023)", "8e5cfbbf06b2a661b024ae1efc7db8f5013b434b316f6b69c3be9a3c90999626" });
        }
    }
}
