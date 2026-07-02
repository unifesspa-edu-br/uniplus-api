using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCurso : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "curso",
                schema: "configuracao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    grau = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    nivel_ensino = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    grupo_area_enem = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
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
                    table.PrimaryKey("pk_curso", x => x.id);
                    table.CheckConstraint("ck_curso_grupo_area_enem", "grupo_area_enem IS NULL OR grupo_area_enem IN ('Tecnológica', 'Humanística I', 'Humanística II', 'Saúde e Biológicas')");
                });

            migrationBuilder.CreateIndex(
                name: "ix_curso_codigo_vivo",
                schema: "configuracao",
                table: "curso",
                column: "codigo",
                unique: true,
                filter: "is_deleted = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "curso",
                schema: "configuracao");
        }
    }
}
