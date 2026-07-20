using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddArvoreSatisfacaoExigencias : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "nos_exigencia",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    processo_seletivo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    no_pai_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ordem = table.Column<int>(type: "integer", nullable: false),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    documento_exigido_id = table.Column<Guid>(type: "uuid", nullable: true),
                    quantidade_minima = table.Column<int>(type: "integer", nullable: true),
                    consequencia = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_nos_exigencia", x => x.id);
                    table.CheckConstraint("ck_nos_exigencia_id_diferente_de_no_pai_id", "id <> no_pai_id");
                    table.CheckConstraint("ck_nos_exigencia_ordem_nao_negativa", "ordem >= 0");
                    table.CheckConstraint("ck_nos_exigencia_quantidade_minima_positiva", "quantidade_minima IS NULL OR quantidade_minima >= 1");
                    table.CheckConstraint("ck_nos_exigencia_tipo_campos_coerentes", "(tipo = 1 AND documento_exigido_id IS NOT NULL AND quantidade_minima IS NULL AND consequencia IS NULL) OR (tipo = 2 AND documento_exigido_id IS NULL AND quantidade_minima IS NULL AND consequencia IS NULL) OR (tipo = 3 AND documento_exigido_id IS NULL AND quantidade_minima IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_nos_exigencia_documentos_exigidos_documento_exigido_id",
                        column: x => x.documento_exigido_id,
                        principalSchema: "selecao",
                        principalTable: "documentos_exigidos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_nos_exigencia_nos_exigencia_no_pai_id",
                        column: x => x.no_pai_id,
                        principalSchema: "selecao",
                        principalTable: "nos_exigencia",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_nos_exigencia_processos_seletivos_processo_seletivo_id",
                        column: x => x.processo_seletivo_id,
                        principalSchema: "selecao",
                        principalTable: "processos_seletivos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "nos_exigencia_base_legal",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    no_exigencia_id = table.Column<Guid>(type: "uuid", nullable: false),
                    referencia = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    abrangencia = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    observacao = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_nos_exigencia_base_legal", x => x.id);
                    table.ForeignKey(
                        name: "fk_nos_exigencia_base_legal_no_exigencia_no_exigencia_id",
                        column: x => x.no_exigencia_id,
                        principalSchema: "selecao",
                        principalTable: "nos_exigencia",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_nos_exigencia_documento_exigido_id",
                schema: "selecao",
                table: "nos_exigencia",
                column: "documento_exigido_id",
                unique: true,
                filter: "documento_exigido_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_nos_exigencia_irmaos_ordem",
                schema: "selecao",
                table: "nos_exigencia",
                columns: new[] { "no_pai_id", "ordem" },
                unique: true,
                filter: "no_pai_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_nos_exigencia_raiz_ordem",
                schema: "selecao",
                table: "nos_exigencia",
                columns: new[] { "processo_seletivo_id", "ordem" },
                unique: true,
                filter: "no_pai_id IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_nos_exigencia_base_legal_no_exigencia_id",
                schema: "selecao",
                table: "nos_exigencia_base_legal",
                column: "no_exigencia_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "nos_exigencia_base_legal",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "nos_exigencia",
                schema: "selecao");
        }
    }
}
