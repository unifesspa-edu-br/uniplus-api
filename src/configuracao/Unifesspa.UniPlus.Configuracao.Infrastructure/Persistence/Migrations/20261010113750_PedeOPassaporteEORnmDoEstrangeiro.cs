using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PedeOPassaporteEORnmDoEstrangeiro : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Nos modelos de inscrição, os dois itens do documento do estrangeiro ocupam, na seção dos
            // dados básicos, a ordem e a exibição do passaporte e do RNM, que os substituem.
            migrationBuilder.Sql("""
                UPDATE configuracao.modelos_formulario
                SET conteudo = jsonb_set(conteudo, '{itens}', (
                    SELECT jsonb_agg(
                        CASE
                            WHEN item->>'etapaCodigo' = 'DADOS_BASICOS' AND item->>'fatoCodigo' = 'DOCUMENTO_ESTRANGEIRO_TIPO'
                                THEN item || '{"fatoCodigo":"PASSAPORTE_NUMERO","rotulo":"Número do passaporte","tipoRenderizacao":"TEXTO","formato":"LIVRE"}'::jsonb
                            WHEN item->>'etapaCodigo' = 'DADOS_BASICOS' AND item->>'fatoCodigo' = 'DOCUMENTO_ESTRANGEIRO_NUMERO'
                                THEN item || '{"fatoCodigo":"RNM_NUMERO","rotulo":"Número do Registro Nacional Migratório (RNM)"}'::jsonb
                            ELSE item
                        END ORDER BY posicao)
                    FROM jsonb_array_elements(conteudo->'itens') WITH ORDINALITY AS itens(item, posicao)))
                WHERE conteudo->'itens' @> '[{"etapaCodigo":"DADOS_BASICOS","fatoCodigo":"DOCUMENTO_ESTRANGEIRO_TIPO"}]'
                   OR conteudo->'itens' @> '[{"etapaCodigo":"DADOS_BASICOS","fatoCodigo":"DOCUMENTO_ESTRANGEIRO_NUMERO"}]';
                """);

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000025"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000026"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000035"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000034"));

            migrationBuilder.InsertData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                columns: new[] { "id", "ativo", "binding", "cardinalidade", "classificacao_protecao", "codigo", "created_at", "created_by", "dependencias", "descricao", "dominio", "escopo", "finalidade_tratamento", "fonte_valores", "formato", "hipotese_legal", "nome", "origem", "ponto_resolucao", "regras_padrao", "sistema", "updated_at", "updated_by" },
                values: new object[,]
                {
                    { new Guid("fa700000-0000-7000-8000-000000000051"), true, "CAMPO_FORMULARIO:PASSAPORTE_NUMERO", "ESCALAR", "IDENTIFICADOR", "PASSAPORTE_NUMERO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, Array.Empty<string>(), null, "TEXTO", "CANDIDATO", "Identificação do candidato no processo seletivo.", null, "LIVRE", "CUMPRIMENTO_OBRIGACAO_LEGAL", "Número do passaporte", "DECLARADO", "INSCRICAO", "[]", true, null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000052"), true, "CAMPO_FORMULARIO:RNM_NUMERO", "ESCALAR", "IDENTIFICADOR", "RNM_NUMERO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, Array.Empty<string>(), null, "TEXTO", "CANDIDATO", "Identificação do candidato no processo seletivo.", null, "LIVRE", "CUMPRIMENTO_OBRIGACAO_LEGAL", "Número do Registro Nacional Migratório (RNM)", "DECLARADO", "INSCRICAO", "[]", true, null, null }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000051"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000052"));

            migrationBuilder.InsertData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                columns: new[] { "id", "ativo", "binding", "cardinalidade", "classificacao_protecao", "codigo", "created_at", "created_by", "dependencias", "descricao", "dominio", "escopo", "finalidade_tratamento", "fonte_valores", "formato", "hipotese_legal", "nome", "origem", "ponto_resolucao", "regras_padrao", "sistema", "updated_at", "updated_by" },
                values: new object[,]
                {
                    { new Guid("fa700000-0000-7000-8000-000000000034"), true, "CAMPO_FORMULARIO:DOCUMENTO_ESTRANGEIRO_TIPO", "ESCALAR", "PESSOAL", "DOCUMENTO_ESTRANGEIRO_TIPO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, Array.Empty<string>(), null, "CATEGORICO", "CANDIDATO", "Identificação do candidato no processo seletivo.", "GLOBAL", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Documento de identificação do estrangeiro", "DECLARADO", "INSCRICAO", "[]", true, null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000035"), true, "CAMPO_FORMULARIO:DOCUMENTO_ESTRANGEIRO_NUMERO", "ESCALAR", "IDENTIFICADOR", "DOCUMENTO_ESTRANGEIRO_NUMERO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, Array.Empty<string>(), null, "TEXTO", "CANDIDATO", "Identificação do candidato no processo seletivo.", null, "LIVRE", "CUMPRIMENTO_OBRIGACAO_LEGAL", "Número do documento do estrangeiro", "DECLARADO", "INSCRICAO", "[]", true, null, null }
                });

            migrationBuilder.InsertData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                columns: new[] { "id", "ativo", "codigo", "created_at", "descricao", "fato_candidato_id", "ordem", "orientacao", "updated_at" },
                values: new object[,]
                {
                    { new Guid("fa70d000-0000-7000-8000-000000000025"), true, "PASSAPORTE", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Passaporte", new Guid("fa700000-0000-7000-8000-000000000034"), 0, null, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000026"), true, "RNM", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Registro Nacional Migratório (RNM)", new Guid("fa700000-0000-7000-8000-000000000034"), 1, null, null }
                });
        }
    }
}
