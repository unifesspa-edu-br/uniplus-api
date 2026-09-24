using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SemeiaEliminacaoPorFaltaEmDiaDeProvaEnem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<bool>(
                name: "baseado_em_enem",
                schema: "selecao",
                table: "configuracoes_classificacao",
                type: "boolean",
                nullable: false,
                comment: "A classificação usa a estrutura de pontuação por área do ENEM — sinal explícito (Story #850) do qual as regras de eliminação do ENEM dependem, substituindo a ramificação por TipoProcesso.",
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldComment: "A classificação usa a estrutura de pontuação por área do ENEM — sinal explícito (Story #850) do qual ELIM-CORTE-REDACAO/ELIM-ZERO-EM-AREA dependem, substituindo a ramificação por TipoProcesso.");

            migrationBuilder.InsertData(
                schema: "selecao",
                table: "rol_de_regras",
                columns: new[] { "id", "base_legal", "codigo", "created_at", "esquema_args", "hash", "invariantes", "tipo", "updated_at", "versao" },
                values: new object[] { new Guid("d0a00000-0000-7000-8000-000000000029"), "Edital (falta em dia de prova do ENEM)", "ELIM-FALTA-EM-DIA-DE-PROVA-ENEM", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{}", "72c9dc358aaa3e81e2ccb08bc6d9ec3c0842c0a1aec5c001e2526c518bb3f3aa", "[\"falta em pelo menos um dia de prova da edição do ENEM usada no processo → elimina\",\"sem participação registrada na edição do ENEM indicada pelo candidato equivale a falta\"]", "regra_eliminacao", null, "v1" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Fronteira append-only do rol_de_regras (ADR-0112): uma entrada referenciada por
            // configuração congelada é fato imutável, e a reversão só é legítima enquanto
            // nenhuma VersaoConfiguracao citar a entrada que este Down remove. A busca é pela
            // tripla {codigo, versao, hash} da referência de regra, nunca por texto.
            //
            // A guarda também cobre o rascunho vivo em regras_eliminacao: a referência de regra
            // é cópia por valor sem FK (ADR-0061), e a publicação serializa o rascunho sem
            // reconsultar o catálogo, então ele congelaria depois uma entrada já removida.
            migrationBuilder.Sql("""
                DO $adr0112$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM selecao.versoes_configuracao
                        WHERE configuracao_congelada @? '$.** ? (@.codigo == "ELIM-FALTA-EM-DIA-DE-PROVA-ENEM" && @.versao == "v1" && exists(@.hash))'
                    ) OR EXISTS (
                        SELECT 1
                        FROM selecao.regras_eliminacao
                        WHERE regra_codigo = 'ELIM-FALTA-EM-DIA-DE-PROVA-ENEM' AND regra_versao = 'v1'
                    ) THEN
                        RAISE EXCEPTION 'rol_de_regras: eliminação por falta em dia de prova do ENEM referenciada por versão de configuração congelada ou por rascunho vivo; remover viola o append-only (ADR-0112)';
                    END IF;
                END
                $adr0112$;
                """);

            migrationBuilder.DeleteData(
                schema: "selecao",
                table: "rol_de_regras",
                keyColumn: "id",
                keyValue: new Guid("d0a00000-0000-7000-8000-000000000029"));

            migrationBuilder.AlterColumn<bool>(
                name: "baseado_em_enem",
                schema: "selecao",
                table: "configuracoes_classificacao",
                type: "boolean",
                nullable: false,
                comment: "A classificação usa a estrutura de pontuação por área do ENEM — sinal explícito (Story #850) do qual ELIM-CORTE-REDACAO/ELIM-ZERO-EM-AREA dependem, substituindo a ramificação por TipoProcesso.",
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldComment: "A classificação usa a estrutura de pontuação por área do ENEM — sinal explícito (Story #850) do qual as regras de eliminação do ENEM dependem, substituindo a ramificação por TipoProcesso.");
        }
    }
}
