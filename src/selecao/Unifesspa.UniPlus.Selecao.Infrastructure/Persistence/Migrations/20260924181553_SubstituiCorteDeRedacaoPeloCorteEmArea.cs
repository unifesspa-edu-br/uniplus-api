using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SubstituiCorteDeRedacaoPeloCorteEmArea : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Substituição sob a mesma linha do rol_de_regras (ADR-0112, Emenda 1): legítima só
            // enquanto nenhuma versão de configuração congelada citar ELIM-CORTE-REDACAO. A busca
            // é pela tripla {codigo, versao, hash} da referência de regra, nunca por texto.
            //
            // O rascunho vivo que declara a regra antiga perde a declaração: a variante de args
            // dela deixa de existir, e a linha não seria mais lida. Sem produção, a Emenda 1
            // autoriza descartar o dado de rascunho; o operador declara de novo o corte com a área.
            migrationBuilder.Sql("""
                DO $adr0112$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM selecao.versoes_configuracao
                        WHERE configuracao_congelada @? '$.** ? (@.codigo == "ELIM-CORTE-REDACAO" && @.versao == "v1" && exists(@.hash))'
                    ) THEN
                        RAISE EXCEPTION 'rol_de_regras: ELIM-CORTE-REDACAO referenciada por versão de configuração congelada; substituir viola o append-only (ADR-0112)';
                    END IF;
                END
                $adr0112$;

                DELETE FROM selecao.regras_eliminacao
                WHERE regra_codigo = 'ELIM-CORTE-REDACAO' AND regra_versao = 'v1';
                """);

            migrationBuilder.UpdateData(
                schema: "selecao",
                table: "rol_de_regras",
                keyColumn: "id",
                keyValue: new Guid("d0a00000-0000-7000-8000-000000000006"),
                columns: new[] { "base_legal", "codigo", "esquema_args", "hash", "invariantes" },
                values: new object[] { "Res. 805/2024 art. 5º e Anexo I (ponto de corte por área do ENEM; Redação = 400)", "ELIM-CORTE-EM-AREA", "{\"area_codigo\":\"text\",\"minimo\":\"numeric\"}", "2b664ed9b782ce9e4b47c70063fd2c2f5bc225c74301fcc801600956d856ad4a", "[\"nota na área < mínimo → elimina\",\"no máximo um corte por área\"]" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // A volta desfaz a substituição só enquanto nada citar ELIM-CORTE-EM-AREA: nem versão
            // congelada nem rascunho vivo, porque a variante de args dela deixa de existir.
            migrationBuilder.Sql("""
                DO $adr0112$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM selecao.versoes_configuracao
                        WHERE configuracao_congelada @? '$.** ? (@.codigo == "ELIM-CORTE-EM-AREA" && @.versao == "v1" && exists(@.hash))'
                    ) OR EXISTS (
                        SELECT 1
                        FROM selecao.regras_eliminacao
                        WHERE regra_codigo = 'ELIM-CORTE-EM-AREA' AND regra_versao = 'v1'
                    ) THEN
                        RAISE EXCEPTION 'rol_de_regras: ELIM-CORTE-EM-AREA referenciada por versão de configuração congelada ou por rascunho vivo; desfazer a substituição viola o append-only (ADR-0112)';
                    END IF;
                END
                $adr0112$;
                """);

            migrationBuilder.UpdateData(
                schema: "selecao",
                table: "rol_de_regras",
                keyColumn: "id",
                keyValue: new Guid("d0a00000-0000-7000-8000-000000000006"),
                columns: new[] { "base_legal", "codigo", "esquema_args", "hash", "invariantes" },
                values: new object[] { "Res. 805/2024 Anexo I (corte de Redação = 400)", "ELIM-CORTE-REDACAO", "{\"minimo\":\"numeric\"}", "6a23db02c00878d5bb98a445e8dc72e209a95f2aa4a8bfd91de8fa08ee69c240", "[\"redação < mínimo → elimina\"]" });
        }
    }
}
