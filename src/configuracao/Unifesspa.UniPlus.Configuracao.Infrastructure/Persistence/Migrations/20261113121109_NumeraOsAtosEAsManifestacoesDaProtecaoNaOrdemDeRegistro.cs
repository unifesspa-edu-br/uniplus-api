using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NumeraOsAtosEAsManifestacoesDaProtecaoNaOrdemDeRegistro : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ordem_de_registro",
                schema: "configuracao",
                table: "protecao_do_catalogo_manifestacoes",
                type: "bigint",
                nullable: false,
                defaultValue: 0L)
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn);

            migrationBuilder.AddColumn<long>(
                name: "ordem_de_registro",
                schema: "configuracao",
                table: "protecao_do_catalogo_atos",
                type: "bigint",
                nullable: false,
                defaultValue: 0L)
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn);

            migrationBuilder.CreateIndex(
                name: "ux_protecao_do_catalogo_manifestacoes_ordem_de_registro",
                schema: "configuracao",
                table: "protecao_do_catalogo_manifestacoes",
                column: "ordem_de_registro",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_protecao_do_catalogo_atos_ordem_de_registro",
                schema: "configuracao",
                table: "protecao_do_catalogo_atos",
                column: "ordem_de_registro",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_protecao_do_catalogo_manifestacoes_ordem_de_registro",
                schema: "configuracao",
                table: "protecao_do_catalogo_manifestacoes");

            migrationBuilder.DropIndex(
                name: "ux_protecao_do_catalogo_atos_ordem_de_registro",
                schema: "configuracao",
                table: "protecao_do_catalogo_atos");

            migrationBuilder.DropColumn(
                name: "ordem_de_registro",
                schema: "configuracao",
                table: "protecao_do_catalogo_manifestacoes");

            migrationBuilder.DropColumn(
                name: "ordem_de_registro",
                schema: "configuracao",
                table: "protecao_do_catalogo_atos");
        }
    }
}
