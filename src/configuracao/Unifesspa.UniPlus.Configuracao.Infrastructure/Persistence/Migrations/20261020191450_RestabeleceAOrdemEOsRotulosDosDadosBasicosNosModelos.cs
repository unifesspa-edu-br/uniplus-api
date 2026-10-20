using Microsoft.EntityFrameworkCore.Migrations;
using Unifesspa.UniPlus.Regras.Formularios;

#nullable disable

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RestabeleceAOrdemEOsRotulosDosDadosBasicosNosModelos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Nos modelos de inscrição, a seção dos dados básicos volta à sequência e aos rótulos do
            // conjunto básico, renumerada a partir da menor ordem gravada: a mesma normalização de
            // ConjuntoBasicoDaInscricao.Referencia. O modelo cuja seção está incompleta ou fora de faixa
            // contígua é normalizado na próxima gravação.
            string sequencia = ConjuntoBasicoDaInscricao.SequenciaEmJson().Replace("'", "''", StringComparison.Ordinal);
            migrationBuilder.Sql($"""
                CREATE TEMPORARY TABLE dados_basicos_canonicos AS
                SELECT c->>'fatoCodigo' AS fato_codigo, c->>'rotulo' AS rotulo, (posicao - 1)::int AS posicao
                FROM jsonb_array_elements('{sequencia}'::jsonb) WITH ORDINALITY AS t(c, posicao);

                UPDATE configuracao.modelos_formulario m
                SET conteudo = jsonb_set(m.conteudo, ARRAY['itens'], (
                    SELECT jsonb_agg(
                        CASE
                            WHEN c.fato_codigo IS NOT NULL AND item->>'etapaCodigo' = '{ConjuntoBasicoDaInscricao.CodigoDaSecao}'
                                THEN item || jsonb_build_object('ordem', basicos.inicio + c.posicao, 'rotulo', c.rotulo)
                            ELSE item
                        END ORDER BY posicao_original)
                    FROM jsonb_array_elements(m.conteudo->'itens') WITH ORDINALITY AS itens(item, posicao_original)
                    LEFT JOIN dados_basicos_canonicos c ON c.fato_codigo = item->>'fatoCodigo'))
                FROM (
                    SELECT m2.id, MIN((item->>'ordem')::int) AS inicio, COUNT(*) AS total, MAX((item->>'ordem')::int) AS fim
                    FROM configuracao.modelos_formulario m2
                    CROSS JOIN LATERAL jsonb_array_elements(m2.conteudo->'itens') AS itens(item)
                    JOIN dados_basicos_canonicos c ON c.fato_codigo = item->>'fatoCodigo'
                    WHERE m2.finalidade = '{EstruturaFormulario.FinalidadeInscricao}' AND item->>'etapaCodigo' = '{ConjuntoBasicoDaInscricao.CodigoDaSecao}'
                    GROUP BY m2.id
                ) basicos
                WHERE m.id = basicos.id
                  AND basicos.total = (SELECT COUNT(*) FROM dados_basicos_canonicos)
                  AND basicos.fim - basicos.inicio + 1 = basicos.total;

                DROP TABLE dados_basicos_canonicos;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // A ordem e os rótulos anteriores não são restaurados.
        }
    }
}
