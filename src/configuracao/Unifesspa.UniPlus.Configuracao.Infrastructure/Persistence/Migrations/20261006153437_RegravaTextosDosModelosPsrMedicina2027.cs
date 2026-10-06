using Microsoft.EntityFrameworkCore.Migrations;

using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;

#nullable disable

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RegravaTextosDosModelosPsrMedicina2027 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Os modelos do PSR Medicina 2027 são gravados de novo com os rótulos e as ajudas revisados:
            // a inserção da semente ignora o modelo que já existe, então ele sai antes.
            foreach (string comando in SementePsrMedicina2027.ComandosDeRemocaoDosModelos().Concat(SementePsrMedicina2027.ComandosDosModelos()))
            {
                migrationBuilder.Sql(comando);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Os textos anteriores não são restaurados: os modelos semeados ficam como estão.
        }
    }
}
