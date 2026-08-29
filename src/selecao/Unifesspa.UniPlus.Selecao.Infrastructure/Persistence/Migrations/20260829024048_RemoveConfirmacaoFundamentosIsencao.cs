using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveConfirmacaoFundamentosIsencao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "confirmacao_fundamentos",
                schema: "selecao",
                table: "configuracoes_taxa_inscricao");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "confirmacao_fundamentos",
                schema: "selecao",
                table: "configuracoes_taxa_inscricao",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                comment: "Confirmação explícita do administrador ao referenciar fundamentos de isenção (CA-06) — irrelevante quando fundamentos é vazio.");
        }
    }
}
