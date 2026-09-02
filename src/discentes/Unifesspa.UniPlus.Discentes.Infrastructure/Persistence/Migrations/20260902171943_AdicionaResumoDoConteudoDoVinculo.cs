using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Discentes.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaResumoDoConteudoDoVinculo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "resumo_do_conteudo",
                schema: "discentes",
                table: "vinculo_discente",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "",
                comment: "Resumo do conteúdo trazido do SIGAA na última sincronização — permite reconhecer que o vínculo não mudou e poupar a reescrita. Não cobre o CPF: como as demais colunas ficam legíveis, um resumo que o cobrisse permitiria recuperá-lo por tentativa e erro, desfazendo a cifra em repouso.");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "resumo_do_conteudo",
                schema: "discentes",
                table: "vinculo_discente");
        }
    }
}
