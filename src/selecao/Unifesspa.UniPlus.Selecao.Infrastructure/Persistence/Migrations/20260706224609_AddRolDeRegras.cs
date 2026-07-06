using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRolDeRegras : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "rol_de_regras",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    versao = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    tipo = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    esquema_args = table.Column<string>(type: "jsonb", nullable: false),
                    invariantes = table.Column<string>(type: "jsonb", nullable: false),
                    base_legal = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rol_de_regras", x => x.id);
                });

            migrationBuilder.InsertData(
                schema: "selecao",
                table: "rol_de_regras",
                columns: new[] { "id", "base_legal", "codigo", "created_at", "esquema_args", "hash", "invariantes", "tipo", "updated_at", "versao" },
                values: new object[,]
                {
                    { new Guid("d0a00000-0000-7000-8000-000000000001"), "Proposta CEPS + #56; média ponderada NOTA=Σ(nota×peso)/Σpeso", "FORMULA-MEDIA-PONDERADA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{\"fonte_pesos\":[\"etapa\",\"peso_area_enem\"]}", "a6fdc29bce533ef3cf8acb6cfdd7d67a99104541f6201d227f0d1c7051229ad6", "[\"divisor = Σ(peso das etapas classificatória∪ambas)\"]", "regra_calculo", null, "v1" },
                    { new Guid("d0a00000-0000-7000-8000-000000000002"), "Portaria MEC 18/2012 art. 16 — SiSU federal (CEPS não calcula)", "CLASSIFICACAO-IMPORTADA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{}", "8e6f1f7b705c601991bf0617bbf881e39a3666c641abdfa4c1bd9ce2ecbcc85c", "[\"sem cálculo local; classificação federal por importação do listão\"]", "regra_calculo", null, "v1" },
                    { new Guid("d0a00000-0000-7000-8000-000000000003"), "Decisão CEPS/PO (gaps 1.1) — truncamento 2 casas (default)", "PRECISAO-TRUNCAR", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{\"casas\":\"integer\"}", "66cbb932e61982f09fdd1d60a0db8411ea02dedc19bb72d1af23004f1e18360b", "[\"trunca na N-ésima casa, sem arredondar\"]", "regra_arredondamento", null, "v1" },
                    { new Guid("d0a00000-0000-7000-8000-000000000004"), "Reprodução de editais antigos (PSE/Convênios)", "PRECISAO-ARREDONDAR-CIMA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{\"casas\":\"integer\"}", "efc491bf6a242a870c7bf9969048c198db88116ba39e72a44bcf1d5d5aebc4fb", "[\"arredonda p/ cima se 3ª casa ≥ 5\"]", "regra_arredondamento", null, "v1" },
                    { new Guid("d0a00000-0000-7000-8000-000000000005"), "Edital por processo (nota mínima eliminatória)", "ELIM-NOTA-MINIMA-ETAPA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{\"etapa\":\"text\",\"nota_minima\":\"numeric\"}", "b64f643eba9744efc20bf19221bde85c645da32262a7d2ef616f18bd7c2ed5ac", "[\"nota < mínima na etapa → elimina\"]", "regra_eliminacao", null, "v1" },
                    { new Guid("d0a00000-0000-7000-8000-000000000006"), "Res. 805/2024 Anexo I (corte de Redação = 400)", "ELIM-CORTE-REDACAO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{\"minimo\":\"numeric\"}", "6a23db02c00878d5bb98a445e8dc72e209a95f2aa4a8bfd91de8fa08ee69c240", "[\"redação < mínimo → elimina\"]", "regra_eliminacao", null, "v1" },
                    { new Guid("d0a00000-0000-7000-8000-000000000007"), "Res. 805/2024 art. 5º (zero em qualquer área elimina)", "ELIM-ZERO-EM-AREA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{}", "75f4929b848138c8ed939a182e3664451ce38cd40a136adda438b62e1b6e3fb8", "[\"nota zero em qualquer área do ENEM → elimina\"]", "regra_eliminacao", null, "v1" },
                    { new Guid("d0a00000-0000-7000-8000-000000000008"), "RN05 + decisão PO Jairo (×1,20 sem teto, após pesos)", "BONUS-MULTIPLICATIVO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{\"fator\":\"numeric\",\"teto\":\"numeric|null\"}", "d824fa64884e038e132f918dfd6efecf56d1df4cd4a1a1f86a78bf31718dae9b", "[\"nota_final × fator, após os pesos; teto opcional\"]", "regra_bonus", null, "v1" },
                    { new Guid("d0a00000-0000-7000-8000-000000000009"), "Lei 10.741/2003 art. 27 (Estatuto do Idoso)", "DESEMPATE-IDOSO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{\"idade_minima\":\"integer\"}", "6738b1a9ca4f063f7f215cabb837e055d8abafaf0835f0b3deeed1c97f0becd4", "[\"prioriza quem satisfaz FAIXA_ETARIA ≥ idade_minima\"]", "criterio_desempate", null, "v1" },
                    { new Guid("d0a00000-0000-7000-8000-000000000010"), "Edital (ordem de desempate)", "DESEMPATE-MAIOR-NOTA-ETAPA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{\"etapa\":\"text\"}", "1a988e6681b2970dc6c568c7372ceaf2f6e5ec0f7061ac47f656e1366a9ecad8", "[\"ordena por maior nota da etapa indicada\"]", "criterio_desempate", null, "v1" },
                    { new Guid("d0a00000-0000-7000-8000-000000000011"), "Edital (maior idade cronológica)", "DESEMPATE-MAIOR-IDADE", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{}", "1efa26eaeffc88baf31ce9e2030c05c9976fae125e9845fe0283168050dc1237", "[\"ordena por data de nascimento (nascido mais cedo vence)\"]", "criterio_desempate", null, "v1" },
                    { new Guid("d0a00000-0000-7000-8000-000000000012"), "Edital (critério adicional via fato do candidato — ex.: professor rural)", "DESEMPATE-PREDICADO-FATO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{\"fato\":\"text\",\"operador\":\"text\",\"valor\":\"any\"}", "d832d910826f25b6b50fd324f2f3cae472440c0e81e082be7c8d4fefe3de3f21", "[\"prioriza quem satisfaz predicado sobre FatoCandidato; fato deve estar no vocabulário e ser coletado\"]", "criterio_desempate", null, "v1" },
                    { new Guid("d0a00000-0000-7000-8000-000000000013"), "RN04 (processamento de 1ª/2ª opção)", "ALOCACAO-OPCOES-RN04", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{\"n_opcoes\":\"integer\"}", "2bb69f0e34483e635aa0903f8d3ba19a4255e8f542c5f7090ac75cecf200c988", "[\"1ª opção → 2ª opção → remanejamento → lista de espera\"]", "regra_ordem_alocacao", null, "v1" },
                    { new Guid("d0a00000-0000-7000-8000-000000000014"), "Lei 12.711/2012 art. 1º parágrafo único (red. Lei 14.723/2023) — renda familiar bruta per capita ≤ 1 SM (ensino superior); PN MEC 18/2012 art. 6º-7º — apuração da renda mensal per capita + exclusões obrigatórias", "RENDA-PER-CAPITA-LEI-12711", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{\"limite_sm\":\"numeric\",\"sm_referencia\":\"numeric (SM congelado na data de início das inscrições)\",\"periodo_apuracao_meses\":\"integer (ex.: 3 últimos meses)\",\"criterio_media_mensal\":\"text (média mensal dos rendimentos brutos)\",\"exclusoes_renda\":\"lista (PN 18/2012 art. 7º)\",\"composicao_nucleo_familiar\":\"quem compõe o núcleo familiar\"}", "5a1ad80627e354c03e4d6ef776a45db695a1203cea574a288dbcdf706ca58899", "[\"média mensal da renda familiar bruta (últimos N meses, após exclusões) ÷ nº de membros do núcleo ≤ limite_sm × sm_referencia\"]", "regra_elegibilidade", null, "v1" },
                    { new Guid("d0a00000-0000-7000-8000-000000000015"), "Portaria Normativa MEC nº 18/2012 art. 10 e 11 (red. PN 2.027/2023) — distribuição e arredondamento das vagas reservadas; Lei 12.711/2012 (red. Lei 14.723/2023)", "DISTRIB-VAGAS-LEI-12711", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{\"pr_minimo\":\"numeric (piso 0,5 — art. 10 II; teto 1,0)\",\"modo_arredondamento\":\"teto (ceil) em todas as sub-reservas EXCETO LI_Q (floor) — art. 11\",\"ordem_garantia_minima\":[\"LB_PPI\",\"LB_Q\",\"LB_PCD\",\"LB_EP\",\"LI_PPI\",\"LI_PCD\",\"LI_EP\"],\"sub_reservas\":[\"PPI\",\"Q\",\"PCD\",\"EP\"],\"entradas_por_edital\":[\"VO_base\",\"PR\",\"ReferenciaReservaDemografica\"]}", "0eb12ca67af16ab666e0db0894d795ec725422326cf7dedba2e804f496e0d807", "[\"VR=ceil(VO×PR)\",\"VRRI=ceil(VR×0,5)\",\"VRSI=VR−VRRI\",\"sub-reservas ceil EXCETO LI_Q=floor (art. 11)\",\"garantia mín-1 ordenada I-VII condicional à disponibilidade (art. 10 §2º), LI_Q fora\",\"INV-3a: LB_EP≥0 e LI_EP≥0\",\"INV-3b: AC≥0\",\"INV-3c: VR_final+RETIRADAS+AC=VO_base\"]", "regra_distribuicao_vagas", null, "v1" },
                    { new Guid("d0a00000-0000-7000-8000-000000000016"), "Res. Unifesspa 532/2021 (vagas PcD/Indígena/Quilombola); Portaria MEC 18/2012 art. 12 (reservas suplementares e outras ações afirmativas)", "DISTRIB-VAGAS-INSTITUCIONAL", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{\"quadro_fixo_por_modalidade\":\"objeto {codigo: quantidade} fixado por edital (NÃO art. 10)\",\"aplicacao\":\"PSIQ (IND/QUIL) e PSE Ed. Campo — quadro institucional\"}", "03b114eb3b559367b7d79f9edb1371f8164c5ede0c5f4b21809ee572c49c9451", "[\"quadro fixo por edital (não recalculado pelo art. 10)\",\"modalidades institucionais somam conforme composicao_vagas (SUPLEMENTAR_AO_TOTAL ou RETIRA_DE)\"]", "regra_distribuicao_vagas", null, "v1" },
                    { new Guid("d0a00000-0000-7000-8000-000000000017"), "Portaria MEC 18/2012 art. 11, parágrafo único (red. PN 2.027/2023) — cap nas vagas da oferta + prioridade do inciso III (LB) sobre o inciso IV (LI); art. 10 II (mínimo 50%, piso)", "RECONCILIACAO-VAGAS-ART11-PU", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{\"reconciliacao_federal\":\"CAP_VO + PRIORIDADE_LB (art. 11 §único, embutida em DISTRIB-VAGAS-LEI-12711)\",\"motores_nao_art10\":\"REDUZIR_DE | REDUZIR_PROPORCIONAL_EM (apenas distribuições institucionais; clamp >=0; VEDADOS à Lei 12.711)\"}", "ad2d8012ddc1f2ea4d763034899d07590c4a49901744b852dca2e70cede8b1e9", "[\"cap em VO: a reserva nunca excede as vagas da oferta (art. 11 §único I)\",\"prioridade LB>LI: na escassez a LI cede primeiro (art. 11 §único II)\",\"estouro sobre o nominal 50% (VR) e LEGAL — piso (art. 10 II), absorvido pela AC\",\"curso pequeno e determinístico por lei (capa, não bloqueia)\"]", "regra_ajuste_distribuicao_vagas", null, "v1" },
                    { new Guid("d0a00000-0000-7000-8000-000000000018"), "Lei 9.784/1999 (processo administrativo federal); edital/processo (prazo configurável por edital); regimento CONSEPE (2ª instância)", "RECURSO-MULTI-INSTANCIA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{\"prazo_valor\":\"numeric\",\"prazo_unidade\":\"HORAS|DIAS|DIAS_UTEIS (sem default — dado do edital)\",\"contra_ato\":\"codigo da fase produtora recorrível (NUNCA resultado definitivo)\",\"marco_inicial\":\"a partir de quando conta (ex.: publicacao_resultado)\",\"instancias\":\"lista ordenada [{instancia,dono,prazo_valor,prazo_unidade}] — 1ª CEPS, 2ª CONSEPE\"}", "660cf3fffe22069f5a7f302a98b1e44b96d2e992680f02304935a36548f95490", "[\"interposição GATED pela janela aberta (sem recurso a qualquer momento)\",\"sem recurso ao resultado definitivo (≠ recurso de habilitação CRCA)\",\"2ª instância (CONSEPE) só após indeferimento da 1ª; CONSEPE emite parecer externo, CEPS anexa e aplica (vinculante)\",\"processo PARALISA enquanto 2ª instância pendente; avanço só após expirar o prazo\",\"append-only: parecer/retificação = novo fato, não sobrescreve o passado\"]", "regra_prazo_recurso", null, "v1" }
                });

            migrationBuilder.CreateIndex(
                name: "ix_rol_de_regras_hash",
                schema: "selecao",
                table: "rol_de_regras",
                column: "hash");

            migrationBuilder.CreateIndex(
                name: "ux_rol_de_regras_codigo_versao",
                schema: "selecao",
                table: "rol_de_regras",
                columns: new[] { "codigo", "versao" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "rol_de_regras",
                schema: "selecao");
        }
    }
}
