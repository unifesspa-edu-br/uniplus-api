using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CriaCadastroTiposEtapa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tipos_etapa",
                schema: "configuracao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    descricao = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    created_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tipos_etapa", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_tipos_etapa_ativo",
                schema: "configuracao",
                table: "tipos_etapa",
                column: "ativo");

            migrationBuilder.CreateIndex(
                name: "ix_tipos_etapa_codigo",
                schema: "configuracao",
                table: "tipos_etapa",
                column: "codigo",
                unique: true);

            // Carga inicial equivalente ao vocabulário que antes era enum órfão no módulo
            // Seleção (issue #1071). Os IDs UUIDv7 são determinísticos, ancorados em
            // 2026-08-11T00:00:00Z (um dia após o anchor de tipos_processo, para não colidir),
            // para permitir o backfill interschema sem FK; novos códigos são exclusivamente
            // administrativos.
            migrationBuilder.InsertData(
                schema: "configuracao",
                table: "tipos_etapa",
                columns: new[] { "id", "codigo", "nome", "descricao", "ativo", "created_at", "created_by", "updated_at", "updated_by" },
                values: new object[,]
                {
                    { new Guid("019fee1e-7000-7000-8000-000000000001"), "PROVA_OBJETIVA", "Prova Objetiva", null, true, new DateTimeOffset(new DateTime(2026, 8, 11, 0, 0, 0, DateTimeKind.Utc)), null, null, null },
                    { new Guid("019fee1e-7000-7000-8000-000000000002"), "REDACAO", "Redação", null, true, new DateTimeOffset(new DateTime(2026, 8, 11, 0, 0, 0, DateTimeKind.Utc)), null, null, null },
                    { new Guid("019fee1e-7000-7000-8000-000000000003"), "ENTREVISTA", "Entrevista", null, true, new DateTimeOffset(new DateTime(2026, 8, 11, 0, 0, 0, DateTimeKind.Utc)), null, null, null },
                    { new Guid("019fee1e-7000-7000-8000-000000000004"), "ANALISE_HISTORICO", "Análise de Histórico", null, true, new DateTimeOffset(new DateTime(2026, 8, 11, 0, 0, 0, DateTimeKind.Utc)), null, null, null },
                    { new Guid("019fee1e-7000-7000-8000-000000000005"), "BANCA_HETEROIDENTIFICACAO", "Banca de Heteroidentificação", null, true, new DateTimeOffset(new DateTime(2026, 8, 11, 0, 0, 0, DateTimeKind.Utc)), null, null, null },
                    { new Guid("019fee1e-7000-7000-8000-000000000006"), "ANALISE_DOCUMENTAL", "Análise Documental", null, true, new DateTimeOffset(new DateTime(2026, 8, 11, 0, 0, 0, DateTimeKind.Utc)), null, null, null },
                    { new Guid("019fee1e-7000-7000-8000-000000000007"), "NOTA_ENEM", "Nota do Enem", null, true, new DateTimeOffset(new DateTime(2026, 8, 11, 0, 0, 0, DateTimeKind.Utc)), null, null, null },
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tipos_etapa",
                schema: "configuracao");
        }
    }
}
