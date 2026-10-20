using Microsoft.EntityFrameworkCore.Migrations;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RestabeleceAOrdemEOsRotulosDosDadosBasicos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Nos formulários de inscrição dos processos ainda não publicados, a seção dos dados básicos
            // volta à sequência e aos rótulos do conjunto básico, renumerada a partir da menor ordem
            // gravada: a mesma normalização de ConjuntoBasicoDaInscricao.Referencia. A seção de versão
            // publicada é imutável e não é tocada; a incompleta ou fora de faixa contígua é normalizada
            // na próxima gravação. A ordem passa por fora da faixa usada para não colidir no índice
            // único durante a troca.
            string sequencia = ConjuntoBasicoDaInscricao.SequenciaEmJson().Replace("'", "''", StringComparison.Ordinal);
            migrationBuilder.Sql($"""
                CREATE TEMPORARY TABLE dados_basicos_canonicos AS
                SELECT c->>'fatoCodigo' AS fato_codigo, c->>'rotulo' AS rotulo, (posicao - 1)::int AS posicao
                FROM jsonb_array_elements('{sequencia}'::jsonb) WITH ORDINALITY AS t(c, posicao);

                CREATE TEMPORARY TABLE dados_basicos_alvo AS
                SELECT f.processo_seletivo_id, MIN(f.ordem) AS inicio
                FROM selecao.fatos_coletados f
                JOIN selecao.processos_seletivos p ON p.id = f.processo_seletivo_id
                JOIN dados_basicos_canonicos c ON c.fato_codigo = f.fato_codigo
                WHERE p.status = {(int)StatusProcesso.Rascunho} AND f.finalidade = {(int)FinalidadeFormulario.Inscricao}
                  AND f.etapa_codigo = '{ConjuntoBasicoDaInscricao.CodigoDaSecao}' AND f.grupo_coletado_id IS NULL
                GROUP BY f.processo_seletivo_id
                HAVING COUNT(*) = (SELECT COUNT(*) FROM dados_basicos_canonicos)
                   AND MAX(f.ordem) - MIN(f.ordem) + 1 = COUNT(*);

                UPDATE selecao.fatos_coletados f
                SET ordem = a.inicio + c.posicao + 1000000, rotulo = c.rotulo
                FROM dados_basicos_alvo a, dados_basicos_canonicos c
                WHERE f.processo_seletivo_id = a.processo_seletivo_id AND f.fato_codigo = c.fato_codigo
                  AND f.finalidade = {(int)FinalidadeFormulario.Inscricao} AND f.etapa_codigo = '{ConjuntoBasicoDaInscricao.CodigoDaSecao}'
                  AND f.grupo_coletado_id IS NULL;

                UPDATE selecao.fatos_coletados f
                SET ordem = f.ordem - 1000000
                FROM dados_basicos_alvo a
                WHERE f.processo_seletivo_id = a.processo_seletivo_id AND f.finalidade = {(int)FinalidadeFormulario.Inscricao}
                  AND f.grupo_coletado_id IS NULL AND f.ordem >= 1000000;

                DROP TABLE dados_basicos_alvo;
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
