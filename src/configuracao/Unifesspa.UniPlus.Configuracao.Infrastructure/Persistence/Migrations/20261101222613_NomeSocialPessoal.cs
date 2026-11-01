using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NomeSocialPessoal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_rol_de_fatos_candidato_classificacao_minima_do_dominio",
                schema: "configuracao",
                table: "rol_de_fatos_candidato");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000029"),
                column: "classificacao_protecao",
                value: "PESSOAL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_rol_de_fatos_candidato_classificacao_minima_do_dominio",
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                sql: "(dominio NOT IN ('TEXTO', 'DATA', 'ENDERECO') AND classificacao_protecao <> 'IDENTIFICADOR') OR (dominio = 'TEXTO' AND classificacao_protecao IN ('PESSOAL', 'IDENTIFICADOR', 'SENSIVEL')) OR (dominio IN ('DATA', 'ENDERECO') AND classificacao_protecao IN ('PESSOAL', 'SENSIVEL'))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_rol_de_fatos_candidato_classificacao_minima_do_dominio",
                schema: "configuracao",
                table: "rol_de_fatos_candidato");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000029"),
                column: "classificacao_protecao",
                value: "PUBLICO");

            migrationBuilder.AddCheckConstraint(
                name: "ck_rol_de_fatos_candidato_classificacao_minima_do_dominio",
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                sql: "(sistema AND codigo = 'NOME_SOCIAL' AND dominio = 'TEXTO' AND classificacao_protecao = 'PUBLICO') OR (dominio NOT IN ('TEXTO', 'DATA', 'ENDERECO') AND classificacao_protecao <> 'IDENTIFICADOR') OR (dominio = 'TEXTO' AND classificacao_protecao IN ('PESSOAL', 'IDENTIFICADOR', 'SENSIVEL')) OR (dominio IN ('DATA', 'ENDERECO') AND classificacao_protecao IN ('PESSOAL', 'SENSIVEL'))");
        }
    }
}
