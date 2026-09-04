using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UnificaRegrasDeDistribuicaoEModalidadesEmV1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Sem produção (LT, 03/09/2026): a reescrita muda a IDENTIDADE (codigo, versao) de
            // DISTRIB-VAGAS-LEI-12711 e DISTRIB-VAGAS-INSTITUCIONAL (v2 → v1) e o CÓDIGO da
            // regra que hoje é DISTRIB-VAGAS-EDU-CAMPO — não só o conteúdo. A emenda 1 da
            // ADR-0112 autoriza descartar todo processo cuja distribuição referencie qualquer
            // uma das cinco regras: o rascunho vivo (configuracoes_distribuicao_vagas) e/ou o
            // snapshot congelado (versoes_configuracao.configuracao_congelada).
            //
            // A cadeia atravessa quatro triggers append-only/condicionais nomeados (ADR-0063) e
            // três FKs RESTRICT que um DELETE cru não passa. Os triggers nomeados (somente-
            // inserção de versoes_configuracao e de ato_normativo, retenção condicional de
            // documentos_edital, somente-inserção de vinculo_ato_entidade e de
            // linhagem_unica_por_objeto) são desarmados só pela duração do DELETE que atravessam
            // e rearmados em seguida, na mesma transação — desabilitar um trigger NOMEADO exige
            // só ownership da tabela. As três FKs RESTRICT (autorreferente de nos_exigencia,
            // autorreferente de ato_normativo — cadeia de retificação, ato_retificado_id — e de
            // nos_exigencia para documentos_exigidos) nunca são desarmadas: "ALTER TABLE ...
            // DISABLE TRIGGER ALL" também desarmaria o gatilho INTERNO da constraint, e isso
            // exige superusuário — privilégio que o papel de aplicação (uniplus) não tem em HML
            // (provisionado NOSUPERUSER). Em vez disso, cada RESTRICT autorreferente é evitada
            // apagando em ORDEM TOPOLÓGICA — folhas antes de pais — dentro de um loop: um único
            // DELETE em lote NÃO basta, porque o Postgres verifica a RESTRICT linha a linha,
            // durante a varredura do próprio statement, não ao final dele — um pai do lote pode
            // ser fisicamente varrido antes do filho que ainda o referencia, mesmo os dois
            // estando no mesmo DELETE (empiricamente confirmado: a versão anterior desta
            // migration, com um DELETE único em ato_normativo, falhava com
            // "violates RESTRICT setting of foreign key constraint fk_ato_normativo_ato_retificado"
            // sempre que havia uma cadeia de retificação no lote). O loop remove, a cada
            // iteração, todo elemento do lote que não é mais referenciado por nenhum outro
            // elemento do MESMO lote — nunca deixa um pai sem seu filho já removido.
            //
            // Ordem: capturar os atos ANTES de apagar a versão que os referencia (senão perde-se
            // o rastro); a árvore de nos_exigencia do processo (loop topológico) antes de
            // documentos_exigidos, que ela referencia por RESTRICT — bancas_requeridas,
            // regras_recurso_fase e nos_exigencia_base_legal caem por cascade normal quando
            // fases_cronograma/nos_exigencia forem removidas, sem trigger nenhum desarmado nelas.
            // Depois, versoes_configuracao antes de processos_seletivos (FK Restrict); este
            // último cascateia para o resto da árvore filha (distribuição, cronograma, o que
            // sobrou de documentos_exigidos). Por fim os atos em publicacoes — primeiro a
            // linhagem (RESTRICT para o ato), depois vinculo_ato_entidade explicitamente (a
            // FK dela para ato_normativo é Cascade, mas o gatilho que a executaria vive no lado
            // referenciado — ato_normativo —, então sem apagar vinculo_ato_entidade antes ela
            // ficaria órfã quando ato_normativo sumir), por fim o próprio ato normativo (loop
            // topológico pela cadeia de retificação). Sem apagar os atos, eles ficariam órfãos:
            // VersaoConfiguracao.AtoCriadorId os referencia por valor, sem FK (ADR-0061), fora do
            // alcance de qualquer DELETE em selecao.
            migrationBuilder.Sql("""
                DO $limpeza_1421$
                DECLARE
                    processos_afetados uuid[];
                    atos_afetados uuid[];
                BEGIN
                    SELECT array_agg(DISTINCT p.id) INTO processos_afetados
                    FROM selecao.processos_seletivos p
                    WHERE EXISTS (
                        SELECT 1 FROM selecao.configuracoes_distribuicao_vagas c
                        WHERE c.processo_seletivo_id = p.id
                    ) OR EXISTS (
                        SELECT 1 FROM selecao.versoes_configuracao v
                        WHERE v.processo_seletivo_id = p.id
                          AND (
                               v.configuracao_congelada @? '$.** ? (@.codigo == "DISTRIB-VAGAS-LEI-12711" && exists(@.hash))'
                            OR v.configuracao_congelada @? '$.** ? (@.codigo == "DISTRIB-VAGAS-LEI-12711-COM-AC-PCD" && exists(@.hash))'
                            OR v.configuracao_congelada @? '$.** ? (@.codigo == "DISTRIB-VAGAS-INSTITUCIONAL" && exists(@.hash))'
                            OR v.configuracao_congelada @? '$.** ? (@.codigo == "DISTRIB-VAGAS-PSIQ" && exists(@.hash))'
                            OR v.configuracao_congelada @? '$.** ? (@.codigo == "DISTRIB-VAGAS-EDU-CAMPO" && exists(@.hash))'
                          )
                    );

                    IF processos_afetados IS NULL THEN
                        RETURN;
                    END IF;

                    SELECT array_agg(DISTINCT ato_criador_id) INTO atos_afetados
                    FROM selecao.versoes_configuracao
                    WHERE processo_seletivo_id = ANY(processos_afetados);

                    ALTER TABLE selecao.versoes_configuracao DISABLE TRIGGER trg_versoes_configuracao_somente_insercao;
                    ALTER TABLE selecao.documentos_edital DISABLE TRIGGER trg_documentos_edital_retencao_delete;

                    -- Remove a árvore de exigências do processo em ordem topológica (folhas
                    -- antes de pais), para nunca violar a FK RESTRICT autorreferente
                    -- (no_pai_id) — nos_exigencia_base_legal cai por cascade normal a cada nó
                    -- removido.
                    LOOP
                        DELETE FROM selecao.nos_exigencia n
                        WHERE n.processo_seletivo_id = ANY(processos_afetados)
                          AND NOT EXISTS (
                              SELECT 1 FROM selecao.nos_exigencia filho
                              WHERE filho.no_pai_id = n.id
                          );
                        EXIT WHEN NOT FOUND;
                    END LOOP;

                    -- documentos_exigidos.exigido_na_fase_id -> fases_cronograma é RESTRICT
                    -- (fases_cronograma é o lado referenciado); apagar documentos_exigidos aqui,
                    -- explicitamente, antes de fases_cronograma cair pelo cascade do processo,
                    -- evita a violação sem precisar desarmar nada em fases_cronograma —
                    -- bancas_requeridas e regras_recurso_fase caem por cascade normal quando
                    -- fases_cronograma sumir.
                    DELETE FROM selecao.documentos_exigidos WHERE processo_seletivo_id = ANY(processos_afetados);

                    DELETE FROM selecao.versoes_configuracao
                    WHERE processo_seletivo_id = ANY(processos_afetados);

                    DELETE FROM selecao.processos_seletivos
                    WHERE id = ANY(processos_afetados);

                    ALTER TABLE selecao.versoes_configuracao ENABLE TRIGGER trg_versoes_configuracao_somente_insercao;
                    ALTER TABLE selecao.documentos_edital ENABLE TRIGGER trg_documentos_edital_retencao_delete;

                    IF atos_afetados IS NOT NULL THEN
                        ALTER TABLE publicacoes.linhagem_unica_por_objeto DISABLE TRIGGER trg_linhagem_unica_somente_insercao;
                        DELETE FROM publicacoes.linhagem_unica_por_objeto
                        WHERE ato_id = ANY(atos_afetados) OR raiz_id = ANY(atos_afetados);
                        ALTER TABLE publicacoes.linhagem_unica_por_objeto ENABLE TRIGGER trg_linhagem_unica_somente_insercao;

                        ALTER TABLE publicacoes.ato_normativo DISABLE TRIGGER trg_ato_normativo_somente_insercao;
                        ALTER TABLE publicacoes.vinculo_ato_entidade DISABLE TRIGGER trg_vinculo_ato_entidade_somente_insercao;

                        DELETE FROM publicacoes.vinculo_ato_entidade WHERE ato_id = ANY(atos_afetados);

                        -- Remove a cadeia de retificação em ordem topológica (o ato mais
                        -- recente antes do que ele retifica), pelo mesmo motivo de
                        -- nos_exigencia acima — evita violar a FK RESTRICT autorreferente
                        -- (ato_retificado_id).
                        LOOP
                            DELETE FROM publicacoes.ato_normativo a
                            WHERE a.id = ANY(atos_afetados)
                              AND NOT EXISTS (
                                  SELECT 1 FROM publicacoes.ato_normativo filho
                                  WHERE filho.ato_retificado_id = a.id
                              );
                            EXIT WHEN NOT FOUND;
                        END LOOP;

                        ALTER TABLE publicacoes.vinculo_ato_entidade ENABLE TRIGGER trg_vinculo_ato_entidade_somente_insercao;
                        ALTER TABLE publicacoes.ato_normativo ENABLE TRIGGER trg_ato_normativo_somente_insercao;
                    END IF;
                END
                $limpeza_1421$;
                """);

            migrationBuilder.UpdateData(
                schema: "selecao",
                table: "rol_de_regras",
                keyColumn: "id",
                keyValue: new Guid("d0a00000-0000-7000-8000-000000000023"),
                columns: new[] { "esquema_args", "hash", "versao" },
                values: new object[] { "{\"pr_minimo\":\"numeric (piso 0,5 — art. 10 II; teto 1,0)\",\"modo_arredondamento\":\"teto (ceil) em todas as sub-reservas EXCETO LI_Q (floor) — art. 11\",\"ordem_garantia_minima\":[\"LB_PPI\",\"LB_Q\",\"LB_PCD\",\"LB_EP\",\"LI_PPI\",\"LI_PCD\",\"LI_EP\"],\"sub_reservas\":[\"PPI\",\"Q\",\"PCD\",\"EP\"],\"entradas_por_edital\":[\"VO_base\",\"PR\",\"ReferenciaReservaDemografica\"],\"modalidades_admitidas\":[\"AC\",\"LB_PPI\",\"LB_Q\",\"LB_PCD\",\"LB_EP\",\"LI_PPI\",\"LI_Q\",\"LI_PCD\",\"LI_EP\"]}", "369e630525f26995a637d305851b4dfb0a713b8de8038a5874846ac512d7d375", "v1" });

            migrationBuilder.UpdateData(
                schema: "selecao",
                table: "rol_de_regras",
                keyColumn: "id",
                keyValue: new Guid("d0a00000-0000-7000-8000-000000000024"),
                columns: new[] { "hash", "versao" },
                values: new object[] { "a9f0f1cb005bf1fa891094b1271df22c0bf95605658659cd8a3f37f3e157e1dc", "v1" });

            migrationBuilder.UpdateData(
                schema: "selecao",
                table: "rol_de_regras",
                keyColumn: "id",
                keyValue: new Guid("d0a00000-0000-7000-8000-000000000026"),
                columns: new[] { "codigo", "esquema_args", "hash" },
                values: new object[] { "DISTRIB-VAGAS-COM-PCD-PURO", "{\"quadro_fixo_por_modalidade\":\"objeto {codigo: quantidade} fixado por edital (NÃO art. 10)\",\"aplicacao\":\"quadro fixo sem as cotas federais — PCD_PURO como reserva de qualquer processo fora do regime federal\",\"modalidades_admitidas\":[\"AC\",\"PCD_PURO\"]}", "18dbc3ea7b3ade62c4edcff134124d5c1dd4535367c8f33678176cd7749510ce" });

            migrationBuilder.InsertData(
                schema: "selecao",
                table: "rol_de_regras",
                columns: new[] { "id", "base_legal", "codigo", "created_at", "esquema_args", "hash", "invariantes", "tipo", "updated_at", "versao" },
                values: new object[] { new Guid("d0a00000-0000-7000-8000-000000000027"), "Portaria Normativa MEC nº 18/2012 art. 10 e 11 (red. PN 2.027/2023) — distribuição e arredondamento das vagas reservadas; Lei 12.711/2012 (red. Lei 14.723/2023)", "DISTRIB-VAGAS-LEI-12711-COM-AC-PCD", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{\"pr_minimo\":\"numeric (piso 0,5 — art. 10 II; teto 1,0)\",\"modo_arredondamento\":\"teto (ceil) em todas as sub-reservas EXCETO LI_Q (floor) — art. 11\",\"ordem_garantia_minima\":[\"LB_PPI\",\"LB_Q\",\"LB_PCD\",\"LB_EP\",\"LI_PPI\",\"LI_PCD\",\"LI_EP\"],\"sub_reservas\":[\"PPI\",\"Q\",\"PCD\",\"EP\"],\"entradas_por_edital\":[\"VO_base\",\"PR\",\"ReferenciaReservaDemografica\"],\"modalidades_admitidas\":[\"AC\",\"LB_PPI\",\"LB_Q\",\"LB_PCD\",\"LB_EP\",\"LI_PPI\",\"LI_Q\",\"LI_PCD\",\"LI_EP\",\"AC_PCD\"]}", "a0097d270c2fcd478ff8fb41a419275b328d0e13b6124dc179b6d224b83c8d08", "[\"VR=ceil(VO×PR)\",\"VRRI=ceil(VR×0,5)\",\"VRSI=VR−VRRI\",\"sub-reservas ceil EXCETO LI_Q=floor (art. 11)\",\"garantia mín-1 ordenada I-VII condicional à disponibilidade (art. 10 §2º), LI_Q fora\",\"INV-3a: LB_EP≥0 e LI_EP≥0\",\"INV-3b: AC≥0\",\"INV-3c: VR_final+RETIRADAS+AC=VO_base\",\"AC_PCD retira de AC, como qualquer outra retirada federal\",\"modalidade fora de modalidades_admitidas é recusada\"]", "regra_distribuicao_vagas", null, "v1" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // O Down reverte o catálogo (rol_de_regras) para a forma anterior — não os
            // processos apagados no Up: a exclusão em cascade não deixa rastro para
            // reconstrução. Aceitável apenas porque não há produção (LT, 03/09/2026).
            migrationBuilder.DeleteData(
                schema: "selecao",
                table: "rol_de_regras",
                keyColumn: "id",
                keyValue: new Guid("d0a00000-0000-7000-8000-000000000027"));

            migrationBuilder.UpdateData(
                schema: "selecao",
                table: "rol_de_regras",
                keyColumn: "id",
                keyValue: new Guid("d0a00000-0000-7000-8000-000000000023"),
                columns: new[] { "esquema_args", "hash", "versao" },
                values: new object[] { "{\"pr_minimo\":\"numeric (piso 0,5 — art. 10 II; teto 1,0)\",\"modo_arredondamento\":\"teto (ceil) em todas as sub-reservas EXCETO LI_Q (floor) — art. 11\",\"ordem_garantia_minima\":[\"LB_PPI\",\"LB_Q\",\"LB_PCD\",\"LB_EP\",\"LI_PPI\",\"LI_PCD\",\"LI_EP\"],\"sub_reservas\":[\"PPI\",\"Q\",\"PCD\",\"EP\"],\"entradas_por_edital\":[\"VO_base\",\"PR\",\"ReferenciaReservaDemografica\"],\"modalidades_admitidas\":[\"AC\",\"LB_PPI\",\"LB_Q\",\"LB_PCD\",\"LB_EP\",\"LI_PPI\",\"LI_Q\",\"LI_PCD\",\"LI_EP\",\"AC_PCD\"]}", "0951eef80fb6fd6af566751547a7566a152dfcc18d4053c5060df10c1d73a88b", "v2" });

            migrationBuilder.UpdateData(
                schema: "selecao",
                table: "rol_de_regras",
                keyColumn: "id",
                keyValue: new Guid("d0a00000-0000-7000-8000-000000000024"),
                columns: new[] { "hash", "versao" },
                values: new object[] { "faa74ff68dcf4d38e22690a873bf84b2525fe59696b329af7460860d6c3ca409", "v2" });

            migrationBuilder.UpdateData(
                schema: "selecao",
                table: "rol_de_regras",
                keyColumn: "id",
                keyValue: new Guid("d0a00000-0000-7000-8000-000000000026"),
                columns: new[] { "codigo", "esquema_args", "hash" },
                values: new object[] { "DISTRIB-VAGAS-EDU-CAMPO", "{\"quadro_fixo_por_modalidade\":\"objeto {codigo: quantidade} fixado por edital (NÃO art. 10)\",\"aplicacao\":\"PSE Educação do Campo\",\"modalidades_admitidas\":[\"AC\",\"PCD_PURO\"]}", "bf890f415c8e5e58a3c64ca45bf32f958c8fa4d1730f2c0155fb7c6fe6581c42" });
        }
    }
}
