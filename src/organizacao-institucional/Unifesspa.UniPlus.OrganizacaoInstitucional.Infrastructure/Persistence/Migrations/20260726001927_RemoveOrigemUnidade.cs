using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveOrigemUnidade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "origem",
                schema: "organizacao",
                table: "unidade");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Migrations são forward-only (ADR-0054): reverter se faz por nova
            // migration, não por Down(). Recriar a coluna aqui seria enganoso —
            // ela voltaria NOT NULL com string vazia em todas as linhas, valor
            // que a versão anterior do domínio recusava.
        }
    }
}
