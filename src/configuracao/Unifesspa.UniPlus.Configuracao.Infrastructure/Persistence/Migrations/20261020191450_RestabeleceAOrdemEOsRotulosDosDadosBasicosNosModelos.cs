using Microsoft.EntityFrameworkCore.Migrations;

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
            migrationBuilder.Sql("""
                CREATE TEMPORARY TABLE dados_basicos_canonicos AS
                SELECT c->>'fatoCodigo' AS fato_codigo, c->>'rotulo' AS rotulo, (posicao - 1)::int AS posicao
                FROM jsonb_array_elements('[{"fatoCodigo":"NOME","rotulo":"Nome completo"},{"fatoCodigo":"DESEJA_NOME_SOCIAL","rotulo":"Deseja usar nome social?"},{"fatoCodigo":"NOME_SOCIAL","rotulo":"Nome social"},{"fatoCodigo":"NACIONALIDADE","rotulo":"Nacionalidade"},{"fatoCodigo":"CPF","rotulo":"CPF"},{"fatoCodigo":"RG_NUMERO","rotulo":"Número do RG"},{"fatoCodigo":"RG_ORGAO_EMISSOR","rotulo":"Órgão emissor do RG"},{"fatoCodigo":"RG_ORGAO_EMISSOR_OUTRO","rotulo":"Nome do órgão emissor"},{"fatoCodigo":"RG_UF","rotulo":"UF de emissão do RG"},{"fatoCodigo":"RG_DATA_EMISSAO","rotulo":"Data de emissão do RG"},{"fatoCodigo":"PASSAPORTE_NUMERO","rotulo":"Número do passaporte"},{"fatoCodigo":"RNM_NUMERO","rotulo":"Número do Registro Nacional Migratório (RNM)"},{"fatoCodigo":"DATA_NASCIMENTO","rotulo":"Data de nascimento"},{"fatoCodigo":"NATURALIDADE_UF","rotulo":"UF de nascimento"},{"fatoCodigo":"NATURALIDADE_MUNICIPIO","rotulo":"Município de nascimento"},{"fatoCodigo":"NOME_MAE","rotulo":"Nome da mãe"},{"fatoCodigo":"NOME_PAI","rotulo":"Nome do pai"},{"fatoCodigo":"ESTADO_CIVIL","rotulo":"Estado civil"},{"fatoCodigo":"SEXO","rotulo":"Sexo biológico"},{"fatoCodigo":"SEXO_CIVIL","rotulo":"Sexo civil"},{"fatoCodigo":"IDENTIDADE_GENERO","rotulo":"Identidade de gênero"},{"fatoCodigo":"ORIENTACAO_SEXUAL","rotulo":"Orientação sexual"},{"fatoCodigo":"COR_RACA","rotulo":"Cor ou raça"},{"fatoCodigo":"EMAIL","rotulo":"E-mail"},{"fatoCodigo":"TELEFONE","rotulo":"Telefone"},{"fatoCodigo":"TIPO_ENDERECO","rotulo":"Tipo de localidade"},{"fatoCodigo":"NOME_COMUNIDADE","rotulo":"Nome da aldeia, comunidade ou quilombo"},{"fatoCodigo":"ENDERECO_RESIDENCIAL","rotulo":"Endereço residencial"}]'::jsonb) WITH ORDINALITY AS t(c, posicao);

                UPDATE configuracao.modelos_formulario m
                SET conteudo = jsonb_set(m.conteudo, '{itens}', (
                    SELECT jsonb_agg(
                        CASE
                            WHEN c.fato_codigo IS NOT NULL AND item->>'etapaCodigo' = 'DADOS_BASICOS'
                                THEN item || jsonb_build_object('ordem', basicos.inicio + c.posicao, 'rotulo', c.rotulo)
                            ELSE item
                        END ORDER BY
                            CASE
                                WHEN c.fato_codigo IS NOT NULL AND item->>'etapaCodigo' = 'DADOS_BASICOS'
                                    THEN basicos.inicio + c.posicao
                                ELSE (item->>'ordem')::int
                            END,
                            posicao_original)
                    FROM jsonb_array_elements(m.conteudo->'itens') WITH ORDINALITY AS itens(item, posicao_original)
                    LEFT JOIN dados_basicos_canonicos c ON c.fato_codigo = item->>'fatoCodigo'))
                FROM (
                    SELECT m2.id, MIN((item->>'ordem')::int) AS inicio, COUNT(*) AS total, MAX((item->>'ordem')::int) AS fim
                    FROM configuracao.modelos_formulario m2
                    CROSS JOIN LATERAL jsonb_array_elements(m2.conteudo->'itens') AS itens(item)
                    JOIN dados_basicos_canonicos c ON c.fato_codigo = item->>'fatoCodigo'
                    WHERE m2.finalidade = 'INSCRICAO' AND item->>'etapaCodigo' = 'DADOS_BASICOS'
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
