using Microsoft.EntityFrameworkCore.Migrations;

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
            // gravada: a mesma normalização de ConjuntoBasicoDaInscricao.Referencia, com a sequência da época embutida. A seção de versão
            // publicada é imutável e não é tocada; a incompleta ou fora de faixa contígua é normalizada
            // na próxima gravação. A ordem passa por fora da faixa usada para não colidir no índice
            // único durante a troca.
            migrationBuilder.Sql("""
                CREATE TEMPORARY TABLE dados_basicos_canonicos AS
                SELECT c->>'fatoCodigo' AS fato_codigo, c->>'rotulo' AS rotulo, (posicao - 1)::int AS posicao
                FROM jsonb_array_elements('[{"fatoCodigo":"NOME","rotulo":"Nome completo"},{"fatoCodigo":"DESEJA_NOME_SOCIAL","rotulo":"Deseja usar nome social?"},{"fatoCodigo":"NOME_SOCIAL","rotulo":"Nome social"},{"fatoCodigo":"NACIONALIDADE","rotulo":"Nacionalidade"},{"fatoCodigo":"CPF","rotulo":"CPF"},{"fatoCodigo":"RG_NUMERO","rotulo":"Número do RG"},{"fatoCodigo":"RG_ORGAO_EMISSOR","rotulo":"Órgão emissor do RG"},{"fatoCodigo":"RG_ORGAO_EMISSOR_OUTRO","rotulo":"Nome do órgão emissor"},{"fatoCodigo":"RG_UF","rotulo":"UF de emissão do RG"},{"fatoCodigo":"RG_DATA_EMISSAO","rotulo":"Data de emissão do RG"},{"fatoCodigo":"PASSAPORTE_NUMERO","rotulo":"Número do passaporte"},{"fatoCodigo":"RNM_NUMERO","rotulo":"Número do Registro Nacional Migratório (RNM)"},{"fatoCodigo":"DATA_NASCIMENTO","rotulo":"Data de nascimento"},{"fatoCodigo":"NATURALIDADE_UF","rotulo":"UF de nascimento"},{"fatoCodigo":"NATURALIDADE_MUNICIPIO","rotulo":"Município de nascimento"},{"fatoCodigo":"NOME_MAE","rotulo":"Nome da mãe"},{"fatoCodigo":"NOME_PAI","rotulo":"Nome do pai"},{"fatoCodigo":"ESTADO_CIVIL","rotulo":"Estado civil"},{"fatoCodigo":"SEXO","rotulo":"Sexo biológico"},{"fatoCodigo":"SEXO_CIVIL","rotulo":"Sexo civil"},{"fatoCodigo":"IDENTIDADE_GENERO","rotulo":"Identidade de gênero"},{"fatoCodigo":"ORIENTACAO_SEXUAL","rotulo":"Orientação sexual"},{"fatoCodigo":"COR_RACA","rotulo":"Cor ou raça"},{"fatoCodigo":"EMAIL","rotulo":"E-mail"},{"fatoCodigo":"TELEFONE","rotulo":"Telefone"},{"fatoCodigo":"TIPO_ENDERECO","rotulo":"Tipo de localidade"},{"fatoCodigo":"NOME_COMUNIDADE","rotulo":"Nome da aldeia, comunidade ou quilombo"},{"fatoCodigo":"ENDERECO_RESIDENCIAL","rotulo":"Endereço residencial"}]'::jsonb) WITH ORDINALITY AS t(c, posicao);

                CREATE TEMPORARY TABLE dados_basicos_alvo AS
                SELECT f.processo_seletivo_id, MIN(f.ordem) AS inicio
                FROM selecao.fatos_coletados f
                JOIN selecao.processos_seletivos p ON p.id = f.processo_seletivo_id
                JOIN dados_basicos_canonicos c ON c.fato_codigo = f.fato_codigo
                WHERE p.status = 1 AND f.finalidade = 1
                  AND f.etapa_codigo = 'DADOS_BASICOS' AND f.grupo_coletado_id IS NULL
                GROUP BY f.processo_seletivo_id
                HAVING COUNT(*) = (SELECT COUNT(*) FROM dados_basicos_canonicos)
                   AND MAX(f.ordem) - MIN(f.ordem) + 1 = COUNT(*);

                UPDATE selecao.fatos_coletados f
                SET ordem = a.inicio + c.posicao + 1000000, rotulo = c.rotulo
                FROM dados_basicos_alvo a, dados_basicos_canonicos c
                WHERE f.processo_seletivo_id = a.processo_seletivo_id AND f.fato_codigo = c.fato_codigo
                  AND f.finalidade = 1 AND f.etapa_codigo = 'DADOS_BASICOS'
                  AND f.grupo_coletado_id IS NULL;

                UPDATE selecao.fatos_coletados f
                SET ordem = f.ordem - 1000000
                FROM dados_basicos_alvo a, dados_basicos_canonicos c
                WHERE f.processo_seletivo_id = a.processo_seletivo_id AND f.fato_codigo = c.fato_codigo
                  AND f.finalidade = 1 AND f.etapa_codigo = 'DADOS_BASICOS'
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
