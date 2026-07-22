using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGrafoDeColetaDeFatos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "fatos_coletados",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    processo_seletivo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fato_codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    ordem = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_fatos_coletados", x => x.id);
                    table.ForeignKey(
                        name: "fk_fatos_coletados_processos_seletivos_processo_seletivo_id",
                        column: x => x.processo_seletivo_id,
                        principalSchema: "selecao",
                        principalTable: "processos_seletivos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "condicoes_precondicao_fato",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    fato_coletado_id = table.Column<Guid>(type: "uuid", nullable: false),
                    clausula = table.Column<int>(type: "integer", nullable: false),
                    fato = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    operador = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    valor = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_condicoes_precondicao_fato", x => x.id);
                    table.ForeignKey(
                        name: "fk_condicoes_precondicao_fato_fato_coletado_fato_coletado_id",
                        column: x => x.fato_coletado_id,
                        principalSchema: "selecao",
                        principalTable: "fatos_coletados",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_condicoes_precondicao_fato_fato_coletado_id",
                schema: "selecao",
                table: "condicoes_precondicao_fato",
                column: "fato_coletado_id");

            migrationBuilder.CreateIndex(
                name: "ux_fatos_coletados_processo_fato",
                schema: "selecao",
                table: "fatos_coletados",
                columns: new[] { "processo_seletivo_id", "fato_codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_fatos_coletados_processo_ordem",
                schema: "selecao",
                table: "fatos_coletados",
                columns: new[] { "processo_seletivo_id", "ordem" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "condicoes_precondicao_fato",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "fatos_coletados",
                schema: "selecao");
        }
    }
}
