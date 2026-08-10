using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TiposProcessoConfiguraveis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tipos_processo",
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
                    table.PrimaryKey("pk_tipos_processo", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_tipos_processo_ativo",
                schema: "configuracao",
                table: "tipos_processo",
                column: "ativo");

            migrationBuilder.CreateIndex(
                name: "ix_tipos_processo_codigo",
                schema: "configuracao",
                table: "tipos_processo",
                column: "codigo",
                unique: true);

            // Carga inicial equivalente ao vocabulário que antes era enum no
            // módulo Seleção. Os IDs UUIDv7 são determinísticos, ancorados em
            // 2026-08-10T00:00:00Z, para permitir o backfill interschema sem FK;
            // novos códigos são exclusivamente administrativos.
            migrationBuilder.InsertData(
                schema: "configuracao",
                table: "tipos_processo",
                columns: new[] { "id", "codigo", "nome", "descricao", "ativo", "created_at", "created_by", "updated_at", "updated_by" },
                values: new object[,]
                {
                    { new Guid("019fe8f8-1400-7000-8000-000000000001"), "SiSU", "SiSU", null, true, new DateTimeOffset(new DateTime(2026, 8, 10, 0, 0, 0, DateTimeKind.Utc)), null, null, null },
                    { new Guid("019fe8f8-1400-7000-8000-000000000002"), "PSIQ", "Processo Seletivo Indígena e Quilombola", null, true, new DateTimeOffset(new DateTime(2026, 8, 10, 0, 0, 0, DateTimeKind.Utc)), null, null, null },
                    { new Guid("019fe8f8-1400-7000-8000-000000000003"), "PSECampo", "Processo Seletivo Especial do Campo", null, true, new DateTimeOffset(new DateTime(2026, 8, 10, 0, 0, 0, DateTimeKind.Utc)), null, null, null },
                    { new Guid("019fe8f8-1400-7000-8000-000000000004"), "PSVR", "Processo Seletivo Via Reserva", null, true, new DateTimeOffset(new DateTime(2026, 8, 10, 0, 0, 0, DateTimeKind.Utc)), null, null, null },
                    { new Guid("019fe8f8-1400-7000-8000-000000000005"), "TransferenciaInterna", "Transferência Interna", null, true, new DateTimeOffset(new DateTime(2026, 8, 10, 0, 0, 0, DateTimeKind.Utc)), null, null, null },
                    { new Guid("019fe8f8-1400-7000-8000-000000000006"), "TransferenciaExterna", "Transferência Externa", null, true, new DateTimeOffset(new DateTime(2026, 8, 10, 0, 0, 0, DateTimeKind.Utc)), null, null, null },
                    { new Guid("019fe8f8-1400-7000-8000-000000000007"), "PortadorDiploma", "Portador de Diploma", null, true, new DateTimeOffset(new DateTime(2026, 8, 10, 0, 0, 0, DateTimeKind.Utc)), null, null, null },
                    { new Guid("019fe8f8-1400-7000-8000-000000000008"), "Reopcao", "Reopção", null, true, new DateTimeOffset(new DateTime(2026, 8, 10, 0, 0, 0, DateTimeKind.Utc)), null, null, null },
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tipos_processo",
                schema: "configuracao");
        }
    }
}
