using Microsoft.EntityFrameworkCore.Migrations;

using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;

#nullable disable

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PedeAEdicaoDoEnemNoModeloDeInscricaoPsrMedicina2027 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // O modelo de inscrição do PSR Medicina 2027 passa a pedir a edição do ENEM, numa seção própria:
            // a inserção da semente ignora o modelo que já existe, então os modelos saem antes.
            foreach (string comando in SementePsrMedicina2027.ComandosDeRemocaoDosModelos().Concat(SementePsrMedicina2027.ComandosDosModelos()))
            {
                migrationBuilder.Sql(comando);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // O modelo anterior não é restaurado: os modelos semeados ficam como estão.
        }
    }
}
