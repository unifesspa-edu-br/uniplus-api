using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRascunhoRetificacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "rascunhos_retificacao",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    processo_seletivo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    motivo = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    versao_base_id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero_versao_base = table.Column<int>(type: "integer", nullable: false),
                    aberto_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    aberto_por_sub = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    revisao = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rascunhos_retificacao", x => x.id);
                    table.ForeignKey(
                        name: "fk_rascunhos_retificacao_processos_seletivos_processo_seletivo",
                        column: x => x.processo_seletivo_id,
                        principalSchema: "selecao",
                        principalTable: "processos_seletivos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_rascunhos_retificacao_processo",
                schema: "selecao",
                table: "rascunhos_retificacao",
                column: "processo_seletivo_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "rascunhos_retificacao",
                schema: "selecao");
        }
    }
}
