using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LimitaAsDatasDoConjuntoBasico : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // A data de nascimento e a de emissão do RG, na seção dos dados básicos, passam a aceitar
            // só datas entre 120 anos antes do envio e o dia do envio.
            migrationBuilder.Sql("""
                UPDATE selecao.fatos_coletados
                SET restricoes = '[{"tipo":"FAIXA_DE_DATA","dataMinima":{"ancora":"ENVIO","anos":-120,"dias":0},"dataMaxima":{"ancora":"ENVIO","anos":0,"dias":0}}]'::jsonb
                WHERE etapa_codigo = 'DADOS_BASICOS' AND fato_codigo IN ('DATA_NASCIMENTO', 'RG_DATA_EMISSAO');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // A faixa de data não é retirada.
        }
    }
}
