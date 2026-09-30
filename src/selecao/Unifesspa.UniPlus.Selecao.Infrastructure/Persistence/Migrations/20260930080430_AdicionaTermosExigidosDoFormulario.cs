using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaTermosExigidosDoFormulario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "formulario_termo_aceite_texto",
                schema: "selecao",
                table: "processos_seletivos");

            migrationBuilder.CreateTable(
                name: "termos_exigidos_formulario",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    processo_seletivo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    ordem = table.Column<int>(type: "integer", nullable: false),
                    termo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    versao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    texto = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: false),
                    base_legal = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    forma_aceite = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    hash_versao = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    exibicao = table.Column<string>(type: "jsonb", nullable: true),
                    obrigatoriedade = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_termos_exigidos_formulario", x => x.id);
                    table.ForeignKey(
                        name: "fk_termos_exigidos_formulario_processos_seletivos_processo_sel",
                        column: x => x.processo_seletivo_id,
                        principalSchema: "selecao",
                        principalTable: "processos_seletivos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "termos_exigidos_formulario",
                schema: "selecao");

            migrationBuilder.AddColumn<string>(
                name: "formulario_termo_aceite_texto",
                schema: "selecao",
                table: "processos_seletivos",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true,
                comment: "Texto do termo de aceite do formulário de inscrição. Ausência = sem termo configurado.");
        }
    }
}
