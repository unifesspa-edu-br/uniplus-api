using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaRascunhoDePublicacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "rascunhos_publicacao",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    processo_seletivo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_sub = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    conteudo = table.Column<string>(type: "text", nullable: false),
                    versao = table.Column<int>(type: "integer", nullable: false),
                    expira_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rascunhos_publicacao", x => x.id);
                    table.ForeignKey(
                        name: "fk_rascunhos_publicacao_processos_seletivos_processo_seletivo_",
                        column: x => x.processo_seletivo_id,
                        principalSchema: "selecao",
                        principalTable: "processos_seletivos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_rascunhos_publicacao_processo_operador",
                schema: "selecao",
                table: "rascunhos_publicacao",
                columns: new[] { "processo_seletivo_id", "usuario_sub" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "rascunhos_publicacao",
                schema: "selecao");
        }
    }
}
