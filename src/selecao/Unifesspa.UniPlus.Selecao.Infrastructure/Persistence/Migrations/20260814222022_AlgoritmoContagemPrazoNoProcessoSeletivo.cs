using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AlgoritmoContagemPrazoNoProcessoSeletivo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "algoritmo_contagem_prazo_codigo",
                schema: "selecao",
                table: "processos_seletivos",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true,
                comment: "Código da entrada de algoritmo de contagem do rol_de_regras que o certame declarou.");

            migrationBuilder.AddColumn<string>(
                name: "algoritmo_contagem_prazo_hash",
                schema: "selecao",
                table: "processos_seletivos",
                type: "character(64)",
                fixedLength: true,
                maxLength: 64,
                nullable: true,
                comment: "Hash da definição resolvida no rol_de_regras — é o que prova que a convenção aplicada não mudou depois.");

            migrationBuilder.AddColumn<string>(
                name: "algoritmo_contagem_prazo_versao",
                schema: "selecao",
                table: "processos_seletivos",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true,
                comment: "Versão da entrada declarada — evolução da convenção é versão nova, nunca alteração da vigente.");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "algoritmo_contagem_prazo_codigo",
                schema: "selecao",
                table: "processos_seletivos");

            migrationBuilder.DropColumn(
                name: "algoritmo_contagem_prazo_hash",
                schema: "selecao",
                table: "processos_seletivos");

            migrationBuilder.DropColumn(
                name: "algoritmo_contagem_prazo_versao",
                schema: "selecao",
                table: "processos_seletivos");
        }
    }
}
