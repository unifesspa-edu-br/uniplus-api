using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaTokenConcorrenciaTipoProcesso : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Sem operação, de propósito. `xmin` é coluna de sistema do Postgres, já
            // presente em toda tabela: o mapeamento do token de concorrência otimista
            // apenas passa a lê-la, e não cria nada. O `AddColumn` que o scaffold emite
            // por padrão para esta mudança de modelo é rejeitado pelo banco com
            // `column name "xmin" conflicts with a system column name` — a migration
            // existe só para o snapshot acompanhar o modelo.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Simétrico ao Up: não há coluna criada para remover.
        }
    }
}
