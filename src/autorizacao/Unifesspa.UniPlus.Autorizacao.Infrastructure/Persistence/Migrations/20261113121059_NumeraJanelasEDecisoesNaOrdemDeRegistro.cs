using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NumeraJanelasEDecisoesNaOrdemDeRegistro : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ordem_de_registro",
                schema: "autorizacao",
                table: "pedido_decisoes",
                type: "bigint",
                nullable: false,
                defaultValue: 0L)
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn);

            migrationBuilder.AddColumn<long>(
                name: "ordem_de_registro",
                schema: "autorizacao",
                table: "janelas_de_migracao",
                type: "bigint",
                nullable: false,
                defaultValue: 0L)
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn);

            migrationBuilder.AddColumn<long>(
                name: "ordem_de_registro",
                schema: "autorizacao",
                table: "decisoes_da_migracao",
                type: "bigint",
                nullable: false,
                defaultValue: 0L)
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn);

            migrationBuilder.CreateIndex(
                name: "ux_pedido_decisoes_ordem_de_registro",
                schema: "autorizacao",
                table: "pedido_decisoes",
                column: "ordem_de_registro",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_janelas_de_migracao_ordem_de_registro",
                schema: "autorizacao",
                table: "janelas_de_migracao",
                column: "ordem_de_registro",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_decisoes_da_migracao_ordem_de_registro",
                schema: "autorizacao",
                table: "decisoes_da_migracao",
                column: "ordem_de_registro",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_pedido_decisoes_ordem_de_registro",
                schema: "autorizacao",
                table: "pedido_decisoes");

            migrationBuilder.DropIndex(
                name: "ux_janelas_de_migracao_ordem_de_registro",
                schema: "autorizacao",
                table: "janelas_de_migracao");

            migrationBuilder.DropIndex(
                name: "ux_decisoes_da_migracao_ordem_de_registro",
                schema: "autorizacao",
                table: "decisoes_da_migracao");

            migrationBuilder.DropColumn(
                name: "ordem_de_registro",
                schema: "autorizacao",
                table: "pedido_decisoes");

            migrationBuilder.DropColumn(
                name: "ordem_de_registro",
                schema: "autorizacao",
                table: "janelas_de_migracao");

            migrationBuilder.DropColumn(
                name: "ordem_de_registro",
                schema: "autorizacao",
                table: "decisoes_da_migracao");
        }
    }
}
