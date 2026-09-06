using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaOrdenacaoAlfabeticaCurso : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_oferta_curso_curso_id",
                schema: "configuracao",
                table: "oferta_curso");

            migrationBuilder.AddColumn<string>(
                name: "nome_ordenacao",
                schema: "configuracao",
                table: "curso",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                computedColumnSql: "lower(translate(normalize(nome, NFC), 'ÁÀÂÃÄÅÉÈÊËÍÌÎÏÓÒÔÕÖÚÙÛÜÇÑÝáàâãäåéèêëíìîïóòôõöúùûüçñý', 'AAAAAAEEEEIIIIOOOOOUUUUCNYaaaaaaeeeeiiiiooooouuuucny') COLLATE \"C\")",
                stored: true,
                collation: "C");

            migrationBuilder.CreateIndex(
                name: "ix_oferta_curso_curso_id",
                schema: "configuracao",
                table: "oferta_curso",
                columns: new[] { "curso_id", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_curso_ordenacao_alfabetica",
                schema: "configuracao",
                table: "curso",
                columns: new[] { "nome_ordenacao", "codigo", "id" },
                filter: "is_deleted = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_oferta_curso_curso_id",
                schema: "configuracao",
                table: "oferta_curso");

            migrationBuilder.DropIndex(
                name: "ix_curso_ordenacao_alfabetica",
                schema: "configuracao",
                table: "curso");

            migrationBuilder.DropColumn(
                name: "nome_ordenacao",
                schema: "configuracao",
                table: "curso");

            migrationBuilder.CreateIndex(
                name: "ix_oferta_curso_curso_id",
                schema: "configuracao",
                table: "oferta_curso",
                column: "curso_id");
        }
    }
}
