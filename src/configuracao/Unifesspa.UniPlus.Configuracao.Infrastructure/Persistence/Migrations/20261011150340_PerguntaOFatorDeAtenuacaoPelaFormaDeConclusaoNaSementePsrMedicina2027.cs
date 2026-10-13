using Microsoft.EntityFrameworkCore.Migrations;

using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;

#nullable disable

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PerguntaOFatorDeAtenuacaoPelaFormaDeConclusaoNaSementePsrMedicina2027 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // O modelo de inscrição do PSR Medicina 2027 passa a perguntar o dado do Fator de Atenuação
            // conforme a forma de conclusão do ensino médio: o município em que o candidato cursou o
            // ensino médio e, para quem concluiu por certificação, a escolha entre ele e o município em
            // que residiu no período do edital. Saem os modelos e o município de residência, que deixa
            // de falar na residência atual; a semente grava de novo os fatos, com os dois novos, e os
            // modelos.
            foreach (string comando in SementePsrMedicina2027.ComandosDeRemocaoDosModelos()
                .Concat(SementePsrMedicina2027.ComandosQueRetiramOMunicipioDeResidenciaDoFator())
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
