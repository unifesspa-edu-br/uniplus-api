using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FacetasConsultaveisDaVitrine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // A função precede a coluna que a usa: uma coluna gerada não pode referenciar função
            // inexistente. IMMUTABLE é o que a habilita a servir coluna gerada e índice; STRICT
            // dispensa tratar o nulo no corpo. A tabela de substituição é a mesma que a
            // Configuração aplica — a função vive no schema de cada módulo porque os schemas não se
            // atravessam, mas o mesmo texto tem de normalizar igual em qualquer listagem.
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION selecao.normalizar_para_comparacao(text)
                    RETURNS text
                    LANGUAGE sql
                    IMMUTABLE
                    PARALLEL SAFE
                    STRICT
                    RETURN lower(translate(normalize($1, NFC),
                        'ÁÀÂÃÄÅÉÈÊËÍÌÎÏÓÒÔÕÖÚÙÛÜÇÑÝáàâãäåéèêëíìîïóòôõöúùûüçñý',
                        'AAAAAAEEEEIIIIOOOOOUUUUCNYaaaaaaeeeeiiiiooooouuuucny') COLLATE "C");
                """);

            migrationBuilder.AddColumn<string[]>(
                name: "modalidades_ofertadas",
                schema: "selecao",
                table: "certames_divulgados",
                type: "text[]",
                nullable: false);

            migrationBuilder.AddColumn<string>(
                name: "nome",
                schema: "selecao",
                table: "certames_divulgados",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false);

            migrationBuilder.AddColumn<string>(
                name: "numero",
                schema: "selecao",
                table: "certames_divulgados",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "nome_ordenacao",
                schema: "selecao",
                table: "certames_divulgados",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                computedColumnSql: "selecao.normalizar_para_comparacao(nome)",
                stored: true,
                collation: "C");

            migrationBuilder.CreateIndex(
                name: "ix_certames_divulgados_modalidades",
                schema: "selecao",
                table: "certames_divulgados",
                column: "modalidades_ofertadas")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "ix_certames_divulgados_nome_ordenacao",
                schema: "selecao",
                table: "certames_divulgados",
                columns: new[] { "nome_ordenacao", "id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_certames_divulgados_modalidades",
                schema: "selecao",
                table: "certames_divulgados");

            migrationBuilder.DropIndex(
                name: "ix_certames_divulgados_nome_ordenacao",
                schema: "selecao",
                table: "certames_divulgados");

            migrationBuilder.DropColumn(
                name: "nome_ordenacao",
                schema: "selecao",
                table: "certames_divulgados");

            migrationBuilder.DropColumn(
                name: "modalidades_ofertadas",
                schema: "selecao",
                table: "certames_divulgados");

            migrationBuilder.DropColumn(
                name: "nome",
                schema: "selecao",
                table: "certames_divulgados");

            migrationBuilder.DropColumn(
                name: "numero",
                schema: "selecao",
                table: "certames_divulgados");

            // Depois das colunas: a função não pode sair enquanto a coluna gerada depende dela.
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS selecao.normalizar_para_comparacao(text);");
        }
    }
}
