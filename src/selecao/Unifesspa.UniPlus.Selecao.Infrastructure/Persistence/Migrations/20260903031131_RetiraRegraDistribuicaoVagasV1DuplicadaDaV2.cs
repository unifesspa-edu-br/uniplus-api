using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RetiraRegraDistribuicaoVagasV1DuplicadaDaV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Fronteira append-only do rol_de_regras (ADR-0112): a v1 de cada
            // regra é retirada porque a v2 (SeedId 23/24, PR #1389) é superset —
            // mesmo conteúdo mais modalidades_admitidas — e a v1 nunca precisou
            // conviver ao lado dela para preservar configuração congelada
            // nenhuma. A retirada só é legítima enquanto nenhuma
            // VersaoConfiguracao referenciar a v1; a busca é ESTRUTURAL (tripla
            // {codigo, versao, hash}), não textual, pelo mesmo motivo do Down de
            // AddAlgoritmosContagemPrazo — outros objetos do snapshot têm a
            // chave bare `codigo` com valor declarado pelo administrador.
            //
            // A guarda também cobre configuracoes_distribuicao_vagas — o
            // RASCUNHO vivo, não só o snapshot congelado. ReferenciaRegra é
            // cópia por valor sem FK (ADR-0061): um rascunho gravado sob v1
            // ANTES desta migration carrega codigo/versao próprios, e a
            // publicação (SnapshotPublicacaoCanonicalizer) serializa esse
            // estado já persistido sem reconsultar o catálogo. Sem esta
            // segunda checagem, um rascunho pré-existente publicaria DEPOIS da
            // remoção e congelaria uma referência a uma linha que já não
            // existe — pior que a duplicidade que esta migration corrige.
            migrationBuilder.Sql("""
                DO $adr0112$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM selecao.versoes_configuracao
                        WHERE configuracao_congelada @? '$.** ? (@.codigo == "DISTRIB-VAGAS-LEI-12711" && @.versao == "v1" && exists(@.hash))'
                           OR configuracao_congelada @? '$.** ? (@.codigo == "DISTRIB-VAGAS-INSTITUCIONAL" && @.versao == "v1" && exists(@.hash))'
                    ) OR EXISTS (
                        SELECT 1
                        FROM selecao.configuracoes_distribuicao_vagas
                        WHERE (regra_distribuicao_codigo = 'DISTRIB-VAGAS-LEI-12711' AND regra_distribuicao_versao = 'v1')
                           OR (regra_distribuicao_codigo = 'DISTRIB-VAGAS-INSTITUCIONAL' AND regra_distribuicao_versao = 'v1')
                    ) THEN
                        RAISE EXCEPTION 'rol_de_regras: v1 de DISTRIB-VAGAS-LEI-12711/INSTITUCIONAL referenciada por versão de configuração congelada ou por rascunho vivo; remover viola o append-only (ADR-0112)';
                    END IF;
                END
                $adr0112$;
                """);

            migrationBuilder.DeleteData(
                schema: "selecao",
                table: "rol_de_regras",
                keyColumn: "id",
                keyValue: new Guid("d0a00000-0000-7000-8000-000000000015"));

            migrationBuilder.DeleteData(
                schema: "selecao",
                table: "rol_de_regras",
                keyColumn: "id",
                keyValue: new Guid("d0a00000-0000-7000-8000-000000000016"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "selecao",
                table: "rol_de_regras",
                columns: new[] { "id", "base_legal", "codigo", "created_at", "esquema_args", "hash", "invariantes", "tipo", "updated_at", "versao" },
                values: new object[,]
                {
                    { new Guid("d0a00000-0000-7000-8000-000000000015"), "Portaria Normativa MEC nº 18/2012 art. 10 e 11 (red. PN 2.027/2023) — distribuição e arredondamento das vagas reservadas; Lei 12.711/2012 (red. Lei 14.723/2023)", "DISTRIB-VAGAS-LEI-12711", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{\"pr_minimo\":\"numeric (piso 0,5 — art. 10 II; teto 1,0)\",\"modo_arredondamento\":\"teto (ceil) em todas as sub-reservas EXCETO LI_Q (floor) — art. 11\",\"ordem_garantia_minima\":[\"LB_PPI\",\"LB_Q\",\"LB_PCD\",\"LB_EP\",\"LI_PPI\",\"LI_PCD\",\"LI_EP\"],\"sub_reservas\":[\"PPI\",\"Q\",\"PCD\",\"EP\"],\"entradas_por_edital\":[\"VO_base\",\"PR\",\"ReferenciaReservaDemografica\"]}", "0eb12ca67af16ab666e0db0894d795ec725422326cf7dedba2e804f496e0d807", "[\"VR=ceil(VO×PR)\",\"VRRI=ceil(VR×0,5)\",\"VRSI=VR−VRRI\",\"sub-reservas ceil EXCETO LI_Q=floor (art. 11)\",\"garantia mín-1 ordenada I-VII condicional à disponibilidade (art. 10 §2º), LI_Q fora\",\"INV-3a: LB_EP≥0 e LI_EP≥0\",\"INV-3b: AC≥0\",\"INV-3c: VR_final+RETIRADAS+AC=VO_base\"]", "regra_distribuicao_vagas", null, "v1" },
                    { new Guid("d0a00000-0000-7000-8000-000000000016"), "Res. Unifesspa 532/2021 (vagas PcD/Indígena/Quilombola); Portaria MEC 18/2012 art. 12 (reservas suplementares e outras ações afirmativas)", "DISTRIB-VAGAS-INSTITUCIONAL", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{\"quadro_fixo_por_modalidade\":\"objeto {codigo: quantidade} fixado por edital (NÃO art. 10)\",\"aplicacao\":\"PSIQ (IND/QUIL) e PSE Ed. Campo — quadro institucional\"}", "03b114eb3b559367b7d79f9edb1371f8164c5ede0c5f4b21809ee572c49c9451", "[\"quadro fixo por edital (não recalculado pelo art. 10)\",\"modalidades institucionais somam conforme composicao_vagas (SUPLEMENTAR_AO_TOTAL ou RETIRA_DE)\"]", "regra_distribuicao_vagas", null, "v1" }
                });
        }
    }
}
