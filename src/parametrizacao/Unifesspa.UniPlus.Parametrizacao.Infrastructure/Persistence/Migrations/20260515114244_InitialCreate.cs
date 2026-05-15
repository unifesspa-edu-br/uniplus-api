using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Parametrizacao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // btree_gist é pré-requisito das junction tables com EXCLUDE USING GIST
            // de AreasDeInteresse (ADR-0060). A primeira entidade área-scoped do
            // Parametrizacao entra em F2; o CREATE EXTENSION IF NOT EXISTS é
            // idempotente — o banco uniplus_parametrizacao já foi provisionado
            // com a extensão na #446, mas a chamada garante portabilidade quando
            // alguém recria o banco do zero (Testcontainers em F2, dev local).
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS btree_gist;");

            migrationBuilder.CreateTable(
                name: "idempotency_cache",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    scope = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    endpoint = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    idempotency_key = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    body_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    status = table.Column<short>(type: "smallint", nullable: false),
                    response_status = table.Column<int>(type: "integer", nullable: true),
                    response_headers_json = table.Column<string>(type: "jsonb", nullable: true),
                    response_body_cipher = table.Column<byte[]>(type: "bytea", nullable: true),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_idempotency_cache", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "idx_idempotency_expires_at",
                table: "idempotency_cache",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "idx_idempotency_lookup",
                table: "idempotency_cache",
                columns: new[] { "scope", "endpoint", "idempotency_key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "idempotency_cache");
        }
    }
}
