using Microsoft.EntityFrameworkCore.Migrations;

using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;

#nullable disable

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SemeiaPsrMedicina2027 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // O tipo de processo, os termos e os fatos do edital de Medicina 2027 ficam disponíveis
            // ao subir o sistema. As linhas saem das factories do domínio, e a inserção ignora a que
            // já existe: o cadastro é administrável, e reaplicar não desfaz o que foi editado na tela.
            foreach (string comando in SementePsrMedicina2027.ComandosDoTipoTermosEFatos())
            {
                migrationBuilder.Sql(comando);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (string comando in SementePsrMedicina2027.ComandosDeRemocao())
            {
                migrationBuilder.Sql(comando);
            }
        }
    }
}
