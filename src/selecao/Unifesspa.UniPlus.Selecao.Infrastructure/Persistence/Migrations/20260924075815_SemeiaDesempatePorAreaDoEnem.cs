using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SemeiaDesempatePorAreaDoEnem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "selecao",
                table: "rol_de_regras",
                columns: new[] { "id", "base_legal", "codigo", "created_at", "esquema_args", "hash", "invariantes", "tipo", "updated_at", "versao" },
                values: new object[] { new Guid("d0a00000-0000-7000-8000-000000000028"), "Edital (ordem de desempate por nota de área do ENEM)", "DESEMPATE-MAIOR-NOTA-AREA-ENEM", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{\"areas\":\"text[]\"}", "81fd6b28df7be85dcfdeb6301d849029d1ac7066cd661e2e60e17fe817b74843", "[\"ordena por maior nota na primeira área da lista; o empate que resta passa à área seguinte\",\"cada área é citada uma única vez, pelo código, e existe em todos os grupos do quadro de pesos por área congelado na classificação\",\"exige classificação baseada em ENEM, calculada pela média ponderada\"]", "criterio_desempate", null, "v1" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Fronteira append-only do rol_de_regras (ADR-0112): uma entrada referenciada por
            // configuração congelada é fato imutável, e a reversão só é legítima enquanto
            // nenhuma VersaoConfiguracao citar a entrada que este Down remove. A busca é pela
            // tripla {codigo, versao, hash} da referência de regra, nunca por texto: uma fase
            // batizada com o código da regra não é referência a ela.
            migrationBuilder.Sql("""
                DO $adr0112$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM selecao.versoes_configuracao
                        WHERE configuracao_congelada @? '$.** ? (@.codigo == "DESEMPATE-MAIOR-NOTA-AREA-ENEM" && @.versao == "v1" && exists(@.hash))'
                    ) THEN
                        RAISE EXCEPTION 'rol_de_regras: critério de desempate por área do ENEM referenciado por versão de configuração congelada; remover viola o append-only (ADR-0112)';
                    END IF;
                END
                $adr0112$;
                """);

            migrationBuilder.DeleteData(
                schema: "selecao",
                table: "rol_de_regras",
                keyColumn: "id",
                keyValue: new Guid("d0a00000-0000-7000-8000-000000000028"));
        }
    }
}
