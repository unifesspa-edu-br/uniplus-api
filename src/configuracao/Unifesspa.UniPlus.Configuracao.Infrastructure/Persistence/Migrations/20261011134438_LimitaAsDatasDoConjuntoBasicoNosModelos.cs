using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LimitaAsDatasDoConjuntoBasicoNosModelos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Nos modelos de inscrição, a data de nascimento e a de emissão do RG, na seção dos dados
            // básicos, passam a aceitar só datas entre 120 anos antes do envio e o dia do envio.
            migrationBuilder.Sql("""
                UPDATE configuracao.modelos_formulario
                SET conteudo = jsonb_set(conteudo, '{itens}', (
                    SELECT jsonb_agg(
                        CASE
                            WHEN item->>'etapaCodigo' = 'DADOS_BASICOS' AND item->>'fatoCodigo' IN ('DATA_NASCIMENTO', 'RG_DATA_EMISSAO')
                                THEN jsonb_set(item, '{restricoes}', '[{"tipo":"FAIXA_DE_DATA","dataMinima":{"ancora":"ENVIO","anos":-120,"dias":0},"dataMaxima":{"ancora":"ENVIO","anos":0,"dias":0}}]'::jsonb)
                            ELSE item
                        END ORDER BY posicao)
                    FROM jsonb_array_elements(conteudo->'itens') WITH ORDINALITY AS itens(item, posicao)))
                WHERE conteudo->'itens' @> '[{"etapaCodigo":"DADOS_BASICOS","fatoCodigo":"DATA_NASCIMENTO"}]'
                   OR conteudo->'itens' @> '[{"etapaCodigo":"DADOS_BASICOS","fatoCodigo":"RG_DATA_EMISSAO"}]';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // A faixa de data não é retirada.
        }
    }
}
