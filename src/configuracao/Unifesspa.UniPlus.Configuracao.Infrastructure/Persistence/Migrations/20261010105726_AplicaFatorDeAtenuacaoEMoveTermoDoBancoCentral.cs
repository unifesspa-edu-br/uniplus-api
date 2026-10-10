using Microsoft.EntityFrameworkCore.Migrations;

using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;

#nullable disable

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AplicaFatorDeAtenuacaoEMoveTermoDoBancoCentral : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Os dois fatos do fator de atenuação são novos (id nunca inserido); os demais fatos e
            // termos já existem e a inserção os ignora — reaplicar a lista inteira é seguro.
            foreach (string comando in SementePsrMedicina2027.ComandosDoTipoTermosEFatos())
            {
                migrationBuilder.Sql(comando);
            }

            // Os modelos já existem com o conteúdo anterior; a inserção da semente ignora o que já
            // existe, então saem antes de serem gravados de novo (mesmo padrão de
            // RegravaTextosDosModelosPsrMedicina2027).
            foreach (string comando in SementePsrMedicina2027.ComandosDeRemocaoDosModelos().Concat(SementePsrMedicina2027.ComandosDosModelos()))
            {
                migrationBuilder.Sql(comando);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Os fatos e o conteúdo anterior dos modelos não são restaurados: os modelos semeados ficam como estão.
        }
    }
}
