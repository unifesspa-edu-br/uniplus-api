using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EscolheOOrgaoEmissorDoRgDeUmaLista : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // O órgão emissor do RG, na seção dos dados básicos, passa a ser escolhido de uma lista, e
            // o nome do órgão que falta nela entra logo depois dele, pedido a quem escolhe "Outro". Os
            // itens e os grupos que vinham depois descem uma posição; a ordem do item passa por fora
            // da faixa usada para não colidir no índice único durante a troca.
            migrationBuilder.Sql("""
                CREATE TEMPORARY TABLE orgao_emissor_alvo AS
                SELECT f.processo_seletivo_id, f.finalidade, f.ordem, f.obrigatoriedade, gen_random_uuid() AS novo_id
                FROM selecao.fatos_coletados f
                WHERE f.etapa_codigo = 'DADOS_BASICOS' AND f.fato_codigo = 'RG_ORGAO_EMISSOR' AND f.grupo_coletado_id IS NULL
                  AND NOT EXISTS (
                      SELECT 1 FROM selecao.fatos_coletados o
                      WHERE o.processo_seletivo_id = f.processo_seletivo_id AND o.fato_codigo = 'RG_ORGAO_EMISSOR_OUTRO');

                UPDATE selecao.fatos_coletados f
                SET ordem = f.ordem + 1000000
                FROM orgao_emissor_alvo a
                WHERE f.processo_seletivo_id = a.processo_seletivo_id AND f.finalidade = a.finalidade
                  AND f.grupo_coletado_id IS NULL AND f.ordem > a.ordem;

                UPDATE selecao.fatos_coletados f
                SET ordem = f.ordem - 999999
                FROM orgao_emissor_alvo a
                WHERE f.processo_seletivo_id = a.processo_seletivo_id AND f.finalidade = a.finalidade
                  AND f.grupo_coletado_id IS NULL AND f.ordem > 1000000;

                UPDATE selecao.grupos_coletados g
                SET ordem = g.ordem + 1
                FROM orgao_emissor_alvo a
                WHERE g.processo_seletivo_id = a.processo_seletivo_id AND g.finalidade = a.finalidade AND g.ordem > a.ordem;

                UPDATE selecao.fatos_coletados f
                SET tipo_renderizacao = 3, formato = NULL
                FROM orgao_emissor_alvo a
                WHERE f.processo_seletivo_id = a.processo_seletivo_id AND f.finalidade = a.finalidade
                  AND f.etapa_codigo = 'DADOS_BASICOS' AND f.fato_codigo = 'RG_ORGAO_EMISSOR';

                INSERT INTO selecao.fatos_coletados (
                    id, processo_seletivo_id, finalidade, etapa_codigo, fato_codigo, ordem, rotulo, tipo_renderizacao,
                    obrigatoriedade, ajuda, pedir_confirmacao, restricoes, impedimento, origem_valores, formato,
                    classificacao_protecao, grupo_coletado_id, created_at)
                SELECT a.novo_id, a.processo_seletivo_id, a.finalidade, 'DADOS_BASICOS', 'RG_ORGAO_EMISSOR_OUTRO', a.ordem + 1,
                       'Nome do órgão emissor', 5, a.obrigatoriedade, NULL, FALSE,
                       '[{"tipo":"TAMANHO_TEXTO","minimo":null,"maximo":60}]'::jsonb, NULL, 0, 'LIVRE', 'PESSOAL', NULL, now()
                FROM orgao_emissor_alvo a;

                INSERT INTO selecao.condicoes_precondicao_fato (id, fato_coletado_id, clausula, fato, operador, valor, created_at)
                SELECT gen_random_uuid(), a.novo_id, 0, 'RG_ORGAO_EMISSOR', 'IGUAL', '"OUTRO"'::jsonb, now()
                FROM orgao_emissor_alvo a;

                DROP TABLE orgao_emissor_alvo;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // O órgão emissor em texto livre não é restaurado.
        }
    }
}
