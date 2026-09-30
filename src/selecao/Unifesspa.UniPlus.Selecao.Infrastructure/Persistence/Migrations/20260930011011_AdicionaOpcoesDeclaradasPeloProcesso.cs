using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaOpcoesDeclaradasPeloProcesso : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "opcoes_do_processo",
                schema: "selecao",
                table: "fatos_coletados",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "opcoes_declaradas_fato",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    processo_seletivo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fato_codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    rotulo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ordem = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_opcoes_declaradas_fato", x => x.id);
                    table.ForeignKey(
                        name: "fk_opcoes_declaradas_fato_processos_seletivos_processo_seletiv",
                        column: x => x.processo_seletivo_id,
                        principalSchema: "selecao",
                        principalTable: "processos_seletivos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_opcoes_declaradas_fato_processo_fato_codigo",
                schema: "selecao",
                table: "opcoes_declaradas_fato",
                columns: new[] { "processo_seletivo_id", "fato_codigo", "codigo" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "opcoes_declaradas_fato",
                schema: "selecao");

            migrationBuilder.DropColumn(
                name: "opcoes_do_processo",
                schema: "selecao",
                table: "fatos_coletados");
        }
    }
}
