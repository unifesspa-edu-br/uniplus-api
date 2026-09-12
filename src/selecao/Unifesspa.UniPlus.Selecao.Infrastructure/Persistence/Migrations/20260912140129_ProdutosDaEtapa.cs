using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProdutosDaEtapa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "produtos_da_etapa",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    etapa_processo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ato_codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    papel = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_produtos_da_etapa", x => x.id);
                    table.ForeignKey(
                        name: "fk_produtos_da_etapa_etapas_processo_etapa_processo_id",
                        column: x => x.etapa_processo_id,
                        principalSchema: "selecao",
                        principalTable: "etapas_processo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_produtos_da_etapa_ato",
                schema: "selecao",
                table: "produtos_da_etapa",
                columns: new[] { "etapa_processo_id", "ato_codigo" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "produtos_da_etapa",
                schema: "selecao");
        }
    }
}
