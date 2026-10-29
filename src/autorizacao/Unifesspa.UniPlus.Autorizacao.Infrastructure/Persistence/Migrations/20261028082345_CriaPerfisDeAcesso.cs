using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CriaPerfisDeAcesso : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "autorizacao");

            migrationBuilder.CreateTable(
                name: "idempotency_cache",
                schema: "autorizacao",
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

            migrationBuilder.CreateTable(
                name: "perfis",
                schema: "autorizacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    descricao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    governanca = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    nome_normalizado = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true, computedColumnSql: "lower(nome)", stored: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_perfis", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "perfil_permissoes",
                schema: "autorizacao",
                columns: table => new
                {
                    codigo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    perfil_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_perfil_permissoes", x => new { x.perfil_id, x.codigo });
                    table.ForeignKey(
                        name: "fk_perfil_permissoes_perfis_perfil_id",
                        column: x => x.perfil_id,
                        principalSchema: "autorizacao",
                        principalTable: "perfis",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                schema: "autorizacao",
                table: "perfis",
                columns: new[] { "id", "ativo", "created_at", "descricao", "governanca", "nome", "updated_at" },
                values: new object[,]
                {
                    { new Guid("019a0000-0000-7000-8000-000000000001"), true, new DateTimeOffset(new DateTime(2026, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Opera as concessões de acesso e mantém os perfis de acesso.", "gestor-de-acessos", "Gestor de acessos", null },
                    { new Guid("019a0000-0000-7000-8000-000000000002"), true, new DateTimeOffset(new DateTime(2026, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Aprova a concessão de acesso a dado pessoal ou sensível.", "encarregada-de-dados", "Encarregada de dados", null }
                });

            migrationBuilder.InsertData(
                schema: "autorizacao",
                table: "perfil_permissoes",
                columns: new[] { "codigo", "perfil_id" },
                values: new object[,]
                {
                    { "autorizacao:perfis:manter", new Guid("019a0000-0000-7000-8000-000000000001") },
                    { "autorizacao:permissoes:consultar-catalogo", new Guid("019a0000-0000-7000-8000-000000000001") },
                    { "autorizacao:permissoes:consultar-catalogo", new Guid("019a0000-0000-7000-8000-000000000002") }
                });

            migrationBuilder.CreateIndex(
                name: "idx_idempotency_expires_at",
                schema: "autorizacao",
                table: "idempotency_cache",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "idx_idempotency_lookup",
                schema: "autorizacao",
                table: "idempotency_cache",
                columns: new[] { "scope", "endpoint", "idempotency_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_perfis_governanca",
                schema: "autorizacao",
                table: "perfis",
                column: "governanca",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_perfis_nome",
                schema: "autorizacao",
                table: "perfis",
                column: "nome_normalizado",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "idempotency_cache",
                schema: "autorizacao");

            migrationBuilder.DropTable(
                name: "perfil_permissoes",
                schema: "autorizacao");

            migrationBuilder.DropTable(
                name: "perfis",
                schema: "autorizacao");
        }
    }
}
