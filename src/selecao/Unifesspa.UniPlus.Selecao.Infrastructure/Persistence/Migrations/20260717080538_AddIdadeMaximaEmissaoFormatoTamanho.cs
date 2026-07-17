using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIdadeMaximaEmissaoFormatoTamanho : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "formato_permitido",
                schema: "selecao",
                table: "documentos_exigidos",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "idade_maxima_referencia_data",
                schema: "selecao",
                table: "documentos_exigidos",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "idade_maxima_referencia_fase_id",
                schema: "selecao",
                table: "documentos_exigidos",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "idade_maxima_referencia_tipo",
                schema: "selecao",
                table: "documentos_exigidos",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "idade_maxima_unidade",
                schema: "selecao",
                table: "documentos_exigidos",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "idade_maxima_valor",
                schema: "selecao",
                table: "documentos_exigidos",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "tamanho_maximo_bytes",
                schema: "selecao",
                table: "documentos_exigidos",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "formato_permitido",
                schema: "selecao",
                table: "documentos_exigidos");

            migrationBuilder.DropColumn(
                name: "idade_maxima_referencia_data",
                schema: "selecao",
                table: "documentos_exigidos");

            migrationBuilder.DropColumn(
                name: "idade_maxima_referencia_fase_id",
                schema: "selecao",
                table: "documentos_exigidos");

            migrationBuilder.DropColumn(
                name: "idade_maxima_referencia_tipo",
                schema: "selecao",
                table: "documentos_exigidos");

            migrationBuilder.DropColumn(
                name: "idade_maxima_unidade",
                schema: "selecao",
                table: "documentos_exigidos");

            migrationBuilder.DropColumn(
                name: "idade_maxima_valor",
                schema: "selecao",
                table: "documentos_exigidos");

            migrationBuilder.DropColumn(
                name: "tamanho_maximo_bytes",
                schema: "selecao",
                table: "documentos_exigidos");
        }
    }
}
