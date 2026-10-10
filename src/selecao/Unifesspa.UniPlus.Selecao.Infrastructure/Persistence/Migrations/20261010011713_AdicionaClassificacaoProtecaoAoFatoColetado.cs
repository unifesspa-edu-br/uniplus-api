using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// Um fato coletado existente não tem como receber retroativamente a classificação certa
    /// sem consultar o catálogo, que a migration não alcança. Sem produção, um rascunho de
    /// processo em homologação é descartável (ADR-0137) — apaga em vez de gravar um valor fora
    /// do vocabulário fechado que o decoder do envelope recusaria na publicação.
    /// </remarks>
    public partial class AdicionaClassificacaoProtecaoAoFatoColetado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM selecao.fatos_coletados;");

            migrationBuilder.AddColumn<string>(
                name: "classificacao_protecao",
                schema: "selecao",
                table: "fatos_coletados",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "classificacao_protecao",
                schema: "selecao",
                table: "fatos_coletados");
        }
    }
}
