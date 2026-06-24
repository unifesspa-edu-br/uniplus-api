using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaPesoAreaEnem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "peso_area_enem",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    resolucao = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    grupo_curso = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    peso_redacao = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: false),
                    peso_ciencias_natureza = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: false),
                    peso_ciencias_humanas = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: false),
                    peso_linguagens = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: false),
                    peso_matematica = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: false),
                    corte_redacao = table.Column<decimal>(type: "numeric(6,3)", precision: 6, scale: 3, nullable: false, defaultValue: 400m),
                    base_legal = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
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
                    table.PrimaryKey("pk_peso_area_enem", x => x.id);
                    table.CheckConstraint("ck_peso_area_enem_corte_redacao", "corte_redacao >= 0");
                    table.CheckConstraint("ck_peso_area_enem_grupo_curso", "grupo_curso IN ('Tecnológica', 'Humanística I', 'Humanística II', 'Saúde e Biológicas')");
                    table.CheckConstraint("ck_peso_area_enem_peso_ciencias_humanas", "peso_ciencias_humanas >= 0");
                    table.CheckConstraint("ck_peso_area_enem_peso_ciencias_natureza", "peso_ciencias_natureza >= 0");
                    table.CheckConstraint("ck_peso_area_enem_peso_linguagens", "peso_linguagens >= 0");
                    table.CheckConstraint("ck_peso_area_enem_peso_matematica", "peso_matematica >= 0");
                    table.CheckConstraint("ck_peso_area_enem_peso_redacao", "peso_redacao >= 0");
                });

            migrationBuilder.CreateIndex(
                name: "ix_peso_area_enem_resolucao_grupo_vivo",
                table: "peso_area_enem",
                columns: new[] { "resolucao", "grupo_curso" },
                unique: true,
                filter: "is_deleted = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "peso_area_enem");
        }
    }
}
