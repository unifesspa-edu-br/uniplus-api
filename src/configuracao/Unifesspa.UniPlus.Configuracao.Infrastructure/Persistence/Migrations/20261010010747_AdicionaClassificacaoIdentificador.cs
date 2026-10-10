using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// Reclassifica os três fatos de sistema que identificam o candidato por número de documento
    /// (CPF, RG_NUMERO, DOCUMENTO_ESTRANGEIRO_NUMERO) de <c>PESSOAL</c> para <c>IDENTIFICADOR</c>
    /// (ADR-0136, emenda de #1856). Nenhum dos três é citado como dependência de derivado nem de regra
    /// padrão no catálogo — a reclassificação não invalida nada existente.
    /// </remarks>
    public partial class AdicionaClassificacaoIdentificador : Migration
    {
        private const string CodigosIdentificador = "'CPF', 'RG_NUMERO', 'DOCUMENTO_ESTRANGEIRO_NUMERO'";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_rol_de_fatos_candidato_classificacao_minima_do_dominio",
                schema: "configuracao",
                table: "rol_de_fatos_candidato");

            migrationBuilder.DropCheckConstraint(
                name: "ck_rol_de_fatos_candidato_classificacao_protecao",
                schema: "configuracao",
                table: "rol_de_fatos_candidato");

            // Condicionado ao texto semeado (sistema + código + classificação atual), não a
            // UpdateData por id que o scaffold gera: classificacao_protecao não é hoje um eixo
            // editável por tela em fato de sistema, mas o condicional protege do mesmo jeito que a
            // correção de base legal de PCD_PURO — se algo já tiver mudado essa linha por outro
            // caminho, o UPDATE não a toca.
            migrationBuilder.Sql(
                $"""
                UPDATE configuracao.rol_de_fatos_candidato
                   SET classificacao_protecao = 'IDENTIFICADOR'
                 WHERE sistema
                   AND codigo IN ({CodigosIdentificador})
                   AND classificacao_protecao = 'PESSOAL';
                """);

            migrationBuilder.AddCheckConstraint(
                name: "ck_rol_de_fatos_candidato_classificacao_minima_do_dominio",
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                sql: "(sistema AND codigo = 'NOME_SOCIAL' AND dominio = 'TEXTO' AND classificacao_protecao = 'PUBLICO') OR dominio NOT IN ('TEXTO', 'DATA', 'ENDERECO') OR (dominio = 'TEXTO' AND classificacao_protecao IN ('PESSOAL', 'IDENTIFICADOR', 'SENSIVEL')) OR (dominio IN ('DATA', 'ENDERECO') AND classificacao_protecao IN ('PESSOAL', 'SENSIVEL'))");

            migrationBuilder.AddCheckConstraint(
                name: "ck_rol_de_fatos_candidato_classificacao_protecao",
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                sql: "classificacao_protecao IN ('PUBLICO', 'INTERNO', 'PESSOAL', 'IDENTIFICADOR', 'SENSIVEL')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_rol_de_fatos_candidato_classificacao_minima_do_dominio",
                schema: "configuracao",
                table: "rol_de_fatos_candidato");

            migrationBuilder.DropCheckConstraint(
                name: "ck_rol_de_fatos_candidato_classificacao_protecao",
                schema: "configuracao",
                table: "rol_de_fatos_candidato");

            migrationBuilder.Sql(
                $"""
                UPDATE configuracao.rol_de_fatos_candidato
                   SET classificacao_protecao = 'PESSOAL'
                 WHERE sistema
                   AND codigo IN ({CodigosIdentificador})
                   AND classificacao_protecao = 'IDENTIFICADOR';
                """);

            migrationBuilder.AddCheckConstraint(
                name: "ck_rol_de_fatos_candidato_classificacao_minima_do_dominio",
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                sql: "(sistema AND codigo = 'NOME_SOCIAL' AND dominio = 'TEXTO' AND classificacao_protecao = 'PUBLICO') OR dominio NOT IN ('TEXTO', 'DATA', 'ENDERECO') OR classificacao_protecao IN ('PESSOAL', 'SENSIVEL')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_rol_de_fatos_candidato_classificacao_protecao",
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                sql: "classificacao_protecao IN ('PUBLICO', 'INTERNO', 'PESSOAL', 'SENSIVEL')");
        }
    }
}
