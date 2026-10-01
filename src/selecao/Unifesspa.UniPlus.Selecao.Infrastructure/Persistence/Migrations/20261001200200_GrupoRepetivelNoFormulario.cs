using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GrupoRepetivelNoFormulario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_fatos_coletados_processo_finalidade_ordem",
                schema: "selecao",
                table: "fatos_coletados");

            migrationBuilder.AddColumn<Guid>(
                name: "grupo_coletado_id",
                schema: "selecao",
                table: "fatos_coletados",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "grupos_coletados",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    processo_seletivo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    finalidade = table.Column<int>(type: "integer", nullable: false),
                    codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    ordem = table.Column<int>(type: "integer", nullable: false),
                    etapa_codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    rotulo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    minimo = table.Column<int>(type: "integer", nullable: false),
                    maximo = table.Column<int>(type: "integer", nullable: false),
                    exibicao = table.Column<string>(type: "jsonb", nullable: true),
                    obrigatoriedade = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_grupos_coletados", x => x.id);
                    table.ForeignKey(
                        name: "fk_grupos_coletados_processos_seletivos_processo_seletivo_id",
                        column: x => x.processo_seletivo_id,
                        principalSchema: "selecao",
                        principalTable: "processos_seletivos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_fatos_coletados_grupo_ordem",
                schema: "selecao",
                table: "fatos_coletados",
                columns: new[] { "grupo_coletado_id", "ordem" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_fatos_coletados_processo_finalidade_ordem",
                schema: "selecao",
                table: "fatos_coletados",
                columns: new[] { "processo_seletivo_id", "finalidade", "ordem" },
                unique: true,
                filter: "grupo_coletado_id IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_grupos_coletados_processo_codigo",
                schema: "selecao",
                table: "grupos_coletados",
                columns: new[] { "processo_seletivo_id", "codigo" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_fatos_coletados_grupo_coletado_grupo_coletado_id",
                schema: "selecao",
                table: "fatos_coletados",
                column: "grupo_coletado_id",
                principalSchema: "selecao",
                principalTable: "grupos_coletados",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_fatos_coletados_grupo_coletado_grupo_coletado_id",
                schema: "selecao",
                table: "fatos_coletados");

            migrationBuilder.DropTable(
                name: "grupos_coletados",
                schema: "selecao");

            migrationBuilder.DropIndex(
                name: "ux_fatos_coletados_grupo_ordem",
                schema: "selecao",
                table: "fatos_coletados");

            migrationBuilder.DropIndex(
                name: "ux_fatos_coletados_processo_finalidade_ordem",
                schema: "selecao",
                table: "fatos_coletados");

            migrationBuilder.DropColumn(
                name: "grupo_coletado_id",
                schema: "selecao",
                table: "fatos_coletados");

            migrationBuilder.CreateIndex(
                name: "ux_fatos_coletados_processo_finalidade_ordem",
                schema: "selecao",
                table: "fatos_coletados",
                columns: new[] { "processo_seletivo_id", "finalidade", "ordem" },
                unique: true);
        }
    }
}
