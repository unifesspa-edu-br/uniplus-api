using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SubstituiOProdutoDoAtoPelaIdentidadeDoItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_atos_do_processo_produto_original",
                schema: "selecao",
                table: "atos_do_processo");

            migrationBuilder.DropColumn(
                name: "produto_do_edital_id",
                schema: "selecao",
                table: "atos_do_processo");

            migrationBuilder.AddColumn<string>(
                name: "item_do_edital",
                schema: "selecao",
                table: "atos_do_processo",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ux_atos_do_processo_item_original",
                schema: "selecao",
                table: "atos_do_processo",
                columns: new[] { "processo_seletivo_id", "item_do_edital" },
                unique: true,
                filter: "item_do_edital IS NOT NULL AND ato_corrigido_id IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_atos_do_processo_item_original",
                schema: "selecao",
                table: "atos_do_processo");

            migrationBuilder.DropColumn(
                name: "item_do_edital",
                schema: "selecao",
                table: "atos_do_processo");

            migrationBuilder.AddColumn<Guid>(
                name: "produto_do_edital_id",
                schema: "selecao",
                table: "atos_do_processo",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ux_atos_do_processo_produto_original",
                schema: "selecao",
                table: "atos_do_processo",
                columns: new[] { "processo_seletivo_id", "produto_do_edital_id" },
                unique: true,
                filter: "produto_do_edital_id IS NOT NULL AND ato_corrigido_id IS NULL");
        }
    }
}
