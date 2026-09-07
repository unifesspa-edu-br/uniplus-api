using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RecorteDeCompetenciaDaBancaRequerida : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "categorias_julgadas",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    banca_requerida_id = table.Column<Guid>(type: "uuid", nullable: false),
                    categoria_documento_origem_id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_categorias_julgadas", x => x.id);
                    table.ForeignKey(
                        name: "fk_categorias_julgadas_bancas_requeridas_banca_requerida_id",
                        column: x => x.banca_requerida_id,
                        principalSchema: "selecao",
                        principalTable: "bancas_requeridas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_categorias_julgadas_codigo",
                schema: "selecao",
                table: "categorias_julgadas",
                columns: new[] { "banca_requerida_id", "codigo" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "categorias_julgadas",
                schema: "selecao");
        }
    }
}
