using Microsoft.EntityFrameworkCore.Migrations;

using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;

#nullable disable

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PerguntaAElegibilidadeDaReservaDepoisDaOpcaoNaSementePsrMedicina2027 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // O modelo de inscrição do PSR Medicina 2027 passa a trazer a seção dos dados básicos e a
            // fazer as perguntas das cotas da lei só a quem optou por elas: o pertencimento indígena, a
            // quem se declarou indígena na cor ou raça, e a opção pela PPI, a quem é preto, pardo ou
            // indígena com o pertencimento. Saem os modelos, e a semente os grava de novo.
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
