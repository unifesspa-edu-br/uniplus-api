using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTipoDocumento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tipo_documento",
                schema: "configuracao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    descricao = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    categoria = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    formatos_aceitos = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    tamanho_maximo_mb = table.Column<int>(type: "integer", nullable: true),
                    tipo_equivalente = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
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
                    table.PrimaryKey("pk_tipo_documento", x => x.id);
                    table.CheckConstraint("ck_tipo_documento_categoria", "categoria IN ('IDENTIFICACAO', 'ESCOLARIDADE', 'RENDA', 'RACA_ETNIA', 'SAUDE', 'RESIDENCIA', 'OUTROS')");
                    table.CheckConstraint("ck_tipo_documento_equivalente_diferente_codigo", "tipo_equivalente IS NULL OR tipo_equivalente <> codigo");
                    table.CheckConstraint("ck_tipo_documento_tamanho_maximo_mb_positivo", "tamanho_maximo_mb IS NULL OR tamanho_maximo_mb > 0");
                });

            migrationBuilder.CreateIndex(
                name: "ix_tipo_documento_categoria",
                schema: "configuracao",
                table: "tipo_documento",
                column: "categoria");

            migrationBuilder.CreateIndex(
                name: "ix_tipo_documento_codigo_vivo",
                schema: "configuracao",
                table: "tipo_documento",
                column: "codigo",
                unique: true,
                filter: "is_deleted = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tipo_documento",
                schema: "configuracao");
        }
    }
}
