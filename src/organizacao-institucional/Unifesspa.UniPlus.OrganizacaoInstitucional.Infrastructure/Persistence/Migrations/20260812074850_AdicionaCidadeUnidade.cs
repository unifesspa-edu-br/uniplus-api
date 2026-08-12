using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaCidadeUnidade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "cidade_codigo_ibge",
                schema: "organizacao",
                table: "unidade",
                type: "character(7)",
                fixedLength: true,
                maxLength: 7,
                nullable: true,
                comment: "Código IBGE (7 dígitos) da cidade da Unidade — referência ao Geo, sem FK cross-banco; opcional all-or-nothing com nome/UF.");

            migrationBuilder.AddColumn<string>(
                name: "cidade_nome",
                schema: "organizacao",
                table: "unidade",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true,
                comment: "Nome de exibição da cidade (display cache) — snapshot do Geo no momento do cadastro/atualização.");

            migrationBuilder.AddColumn<string>(
                name: "cidade_uf",
                schema: "organizacao",
                table: "unidade",
                type: "character(2)",
                fixedLength: true,
                maxLength: 2,
                nullable: true,
                comment: "UF da cidade (display cache) — snapshot do Geo no momento do cadastro/atualização.");

            migrationBuilder.AddCheckConstraint(
                name: "ck_unidade_cidade_completa",
                schema: "organizacao",
                table: "unidade",
                sql: "(cidade_codigo_ibge IS NULL AND cidade_nome IS NULL AND cidade_uf IS NULL) OR (cidade_codigo_ibge IS NOT NULL AND cidade_nome IS NOT NULL AND cidade_uf IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_unidade_cidade_completa",
                schema: "organizacao",
                table: "unidade");

            migrationBuilder.DropColumn(
                name: "cidade_codigo_ibge",
                schema: "organizacao",
                table: "unidade");

            migrationBuilder.DropColumn(
                name: "cidade_nome",
                schema: "organizacao",
                table: "unidade");

            migrationBuilder.DropColumn(
                name: "cidade_uf",
                schema: "organizacao",
                table: "unidade");
        }
    }
}
