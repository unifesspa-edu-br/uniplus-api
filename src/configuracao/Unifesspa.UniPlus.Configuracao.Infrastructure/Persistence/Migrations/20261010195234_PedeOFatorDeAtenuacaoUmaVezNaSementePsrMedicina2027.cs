using Microsoft.EntityFrameworkCore.Migrations;

using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;

#nullable disable

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PedeOFatorDeAtenuacaoUmaVezNaSementePsrMedicina2027 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // O modelo de inscrição do PSR Medicina 2027 passa a pedir o Fator de Atenuação uma vez e, a quem o
            // pede, o município de residência. Saem os modelos e os fatos antigos do pedido, e a semente grava
            // de novo os fatos e os modelos: a inserção ignora a linha que já existe.
            foreach (string comando in SementePsrMedicina2027.ComandosDeRemocaoDosModelos()
                .Concat(SementePsrMedicina2027.ComandosQueRetiramOsFatosAntigosDoFator())
                .Concat(SementePsrMedicina2027.ComandosDoTipoTermosEFatos())
                .Concat(SementePsrMedicina2027.ComandosDosModelos()))
            {
                migrationBuilder.Sql(comando);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Os fatos e o modelo anteriores não são restaurados: a semente fica como está.
        }
    }
}
