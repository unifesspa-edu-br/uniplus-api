using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveAreasOrganizacionais : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Issue #625: o cadastro de governança AreaOrganizacional é descartado.
            // O modelo de autorização PBAC+ABAC (Epic #600, ADR-0078) substitui o
            // escopo por Área, então a tabela e seu read-side deixam de existir.
            // Destrutivo, mas seguro: ambiente dev/scaffolding sem dados reais nem
            // HML/PROD.
            migrationBuilder.DropTable(
                name: "areas_organizacionais");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Forward-only per ADR-0054 §J: nova migration "Reverte..." é o
            // mecanismo canônico de revert. Recriar a tabela aqui ressuscitaria um
            // cadastro deliberadamente aposentado.
            throw new NotSupportedException("Forward-only migration per ADR-0054 §J.");
        }
    }
}
