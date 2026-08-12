using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaCidadeUnidadeAdministradora : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "unidade_administradora_cidade_codigo_ibge",
                schema: "selecao",
                table: "processos_seletivos",
                type: "character(7)",
                fixedLength: true,
                maxLength: 7,
                nullable: true,
                comment: "Snapshot-copy do código IBGE da cidade da Unidade administradora no momento da criação — nulo para processos anteriores à issue #1114.");

            migrationBuilder.AddColumn<string>(
                name: "unidade_administradora_cidade_nome",
                schema: "selecao",
                table: "processos_seletivos",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true,
                comment: "Snapshot-copy do nome de exibição da cidade da Unidade administradora no momento da criação.");

            migrationBuilder.AddColumn<string>(
                name: "unidade_administradora_cidade_uf",
                schema: "selecao",
                table: "processos_seletivos",
                type: "character(2)",
                fixedLength: true,
                maxLength: 2,
                nullable: true,
                comment: "Snapshot-copy da UF da cidade da Unidade administradora no momento da criação.");

            migrationBuilder.AddCheckConstraint(
                name: "ck_processos_seletivos_unidade_administradora_cidade_completa",
                schema: "selecao",
                table: "processos_seletivos",
                sql: "(unidade_administradora_cidade_codigo_ibge IS NULL AND unidade_administradora_cidade_nome IS NULL AND unidade_administradora_cidade_uf IS NULL) OR (unidade_administradora_cidade_codigo_ibge IS NOT NULL AND unidade_administradora_cidade_nome IS NOT NULL AND unidade_administradora_cidade_uf IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_processos_seletivos_unidade_administradora_cidade_completa",
                schema: "selecao",
                table: "processos_seletivos");

            migrationBuilder.DropColumn(
                name: "unidade_administradora_cidade_codigo_ibge",
                schema: "selecao",
                table: "processos_seletivos");

            migrationBuilder.DropColumn(
                name: "unidade_administradora_cidade_nome",
                schema: "selecao",
                table: "processos_seletivos");

            migrationBuilder.DropColumn(
                name: "unidade_administradora_cidade_uf",
                schema: "selecao",
                table: "processos_seletivos");
        }
    }
}
