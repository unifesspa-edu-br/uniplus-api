using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEditalSnapshotPublicacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "editais",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    processo_seletivo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    natureza = table.Column<int>(type: "integer", nullable: false),
                    numero = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    data_publicacao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    documento_edital_id = table.Column<Guid>(type: "uuid", nullable: false),
                    edital_retificado_id = table.Column<Guid>(type: "uuid", nullable: true),
                    motivo_retificacao = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_editais", x => x.id);
                    table.CheckConstraint("ck_editais_contrato_natureza", "(natureza = 1 AND edital_retificado_id IS NULL AND motivo_retificacao IS NULL) OR (natureza = 2 AND edital_retificado_id IS NOT NULL AND motivo_retificacao IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_editais_documento_edital_id",
                        column: x => x.documento_edital_id,
                        principalSchema: "selecao",
                        principalTable: "documentos_edital",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_editais_edital_retificado_id",
                        column: x => x.edital_retificado_id,
                        principalSchema: "selecao",
                        principalTable: "editais",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_editais_processos_seletivos_processo_seletivo_id",
                        column: x => x.processo_seletivo_id,
                        principalSchema: "selecao",
                        principalTable: "processos_seletivos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "snapshot_publicacao",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    edital_id = table.Column<Guid>(type: "uuid", nullable: false),
                    schema_version = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    algoritmo_hash = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    configuracao_congelada_canonica = table.Column<byte[]>(type: "bytea", nullable: false),
                    configuracao_congelada = table.Column<string>(type: "jsonb", nullable: false),
                    hash_configuracao = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    hash_edital = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ator_usuario_sub = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    data_publicacao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_snapshot_publicacao", x => x.id);
                    table.ForeignKey(
                        name: "fk_snapshot_publicacao_edital_id",
                        column: x => x.edital_id,
                        principalSchema: "selecao",
                        principalTable: "editais",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_editais_documento_edital_id",
                schema: "selecao",
                table: "editais",
                column: "documento_edital_id");

            migrationBuilder.CreateIndex(
                name: "ix_editais_edital_retificado_id",
                schema: "selecao",
                table: "editais",
                column: "edital_retificado_id");

            migrationBuilder.CreateIndex(
                name: "ux_editais_processo_abertura_unica",
                schema: "selecao",
                table: "editais",
                column: "processo_seletivo_id",
                unique: true,
                filter: "natureza = 1");

            migrationBuilder.CreateIndex(
                name: "ux_editais_processo_data_publicacao",
                schema: "selecao",
                table: "editais",
                columns: new[] { "processo_seletivo_id", "data_publicacao" },
                unique: true,
                filter: "data_publicacao IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_snapshot_publicacao_edital_id",
                schema: "selecao",
                table: "snapshot_publicacao",
                column: "edital_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "snapshot_publicacao",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "editais",
                schema: "selecao");
        }
    }
}
