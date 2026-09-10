using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaBaseLegalBonusRegional : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "base_legal_bonus_regional",
                schema: "configuracao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo_instrumento = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    identificacao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    descricao = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
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
                    table.PrimaryKey("pk_base_legal_bonus_regional", x => x.id);
                    table.CheckConstraint("CK_base_legal_bonus_regional_tipo_instrumento", "tipo_instrumento IN ('LEI','DECRETO','PORTARIA','RESOLUCAO','INSTRUCAO_NORMATIVA','PARECER')");
                });

            migrationBuilder.CreateTable(
                name: "base_legal_bonus_regional_municipio",
                schema: "configuracao",
                columns: table => new
                {
                    base_legal_bonus_regional_id = table.Column<Guid>(type: "uuid", nullable: false),
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    codigo_ibge = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    nome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    uf = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_base_legal_bonus_regional_municipio", x => new { x.base_legal_bonus_regional_id, x.id });
                    table.CheckConstraint("CK_base_legal_bonus_regional_municipio_codigo_ibge", "codigo_ibge ~ '^[0-9]{7}$'");
                    table.ForeignKey(
                        name: "fk_base_legal_bonus_regional_municipio_base_legal_bonus_region",
                        column: x => x.base_legal_bonus_regional_id,
                        principalSchema: "configuracao",
                        principalTable: "base_legal_bonus_regional",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_base_legal_bonus_regional_tipo_instrumento",
                schema: "configuracao",
                table: "base_legal_bonus_regional",
                column: "tipo_instrumento");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "base_legal_bonus_regional_municipio",
                schema: "configuracao");

            migrationBuilder.DropTable(
                name: "base_legal_bonus_regional",
                schema: "configuracao");
        }
    }
}
