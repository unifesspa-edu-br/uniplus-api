using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Geo.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNomeOrdenacaoIndex : Migration
    {
        // Índice funcional B-tree que sustenta a ordenação alfabética server-side de
        // estado/cidade (#700, ADR-0094). A MR.EntityFrameworkCore.KeysetPagination gera
        // `ORDER BY COALESCE(nome_normalizado, ''), id` (coalesce não-nulo, ADR-0095) e o
        // seek por `COALESCE(nome_normalizado, '')` + id — então o índice precisa ser sobre
        // a MESMA expressão (um índice de coluna simples não casaria). Expression index não é
        // modelável via HasIndex do EF (não entra no snapshot) → SQL cru. A colação é a
        // default da coluna (= a do ORDER BY): consistente por construção; nome_normalizado é
        // minúscula+sem-acento, então a ordem é alfabética. CONCURRENTLY é omitido (sem dados
        // em produção; criação dentro da transação da migration).

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "CREATE INDEX ix_estado_nome_ordenacao ON estado (COALESCE(nome_normalizado, ''), id);");
            migrationBuilder.Sql(
                "CREATE INDEX ix_cidade_nome_ordenacao ON cidade (COALESCE(nome_normalizado, ''), id);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_cidade_nome_ordenacao;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_estado_nome_ordenacao;");
        }
    }
}
