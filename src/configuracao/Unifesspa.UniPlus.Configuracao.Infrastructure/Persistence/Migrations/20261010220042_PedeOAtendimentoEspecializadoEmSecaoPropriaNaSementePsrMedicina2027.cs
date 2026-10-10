using Microsoft.EntityFrameworkCore.Migrations;

using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;

#nullable disable

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PedeOAtendimentoEspecializadoEmSecaoPropriaNaSementePsrMedicina2027 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // O modelo de inscrição do PSR Medicina 2027 passa a pedir o atendimento especializado numa
            // seção própria: o pedido abre a condição e o recurso, e a condição fora da lista é descrita.
            // Saem os modelos, e a semente grava de novo os fatos e os modelos: a inserção ignora a linha
            // que já existe, e os dois fatos novos do pedido entram.
            foreach (string comando in SementePsrMedicina2027.ComandosDeRemocaoDosModelos()
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
