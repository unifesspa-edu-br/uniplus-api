using Microsoft.EntityFrameworkCore.Migrations;

using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;

#nullable disable

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeparaOModeloDeInscricaoPsrEmComEUmSemProva : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // O atendimento especializado serve à realização da prova, então o processo sem prova, como o
            // de Medicina, não o pergunta. O modelo único de inscrição vira dois: o sem prova, sem a seção
            // e os documentos do atendimento, e o com prova, que mantém ambos e limita a descrição da
            // outra condição. Saem os modelos, e a semente os grava de novo.
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
