using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveAreaDeInteresse : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // KILL do eixo de governança por Área (Epic #600, ADR-0078): a
            // junction temporal de áreas de interesse e a coluna proprietario
            // de ObrigatoriedadeLegal deixam de existir. O exclusion constraint
            // GIST cai junto com a tabela. Dev/scaffolding sem dados reais.
            migrationBuilder.DropTable(
                name: "obrigatoriedade_legal_areas_de_interesse");

            migrationBuilder.DropColumn(
                name: "proprietario",
                table: "obrigatoriedades_legais");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Forward-only per ADR-0054 §J: recriar a junction/coluna
            // ressuscitaria um eixo de governança deliberadamente aposentado.
            throw new NotSupportedException("Forward-only migration per ADR-0054 §J.");
        }
    }
}
