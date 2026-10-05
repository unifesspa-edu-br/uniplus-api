using Microsoft.EntityFrameworkCore.Migrations;

using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;

#nullable disable

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SemeiaModelosPsrMedicina2027 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Os formulários de inscrição e de habilitação do edital de Medicina 2027, ativos, ficam
            // disponíveis ao subir o sistema para o processo PSR partir deles. O conteúdo é conferido
            // pelo domínio ao ser construído, e a inserção ignora o modelo que já existe.
            foreach (string comando in SementePsrMedicina2027.ComandosDosModelos())
            {
                migrationBuilder.Sql(comando);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (string comando in SementePsrMedicina2027.ComandosDeRemocaoDosModelos())
            {
                migrationBuilder.Sql(comando);
            }
        }
    }
}
