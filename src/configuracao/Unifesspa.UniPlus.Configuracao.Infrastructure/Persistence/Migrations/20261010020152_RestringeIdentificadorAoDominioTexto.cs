using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RestringeIdentificadorAoDominioTexto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_rol_de_fatos_candidato_classificacao_minima_do_dominio",
                schema: "configuracao",
                table: "rol_de_fatos_candidato");

            migrationBuilder.AddCheckConstraint(
                name: "ck_rol_de_fatos_candidato_classificacao_minima_do_dominio",
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                sql: "(sistema AND codigo = 'NOME_SOCIAL' AND dominio = 'TEXTO' AND classificacao_protecao = 'PUBLICO') OR (dominio NOT IN ('TEXTO', 'DATA', 'ENDERECO') AND classificacao_protecao <> 'IDENTIFICADOR') OR (dominio = 'TEXTO' AND classificacao_protecao IN ('PESSOAL', 'IDENTIFICADOR', 'SENSIVEL')) OR (dominio IN ('DATA', 'ENDERECO') AND classificacao_protecao IN ('PESSOAL', 'SENSIVEL'))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_rol_de_fatos_candidato_classificacao_minima_do_dominio",
                schema: "configuracao",
                table: "rol_de_fatos_candidato");

            migrationBuilder.AddCheckConstraint(
                name: "ck_rol_de_fatos_candidato_classificacao_minima_do_dominio",
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                sql: "(sistema AND codigo = 'NOME_SOCIAL' AND dominio = 'TEXTO' AND classificacao_protecao = 'PUBLICO') OR dominio NOT IN ('TEXTO', 'DATA', 'ENDERECO') OR (dominio = 'TEXTO' AND classificacao_protecao IN ('PESSOAL', 'IDENTIFICADOR', 'SENSIVEL')) OR (dominio IN ('DATA', 'ENDERECO') AND classificacao_protecao IN ('PESSOAL', 'SENSIVEL'))");
        }
    }
}
