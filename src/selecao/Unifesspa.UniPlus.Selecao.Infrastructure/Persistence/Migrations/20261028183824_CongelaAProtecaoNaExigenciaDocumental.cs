using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// A exigência já gravada recebe, pelo default da coluna, o padrão provisório (pessoal, com a
    /// finalidade e a hipótese comuns do cadastro): a migration não alcança o tipo de documento, que
    /// está em outro banco. A publicação e a retificação relêem o tipo e congelam o valor vigente, e
    /// é isso que substitui o padrão.
    /// </remarks>
    public partial class CongelaAProtecaoNaExigenciaDocumental : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "classificacao_protecao",
                schema: "selecao",
                table: "documentos_exigidos",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "PESSOAL");

            migrationBuilder.AddColumn<string[]>(
                name: "fatos_comprovados",
                schema: "selecao",
                table: "documentos_exigidos",
                type: "text[]",
                nullable: false,
                defaultValue: Array.Empty<string>());

            migrationBuilder.AddColumn<string>(
                name: "finalidade_tratamento",
                schema: "selecao",
                table: "documentos_exigidos",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.");

            migrationBuilder.AddColumn<string>(
                name: "hipotese_legal",
                schema: "selecao",
                table: "documentos_exigidos",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "CUMPRIMENTO_OBRIGACAO_LEGAL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "classificacao_protecao",
                schema: "selecao",
                table: "documentos_exigidos");

            migrationBuilder.DropColumn(
                name: "fatos_comprovados",
                schema: "selecao",
                table: "documentos_exigidos");

            migrationBuilder.DropColumn(
                name: "finalidade_tratamento",
                schema: "selecao",
                table: "documentos_exigidos");

            migrationBuilder.DropColumn(
                name: "hipotese_legal",
                schema: "selecao",
                table: "documentos_exigidos");
        }
    }
}
