using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentosExigidos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "documentos_exigidos",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    processo_seletivo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    exigido_na_fase_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo_documento_origem_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo_documento_codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    tipo_documento_nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    tipo_documento_categoria = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    aplicabilidade = table.Column<int>(type: "integer", nullable: false),
                    obrigatorio = table.Column<bool>(type: "boolean", nullable: false),
                    consequencia_indeferimento = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    grupo_satisfacao_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_documentos_exigidos", x => x.id);
                    table.ForeignKey(
                        name: "fk_documentos_exigidos_fases_cronograma_exigido_na_fase_id",
                        column: x => x.exigido_na_fase_id,
                        principalSchema: "selecao",
                        principalTable: "fases_cronograma",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_documentos_exigidos_processos_seletivos_processo_seletivo_id",
                        column: x => x.processo_seletivo_id,
                        principalSchema: "selecao",
                        principalTable: "processos_seletivos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_documentos_exigidos_exigido_na_fase_id",
                schema: "selecao",
                table: "documentos_exigidos",
                column: "exigido_na_fase_id");

            migrationBuilder.CreateIndex(
                name: "ix_documentos_exigidos_processo_seletivo_id",
                schema: "selecao",
                table: "documentos_exigidos",
                column: "processo_seletivo_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "documentos_exigidos",
                schema: "selecao");
        }
    }
}
