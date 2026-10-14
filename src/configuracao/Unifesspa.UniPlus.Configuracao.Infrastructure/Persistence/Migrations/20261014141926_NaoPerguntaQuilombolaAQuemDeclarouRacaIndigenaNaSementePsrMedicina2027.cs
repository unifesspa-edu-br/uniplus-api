using Microsoft.EntityFrameworkCore.Migrations;

using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;

#nullable disable

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NaoPerguntaQuilombolaAQuemDeclarouRacaIndigenaNaSementePsrMedicina2027 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // O modelo de inscrição do PSR Medicina 2027 deixa de perguntar se o candidato é quilombola
            // a quem declarou raça indígena, e a pergunta passa a explicar que as vagas para pretos e
            // pardos são para quem informou essa cor ou raça. Saem os modelos, e a semente os grava de
            // novo.
            foreach (string comando in SementePsrMedicina2027.ComandosDeRemocaoDosModelos()
                .Concat(SementePsrMedicina2027.ComandosDosModelos()))
            {
                migrationBuilder.Sql(comando);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Os modelos anteriores não são restaurados: a semente fica como está.
        }
    }
}
