using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SemeiaEliminacaoPorAusenciaNaEtapa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "selecao",
                table: "rol_de_regras",
                columns: new[] { "id", "base_legal", "codigo", "created_at", "esquema_args", "hash", "invariantes", "tipo", "updated_at", "versao" },
                values: new object[] { new Guid("d0a00000-0000-7000-8000-000000000032"), "Edital (eliminação do candidato que faltar à etapa)", "ELIM-AUSENCIA-NA-ETAPA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{\"etapa_ref\":\"text\"}", "679d25bb9ac2d0948473cb183f5b8063a5caf3955a903980d6f3ed2af750035c", "[\"falta à etapa referenciada → elimina\",\"não se aplica à etapa de nota do ENEM\"]", "regra_eliminacao", null, "v1" });
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
                        WHERE configuracao_congelada @? '$.** ? (@.codigo == "ELIM-AUSENCIA-NA-ETAPA" && @.versao == "v1" && exists(@.hash))'
                    ) OR EXISTS (
                        SELECT 1
                        FROM selecao.regras_eliminacao
                        WHERE regra_codigo = 'ELIM-AUSENCIA-NA-ETAPA' AND regra_versao = 'v1'
                    ) THEN
                        RAISE EXCEPTION 'rol_de_regras: eliminação por ausência na etapa referenciada por versão de configuração congelada ou por rascunho vivo; remover viola o append-only (ADR-0112)';
                    END IF;
                END
                $adr0112$;
                """);

            migrationBuilder.DeleteData(
                schema: "selecao",
                table: "rol_de_regras",
                keyColumn: "id",
                keyValue: new Guid("d0a00000-0000-7000-8000-000000000032"));
        }
    }
}
