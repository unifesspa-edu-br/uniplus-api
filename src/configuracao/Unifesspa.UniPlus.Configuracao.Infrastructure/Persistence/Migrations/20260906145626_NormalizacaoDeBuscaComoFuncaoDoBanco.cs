using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NormalizacaoDeBuscaComoFuncaoDoBanco : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // A função precede a coluna que a usa: uma coluna gerada não pode
            // referenciar função inexistente. IMMUTABLE é o que a habilita a servir
            // coluna gerada e índice; STRICT dispensa tratar o nulo no corpo.
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION configuracao.normalizar_para_comparacao(text)
                    RETURNS text
                    LANGUAGE sql
                    IMMUTABLE
                    PARALLEL SAFE
                    STRICT
                    RETURN lower(translate(normalize($1, NFC),
                        'ÁÀÂÃÄÅÉÈÊËÍÌÎÏÓÒÔÕÖÚÙÛÜÇÑÝáàâãäåéèêëíìîïóòôõöúùûüçñý',
                        'AAAAAAEEEEIIIIOOOOOUUUUCNYaaaaaaeeeeiiiiooooouuuucny') COLLATE "C");
                """);

            migrationBuilder.AlterColumn<string>(
                name: "nome_ordenacao",
                schema: "configuracao",
                table: "curso",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                computedColumnSql: "configuracao.normalizar_para_comparacao(nome)",
                stored: true,
                collation: "C",
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldComputedColumnSql: "lower(translate(normalize(nome, NFC), 'ÁÀÂÃÄÅÉÈÊËÍÌÎÏÓÒÔÕÖÚÙÛÜÇÑÝáàâãäåéèêëíìîïóòôõöúùûüçñý', 'AAAAAAEEEEIIIIOOOOOUUUUCNYaaaaaaeeeeiiiiooooouuuucny') COLLATE \"C\")",
                oldStored: true,
                oldCollation: "C");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "nome_ordenacao",
                schema: "configuracao",
                table: "curso",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                computedColumnSql: "lower(translate(normalize(nome, NFC), 'ÁÀÂÃÄÅÉÈÊËÍÌÎÏÓÒÔÕÖÚÙÛÜÇÑÝáàâãäåéèêëíìîïóòôõöúùûüçñý', 'AAAAAAEEEEIIIIOOOOOUUUUCNYaaaaaaeeeeiiiiooooouuuucny') COLLATE \"C\")",
                stored: true,
                collation: "C",
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldComputedColumnSql: "configuracao.normalizar_para_comparacao(nome)",
                oldStored: true,
                oldCollation: "C");

            // A coluna volta a embutir a expressão, então a função deixa de ter uso.
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS configuracao.normalizar_para_comparacao(text);");
        }
    }
}
