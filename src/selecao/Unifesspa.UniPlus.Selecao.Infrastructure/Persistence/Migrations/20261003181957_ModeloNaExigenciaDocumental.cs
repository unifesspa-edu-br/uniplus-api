using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ModeloNaExigenciaDocumental : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "modelo_formato",
                schema: "selecao",
                table: "documentos_exigidos",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "modelo_hash_sha256",
                schema: "selecao",
                table: "documentos_exigidos",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "modelo_id",
                schema: "selecao",
                table: "documentos_exigidos",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "modelo_nome_arquivo",
                schema: "selecao",
                table: "documentos_exigidos",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "modelo_formato",
                schema: "selecao",
                table: "documentos_exigidos");

            migrationBuilder.DropColumn(
                name: "modelo_hash_sha256",
                schema: "selecao",
                table: "documentos_exigidos");

            migrationBuilder.DropColumn(
                name: "modelo_id",
                schema: "selecao",
                table: "documentos_exigidos");

            migrationBuilder.DropColumn(
                name: "modelo_nome_arquivo",
                schema: "selecao",
                table: "documentos_exigidos");
        }
    }
}
