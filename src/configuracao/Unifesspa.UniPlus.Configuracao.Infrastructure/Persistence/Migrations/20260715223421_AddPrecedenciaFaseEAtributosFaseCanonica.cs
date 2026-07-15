using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPrecedenciaFaseEAtributosFaseCanonica : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "coleta_inscricao",
                schema: "configuracao",
                table: "fase_canonica",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "origem_data",
                schema: "configuracao",
                table: "fase_canonica",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "PROPRIA");

            migrationBuilder.AddColumn<bool>(
                name: "produz_resultado",
                schema: "configuracao",
                table: "fase_canonica",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "resultado_definitivo",
                schema: "configuracao",
                table: "fase_canonica",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "precedencia_fase",
                schema: "configuracao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    antecessora_codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    sucessora_codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    permite_sobreposicao = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_precedencia_fase", x => x.id);
                    table.CheckConstraint("ck_precedencia_fase_antecessora_canonica", "antecessora_codigo IN ('INSCRICAO', 'HOMOLOGACAO', 'ENSALAMENTO', 'AVALIACAO', 'CLASSIFICACAO', 'RESULTADO_PRELIMINAR', 'RECURSOS', 'RESULTADO_FINAL', 'HABILITACAO', 'HETEROIDENTIFICACAO', 'MATRICULA', 'HOMOLOGACAO_RESULTADO_FINAL', 'LISTA_ESPERA', 'CHAMADA')");
                    table.CheckConstraint("ck_precedencia_fase_antecessora_formato", "antecessora_codigo ~ '^[A-Z_]+$'");
                    table.CheckConstraint("ck_precedencia_fase_sem_self_loop", "antecessora_codigo <> sucessora_codigo");
                    table.CheckConstraint("ck_precedencia_fase_sucessora_canonica", "sucessora_codigo IN ('INSCRICAO', 'HOMOLOGACAO', 'ENSALAMENTO', 'AVALIACAO', 'CLASSIFICACAO', 'RESULTADO_PRELIMINAR', 'RECURSOS', 'RESULTADO_FINAL', 'HABILITACAO', 'HETEROIDENTIFICACAO', 'MATRICULA', 'HOMOLOGACAO_RESULTADO_FINAL', 'LISTA_ESPERA', 'CHAMADA')");
                    table.CheckConstraint("ck_precedencia_fase_sucessora_formato", "sucessora_codigo ~ '^[A-Z_]+$'");
                });

            migrationBuilder.InsertData(
                schema: "configuracao",
                table: "precedencia_fase",
                columns: new[] { "id", "antecessora_codigo", "created_at", "created_by", "deleted_at", "deleted_by", "is_deleted", "sucessora_codigo", "updated_at", "updated_by" },
                values: new object[,]
                {
                    { new Guid("93ec0000-0000-7000-8000-000000000001"), "INSCRICAO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, false, "HOMOLOGACAO", null, null },
                    { new Guid("93ec0000-0000-7000-8000-000000000002"), "RESULTADO_PRELIMINAR", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, false, "RECURSOS", null, null },
                    { new Guid("93ec0000-0000-7000-8000-000000000003"), "RECURSOS", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, false, "RESULTADO_FINAL", null, null },
                    { new Guid("93ec0000-0000-7000-8000-000000000004"), "RESULTADO_FINAL", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, false, "HABILITACAO", null, null },
                    { new Guid("93ec0000-0000-7000-8000-000000000005"), "HABILITACAO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, false, "MATRICULA", null, null },
                    { new Guid("93ec0000-0000-7000-8000-000000000006"), "HETEROIDENTIFICACAO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, false, "HOMOLOGACAO_RESULTADO_FINAL", null, null }
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_fase_canonica_origem_data",
                schema: "configuracao",
                table: "fase_canonica",
                sql: "origem_data IN ('PROPRIA', 'DELEGADA')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_fase_canonica_resultado_definitivo",
                schema: "configuracao",
                table: "fase_canonica",
                sql: "resultado_definitivo = false OR produz_resultado = true");

            migrationBuilder.CreateIndex(
                name: "ix_precedencia_fase_par_vivo",
                schema: "configuracao",
                table: "precedencia_fase",
                columns: new[] { "antecessora_codigo", "sucessora_codigo" },
                unique: true,
                filter: "is_deleted = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "precedencia_fase",
                schema: "configuracao");

            migrationBuilder.DropCheckConstraint(
                name: "ck_fase_canonica_origem_data",
                schema: "configuracao",
                table: "fase_canonica");

            migrationBuilder.DropCheckConstraint(
                name: "ck_fase_canonica_resultado_definitivo",
                schema: "configuracao",
                table: "fase_canonica");

            migrationBuilder.DropColumn(
                name: "coleta_inscricao",
                schema: "configuracao",
                table: "fase_canonica");

            migrationBuilder.DropColumn(
                name: "origem_data",
                schema: "configuracao",
                table: "fase_canonica");

            migrationBuilder.DropColumn(
                name: "produz_resultado",
                schema: "configuracao",
                table: "fase_canonica");

            migrationBuilder.DropColumn(
                name: "resultado_definitivo",
                schema: "configuracao",
                table: "fase_canonica");
        }
    }
}
