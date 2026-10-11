using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EscolheOOrgaoEmissorDoRgDeUmaLista : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000032"),
                columns: new[] { "dominio", "fonte_valores", "formato" },
                values: new object[] { "CATEGORICO", "GLOBAL", null });

            migrationBuilder.InsertData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                columns: new[] { "id", "ativo", "codigo", "created_at", "descricao", "fato_candidato_id", "ordem", "orientacao", "updated_at" },
                values: new object[,]
                {
                    { new Guid("fa70d000-0000-7000-8000-000000000057"), true, "SSP", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Secretaria de Segurança Pública (SSP)", new Guid("fa700000-0000-7000-8000-000000000032"), 0, null, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000058"), true, "PC", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Polícia Civil (PC)", new Guid("fa700000-0000-7000-8000-000000000032"), 1, null, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000059"), true, "DETRAN", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Departamento Estadual de Trânsito (DETRAN)", new Guid("fa700000-0000-7000-8000-000000000032"), 2, null, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000060"), true, "IFP", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Instituto Félix Pacheco (IFP)", new Guid("fa700000-0000-7000-8000-000000000032"), 3, null, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000061"), true, "PM", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Polícia Militar (PM)", new Guid("fa700000-0000-7000-8000-000000000032"), 4, null, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000062"), true, "CBM", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Corpo de Bombeiros Militar (CBM)", new Guid("fa700000-0000-7000-8000-000000000032"), 5, null, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000063"), true, "EB", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Exército Brasileiro (EB)", new Guid("fa700000-0000-7000-8000-000000000032"), 6, null, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000064"), true, "MB", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Marinha do Brasil (MB)", new Guid("fa700000-0000-7000-8000-000000000032"), 7, null, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000065"), true, "FAB", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Força Aérea Brasileira (FAB)", new Guid("fa700000-0000-7000-8000-000000000032"), 8, null, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000066"), true, "DPF", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Departamento de Polícia Federal (DPF)", new Guid("fa700000-0000-7000-8000-000000000032"), 9, null, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000067"), true, "MRE", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Ministério das Relações Exteriores (MRE)", new Guid("fa700000-0000-7000-8000-000000000032"), 10, null, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000068"), true, "OUTRO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Outro", new Guid("fa700000-0000-7000-8000-000000000032"), 11, null, null }
                });

            migrationBuilder.InsertData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                columns: new[] { "id", "ativo", "binding", "cardinalidade", "classificacao_protecao", "codigo", "created_at", "created_by", "dependencias", "descricao", "dominio", "escopo", "finalidade_tratamento", "fonte_valores", "formato", "hipotese_legal", "nome", "origem", "ponto_resolucao", "regras_padrao", "sistema", "updated_at", "updated_by" },
                values: new object[] { new Guid("fa700000-0000-7000-8000-000000000056"), true, "CAMPO_FORMULARIO:RG_ORGAO_EMISSOR_OUTRO", "ESCALAR", "PESSOAL", "RG_ORGAO_EMISSOR_OUTRO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, Array.Empty<string>(), "O órgão emissor do RG que não está na lista.", "TEXTO", "CANDIDATO", "Identificação do candidato no processo seletivo.", null, "LIVRE", "CUMPRIMENTO_OBRIGACAO_LEGAL", "Nome do órgão emissor do RG", "DECLARADO", "INSCRICAO", "[]", true, null, null });


            // Nos modelos de inscrição, o órgão emissor do RG, na seção dos dados básicos, passa a ser
            // escolhido de uma lista, e o nome do órgão que falta nela entra logo depois dele: os itens
            // e os grupos que vinham depois descem uma posição.
            migrationBuilder.Sql("""
                WITH alvo AS (
                    SELECT m.id, (i.item->>'ordem')::int AS ordem, i.posicao
                    FROM configuracao.modelos_formulario m,
                         jsonb_array_elements(m.conteudo->'itens') WITH ORDINALITY AS i(item, posicao)
                    WHERE i.item->>'etapaCodigo' = 'DADOS_BASICOS' AND i.item->>'fatoCodigo' = 'RG_ORGAO_EMISSOR'
                      AND NOT m.conteudo->'itens' @> '[{"fatoCodigo":"RG_ORGAO_EMISSOR_OUTRO"}]')
                UPDATE configuracao.modelos_formulario m
                SET conteudo = jsonb_set(jsonb_set(m.conteudo, '{itens}', (
                        SELECT jsonb_agg(itens.item ORDER BY itens.posicao)
                        FROM (
                            SELECT CASE
                                       WHEN i.posicao = alvo.posicao
                                           THEN i.item || '{"tipoRenderizacao":"SELECAO_UNICA","formato":null}'::jsonb
                                       WHEN (i.item->>'ordem')::int > alvo.ordem
                                           THEN jsonb_set(i.item, '{ordem}', to_jsonb((i.item->>'ordem')::int + 1))
                                       ELSE i.item
                                   END AS item,
                                   i.posicao::numeric AS posicao
                            FROM jsonb_array_elements(m.conteudo->'itens') WITH ORDINALITY AS i(item, posicao)
                            UNION ALL
                            SELECT jsonb_build_object(
                                       'fatoCodigo', 'RG_ORGAO_EMISSOR_OUTRO', 'ordem', alvo.ordem + 1, 'etapaCodigo', 'DADOS_BASICOS',
                                       'rotulo', 'Nome do órgão emissor', 'tipoRenderizacao', 'TEXTO', 'formato', 'LIVRE', 'ajuda', NULL,
                                       'obrigatoriedade', '{"tipo":"SEMPRE","predicado":null}'::jsonb,
                                       'exibicao', '[[{"fato":"RG_ORGAO_EMISSOR","operador":"IGUAL","valor":"OUTRO"}]]'::jsonb,
                                       'restricoes', '[{"tipo":"TAMANHO_TEXTO","minimo":null,"maximo":60}]'::jsonb,
                                       'pedirConfirmacao', FALSE, 'impedimento', NULL),
                                   alvo.posicao + 0.5) AS itens)),
                    '{grupos}', COALESCE((
                        SELECT jsonb_agg(
                                   CASE
                                       WHEN (g.grupo->>'ordem')::int > alvo.ordem
                                           THEN jsonb_set(g.grupo, '{ordem}', to_jsonb((g.grupo->>'ordem')::int + 1))
                                       ELSE g.grupo
                                   END ORDER BY g.posicao)
                        FROM jsonb_array_elements(m.conteudo->'grupos') WITH ORDINALITY AS g(grupo, posicao)), '[]'::jsonb))
                FROM alvo
                WHERE m.id = alvo.id;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000057"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000058"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000059"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000060"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000061"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000062"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000063"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000064"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000065"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000066"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000067"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000068"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000056"));

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000032"),
                columns: new[] { "dominio", "fonte_valores", "formato" },
                values: new object[] { "TEXTO", null, "LIVRE" });
        }
    }
}
