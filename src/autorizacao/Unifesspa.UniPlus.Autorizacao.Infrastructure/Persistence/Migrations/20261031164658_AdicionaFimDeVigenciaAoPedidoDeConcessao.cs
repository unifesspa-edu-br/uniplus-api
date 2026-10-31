using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaFimDeVigenciaAoPedidoDeConcessao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "valida_ate",
                schema: "autorizacao",
                table: "pedidos_de_concessao",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "valida_ate_sugerida",
                schema: "autorizacao",
                table: "pedidos_de_concessao",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "valida_ate",
                schema: "autorizacao",
                table: "pedidos_de_concessao");

            migrationBuilder.DropColumn(
                name: "valida_ate_sugerida",
                schema: "autorizacao",
                table: "pedidos_de_concessao");
        }
    }
}
