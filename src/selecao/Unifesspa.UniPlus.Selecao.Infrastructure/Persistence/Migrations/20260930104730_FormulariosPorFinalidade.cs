using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FormulariosPorFinalidade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Itens e termos pertencem a um formulário por finalidade, e não há formulário a que
            // ligar os existentes; sem produção, eles saem em vez de migrar.
            migrationBuilder.Sql("DELETE FROM selecao.termos_exigidos_formulario;");
            migrationBuilder.Sql("DELETE FROM selecao.fatos_coletados;");

            migrationBuilder.DropIndex(
                name: "ux_termos_exigidos_formulario_processo_codigo",
                schema: "selecao",
                table: "termos_exigidos_formulario");

            migrationBuilder.DropIndex(
                name: "ux_termos_exigidos_formulario_processo_ordem",
                schema: "selecao",
                table: "termos_exigidos_formulario");

            migrationBuilder.DropIndex(
                name: "ux_fatos_coletados_processo_ordem",
                schema: "selecao",
                table: "fatos_coletados");

            migrationBuilder.DropColumn(
                name: "formulario_titulo",
                schema: "selecao",
                table: "processos_seletivos");

            migrationBuilder.AddColumn<int>(
                name: "finalidade",
                schema: "selecao",
                table: "termos_exigidos_formulario",
                type: "integer",
                nullable: false);

            migrationBuilder.AddColumn<string>(
                name: "etapa_codigo",
                schema: "selecao",
                table: "fatos_coletados",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "finalidade",
                schema: "selecao",
                table: "fatos_coletados",
                type: "integer",
                nullable: false);

            migrationBuilder.CreateTable(
                name: "formularios_processo",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    processo_seletivo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    finalidade = table.Column<int>(type: "integer", nullable: false),
                    fase_id = table.Column<Guid>(type: "uuid", nullable: true),
                    titulo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    modelo_origem_id = table.Column<Guid>(type: "uuid", nullable: true),
                    modelo_origem_codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_formularios_processo", x => x.id);
                    table.ForeignKey(
                        name: "fk_formularios_processo_processos_seletivos_processo_seletivo_",
                        column: x => x.processo_seletivo_id,
                        principalSchema: "selecao",
                        principalTable: "processos_seletivos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "etapas_formulario",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    formulario_processo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    ordem = table.Column<int>(type: "integer", nullable: false),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    bloco = table.Column<int>(type: "integer", nullable: false),
                    titulo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    descricao = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    aviso = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_etapas_formulario", x => x.id);
                    table.ForeignKey(
                        name: "fk_etapas_formulario_formulario_processo_formulario_processo_id",
                        column: x => x.formulario_processo_id,
                        principalSchema: "selecao",
                        principalTable: "formularios_processo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_termos_exigidos_formulario_processo_finalidade_codigo",
                schema: "selecao",
                table: "termos_exigidos_formulario",
                columns: new[] { "processo_seletivo_id", "finalidade", "codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_termos_exigidos_formulario_processo_finalidade_ordem",
                schema: "selecao",
                table: "termos_exigidos_formulario",
                columns: new[] { "processo_seletivo_id", "finalidade", "ordem" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_fatos_coletados_processo_finalidade_ordem",
                schema: "selecao",
                table: "fatos_coletados",
                columns: new[] { "processo_seletivo_id", "finalidade", "ordem" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_etapas_formulario_formulario_codigo",
                schema: "selecao",
                table: "etapas_formulario",
                columns: new[] { "formulario_processo_id", "codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_etapas_formulario_formulario_ordem",
                schema: "selecao",
                table: "etapas_formulario",
                columns: new[] { "formulario_processo_id", "ordem" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_formularios_processo_processo_finalidade",
                schema: "selecao",
                table: "formularios_processo",
                columns: new[] { "processo_seletivo_id", "finalidade" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Sem a finalidade, itens e termos de formulários diferentes colidiriam nos índices
            // do processo inteiro; como no Up, saem em vez de migrar.
            migrationBuilder.Sql("DELETE FROM selecao.termos_exigidos_formulario;");
            migrationBuilder.Sql("DELETE FROM selecao.fatos_coletados;");

            migrationBuilder.DropTable(
                name: "etapas_formulario",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "formularios_processo",
                schema: "selecao");

            migrationBuilder.DropIndex(
                name: "ux_termos_exigidos_formulario_processo_finalidade_codigo",
                schema: "selecao",
                table: "termos_exigidos_formulario");

            migrationBuilder.DropIndex(
                name: "ux_termos_exigidos_formulario_processo_finalidade_ordem",
                schema: "selecao",
                table: "termos_exigidos_formulario");

            migrationBuilder.DropIndex(
                name: "ux_fatos_coletados_processo_finalidade_ordem",
                schema: "selecao",
                table: "fatos_coletados");

            migrationBuilder.DropColumn(
                name: "finalidade",
                schema: "selecao",
                table: "termos_exigidos_formulario");

            migrationBuilder.DropColumn(
                name: "etapa_codigo",
                schema: "selecao",
                table: "fatos_coletados");

            migrationBuilder.DropColumn(
                name: "finalidade",
                schema: "selecao",
                table: "fatos_coletados");

            migrationBuilder.AddColumn<string>(
                name: "formulario_titulo",
                schema: "selecao",
                table: "processos_seletivos",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true,
                comment: "Título do formulário de inscrição apresentado ao candidato. Ausência = sem título configurado.");

            migrationBuilder.CreateIndex(
                name: "ux_termos_exigidos_formulario_processo_codigo",
                schema: "selecao",
                table: "termos_exigidos_formulario",
                columns: new[] { "processo_seletivo_id", "codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_termos_exigidos_formulario_processo_ordem",
                schema: "selecao",
                table: "termos_exigidos_formulario",
                columns: new[] { "processo_seletivo_id", "ordem" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_fatos_coletados_processo_ordem",
                schema: "selecao",
                table: "fatos_coletados",
                columns: new[] { "processo_seletivo_id", "ordem" },
                unique: true);
        }
    }
}
