using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CriaTermoConsentimento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "termo_consentimento",
                schema: "configuracao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    texto_rascunho = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: true),
                    base_legal_rascunho = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    forma_aceite_rascunho = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    revisado = table.Column<bool>(type: "boolean", nullable: false),
                    revisado_por = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    revisado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("pk_termo_consentimento", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "termo_consentimento_versao",
                schema: "configuracao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    termo_consentimento_id = table.Column<Guid>(type: "uuid", nullable: false),
                    texto = table.Column<string>(type: "text", nullable: false),
                    base_legal = table.Column<string>(type: "text", nullable: false),
                    forma_aceite = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    promovida_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    promovida_por = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_termo_consentimento_versao", x => x.id);
                    table.ForeignKey(
                        name: "fk_termo_consentimento_versao_termo_consentimento_termo_consen",
                        column: x => x.termo_consentimento_id,
                        principalSchema: "configuracao",
                        principalTable: "termo_consentimento",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "ix_termo_consentimento_nome",
                schema: "configuracao",
                table: "termo_consentimento",
                column: "nome",
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_termo_consentimento_versao_termo_promovida_em",
                schema: "configuracao",
                table: "termo_consentimento_versao",
                columns: new[] { "termo_consentimento_id", "promovida_em" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "termo_consentimento_versao",
                schema: "configuracao");

            migrationBuilder.DropTable(
                name: "termo_consentimento",
                schema: "configuracao");
        }
    }
}
