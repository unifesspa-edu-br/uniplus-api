using Microsoft.EntityFrameworkCore.Migrations;

using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;

#nullable disable

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExigeOLaudoDeQuemSolicitaAtendimentoNaSementePsrMedicina2027 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // O modelo de inscrição do PSR Medicina 2027 passa a exigir, de quem solicita atendimento
            // especializado, o laudo médico de cada condição ofertada e a certidão de nascimento da
            // criança da lactante, com a base legal resolvida. Saem os modelos, e a semente os grava
            // de novo.
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
