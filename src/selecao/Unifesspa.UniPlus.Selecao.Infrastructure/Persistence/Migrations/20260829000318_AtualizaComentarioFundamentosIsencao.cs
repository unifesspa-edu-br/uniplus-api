using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AtualizaComentarioFundamentosIsencao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "fundamentos",
                schema: "selecao",
                table: "configuracoes_taxa_inscricao",
                type: "jsonb",
                nullable: false,
                comment: "Fundamentos de isenção referenciados (tokens de FundamentoIsencaoCodigo), deduplicados e em ordem canônica; vazio somente quando cobra=false (issue #1310).",
                oldClrType: typeof(string),
                oldType: "jsonb",
                oldComment: "Fundamentos de isenção referenciados (tokens de FundamentoIsencaoCodigo), deduplicados e em ordem canônica; vazio é estado válido (CA-04).");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "fundamentos",
                schema: "selecao",
                table: "configuracoes_taxa_inscricao",
                type: "jsonb",
                nullable: false,
                comment: "Fundamentos de isenção referenciados (tokens de FundamentoIsencaoCodigo), deduplicados e em ordem canônica; vazio é estado válido (CA-04).",
                oldClrType: typeof(string),
                oldType: "jsonb",
                oldComment: "Fundamentos de isenção referenciados (tokens de FundamentoIsencaoCodigo), deduplicados e em ordem canônica; vazio somente quando cobra=false (issue #1310).");
        }
    }
}
