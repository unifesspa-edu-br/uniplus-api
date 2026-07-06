namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Seed;

using System.Text.Json;

using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Fonte única do seed da biblioteca <c>rol_de_regras</c> (Story #772): as
/// regras <c>v1</c> tipadas e versionadas que a configuração do Processo
/// Seletivo referencia. Consumida tanto pela migration (que materializa as
/// linhas) quanto pelos testes (que recomputam o hash e conferem a
/// completude), garantindo uma única definição por regra.
/// </summary>
/// <remarks>
/// <para>
/// O conteúdo de domínio (código, tipo, <c>esquema_args</c>, invariantes e
/// base legal) é a modelagem validada do CEPS (P-A distribuição / P-B
/// classificação, provada contra Postgres real). Portado fielmente; as
/// melhorias são estruturais — identificadores fixos determinísticos, hash
/// canônico content-addressable computado no domínio e append-only por
/// convenção (sem gatilho de banco).
/// </para>
/// <para>
/// Os <see cref="Guid"/> são fixos (não <c>Guid.CreateVersion7</c>) porque
/// seed precisa de identidade estável entre execuções; o <c>hash</c> não é
/// literal — é derivado da definição via <see cref="Item.ComputarHash"/>,
/// mantendo content-addressability por construção.
/// </para>
/// </remarks>
public static class RegraCatalogoSeed
{
    /// <summary>Versão corrente de toda regra semeada nesta rodada.</summary>
    public const string VersaoV1 = "v1";

    private static Guid SeedId(int n) =>
        Guid.Parse($"d0a00000-0000-7000-8000-{n:D12}");

    /// <summary>As 18 regras <c>v1</c> do catálogo, na ordem canônica.</summary>
    public static IReadOnlyList<RegraCatalogoSeedItem> Itens { get; } =
    [
        // regra_calculo — fórmula da nota final
        new(SeedId(1), "FORMULA-MEDIA-PONDERADA", VersaoV1, TipoRegra.RegraCalculo,
            """{"fonte_pesos":["etapa","peso_area_enem"]}""",
            """["divisor = Σ(peso das etapas classificatória∪ambas)"]""",
            "Proposta CEPS + #56; média ponderada NOTA=Σ(nota×peso)/Σpeso"),

        new(SeedId(2), "CLASSIFICACAO-IMPORTADA", VersaoV1, TipoRegra.RegraCalculo,
            "{}",
            """["sem cálculo local; classificação federal por importação do listão"]""",
            "Portaria MEC 18/2012 art. 16 — SiSU federal (CEPS não calcula)"),

        // regra_arredondamento — precisão da nota
        new(SeedId(3), "PRECISAO-TRUNCAR", VersaoV1, TipoRegra.RegraArredondamento,
            """{"casas":"integer"}""",
            """["trunca na N-ésima casa, sem arredondar"]""",
            "Decisão CEPS/PO (gaps 1.1) — truncamento 2 casas (default)"),

        new(SeedId(4), "PRECISAO-ARREDONDAR-CIMA", VersaoV1, TipoRegra.RegraArredondamento,
            """{"casas":"integer"}""",
            """["arredonda p/ cima se 3ª casa ≥ 5"]""",
            "Reprodução de editais antigos (PSE/Convênios)"),

        // regra_eliminacao — eliminação por cálculo (lista)
        new(SeedId(5), "ELIM-NOTA-MINIMA-ETAPA", VersaoV1, TipoRegra.RegraEliminacao,
            """{"etapa":"text","nota_minima":"numeric"}""",
            """["nota < mínima na etapa → elimina"]""",
            "Edital por processo (nota mínima eliminatória)"),

        new(SeedId(6), "ELIM-CORTE-REDACAO", VersaoV1, TipoRegra.RegraEliminacao,
            """{"minimo":"numeric"}""",
            """["redação < mínimo → elimina"]""",
            "Res. 805/2024 Anexo I (corte de Redação = 400)"),

        new(SeedId(7), "ELIM-ZERO-EM-AREA", VersaoV1, TipoRegra.RegraEliminacao,
            "{}",
            """["nota zero em qualquer área do ENEM → elimina"]""",
            "Res. 805/2024 art. 5º (zero em qualquer área elimina)"),

        // regra_bonus — bônus sobre a nota final
        new(SeedId(8), "BONUS-MULTIPLICATIVO", VersaoV1, TipoRegra.RegraBonus,
            """{"fator":"numeric","teto":"numeric|null"}""",
            """["nota_final × fator, após os pesos; teto opcional"]""",
            "RN05 + decisão PO Jairo (×1,20 sem teto, após pesos)"),

        // criterio_desempate — critérios de desempate (tipados)
        new(SeedId(9), "DESEMPATE-IDOSO", VersaoV1, TipoRegra.CriterioDesempate,
            """{"idade_minima":"integer"}""",
            """["prioriza quem satisfaz FAIXA_ETARIA ≥ idade_minima"]""",
            "Lei 10.741/2003 art. 27 (Estatuto do Idoso)"),

        new(SeedId(10), "DESEMPATE-MAIOR-NOTA-ETAPA", VersaoV1, TipoRegra.CriterioDesempate,
            """{"etapa":"text"}""",
            """["ordena por maior nota da etapa indicada"]""",
            "Edital (ordem de desempate)"),

        new(SeedId(11), "DESEMPATE-MAIOR-IDADE", VersaoV1, TipoRegra.CriterioDesempate,
            "{}",
            """["ordena por data de nascimento (nascido mais cedo vence)"]""",
            "Edital (maior idade cronológica)"),

        new(SeedId(12), "DESEMPATE-PREDICADO-FATO", VersaoV1, TipoRegra.CriterioDesempate,
            """{"fato":"text","operador":"text","valor":"any"}""",
            """["prioriza quem satisfaz predicado sobre FatoCandidato; fato deve estar no vocabulário e ser coletado"]""",
            "Edital (critério adicional via fato do candidato — ex.: professor rural)"),

        // regra_ordem_alocacao — 1ª/2ª opção → remanejamento → lista de espera
        new(SeedId(13), "ALOCACAO-OPCOES-RN04", VersaoV1, TipoRegra.RegraOrdemAlocacao,
            """{"n_opcoes":"integer"}""",
            """["1ª opção → 2ª opção → remanejamento → lista de espera"]""",
            "RN04 (processamento de 1ª/2ª opção)"),

        // regra_elegibilidade — enquadramento em cota
        new(SeedId(14), "RENDA-PER-CAPITA-LEI-12711", VersaoV1, TipoRegra.RegraElegibilidade,
            """
            {"limite_sm":"numeric","sm_referencia":"numeric (SM congelado na data de início das inscrições)","periodo_apuracao_meses":"integer (ex.: 3 últimos meses)","criterio_media_mensal":"text (média mensal dos rendimentos brutos)","exclusoes_renda":"lista (PN 18/2012 art. 7º)","composicao_nucleo_familiar":"quem compõe o núcleo familiar"}
            """,
            """["média mensal da renda familiar bruta (últimos N meses, após exclusões) ÷ nº de membros do núcleo ≤ limite_sm × sm_referencia"]""",
            "Lei 12.711/2012 art. 1º parágrafo único (red. Lei 14.723/2023) — renda familiar bruta per capita ≤ 1 SM (ensino superior); PN MEC 18/2012 art. 6º-7º — apuração da renda mensal per capita + exclusões obrigatórias"),

        // regra_distribuicao_vagas — cálculo do quadro de vagas reservadas
        new(SeedId(15), "DISTRIB-VAGAS-LEI-12711", VersaoV1, TipoRegra.RegraDistribuicaoVagas,
            """
            {"pr_minimo":"numeric (piso 0,5 — art. 10 II; teto 1,0)","modo_arredondamento":"teto (ceil) em todas as sub-reservas EXCETO LI_Q (floor) — art. 11","ordem_garantia_minima":["LB_PPI","LB_Q","LB_PCD","LB_EP","LI_PPI","LI_PCD","LI_EP"],"sub_reservas":["PPI","Q","PCD","EP"],"entradas_por_edital":["VO_base","PR","ReferenciaReservaDemografica"]}
            """,
            """
            ["VR=ceil(VO×PR)","VRRI=ceil(VR×0,5)","VRSI=VR−VRRI","sub-reservas ceil EXCETO LI_Q=floor (art. 11)","garantia mín-1 ordenada I-VII condicional à disponibilidade (art. 10 §2º), LI_Q fora","INV-3a: LB_EP≥0 e LI_EP≥0","INV-3b: AC≥0","INV-3c: VR_final+RETIRADAS+AC=VO_base"]
            """,
            "Portaria Normativa MEC nº 18/2012 art. 10 e 11 (red. PN 2.027/2023) — distribuição e arredondamento das vagas reservadas; Lei 12.711/2012 (red. Lei 14.723/2023)"),

        new(SeedId(16), "DISTRIB-VAGAS-INSTITUCIONAL", VersaoV1, TipoRegra.RegraDistribuicaoVagas,
            """
            {"quadro_fixo_por_modalidade":"objeto {codigo: quantidade} fixado por edital (NÃO art. 10)","aplicacao":"PSIQ (IND/QUIL) e PSE Ed. Campo — quadro institucional"}
            """,
            """
            ["quadro fixo por edital (não recalculado pelo art. 10)","modalidades institucionais somam conforme composicao_vagas (SUPLEMENTAR_AO_TOTAL ou RETIRA_DE)"]
            """,
            "Res. Unifesspa 532/2021 (vagas PcD/Indígena/Quilombola); Portaria MEC 18/2012 art. 12 (reservas suplementares e outras ações afirmativas)"),

        // regra_ajuste_distribuicao_vagas — reconciliação do estouro
        new(SeedId(17), "RECONCILIACAO-VAGAS-ART11-PU", VersaoV1, TipoRegra.RegraAjusteDistribuicaoVagas,
            """
            {"reconciliacao_federal":"CAP_VO + PRIORIDADE_LB (art. 11 §único, embutida em DISTRIB-VAGAS-LEI-12711)","motores_nao_art10":"REDUZIR_DE | REDUZIR_PROPORCIONAL_EM (apenas distribuições institucionais; clamp >=0; VEDADOS à Lei 12.711)"}
            """,
            """
            ["cap em VO: a reserva nunca excede as vagas da oferta (art. 11 §único I)","prioridade LB>LI: na escassez a LI cede primeiro (art. 11 §único II)","estouro sobre o nominal 50% (VR) e LEGAL — piso (art. 10 II), absorvido pela AC","curso pequeno e determinístico por lei (capa, não bloqueia)"]
            """,
            "Portaria MEC 18/2012 art. 11, parágrafo único (red. PN 2.027/2023) — cap nas vagas da oferta + prioridade do inciso III (LB) sobre o inciso IV (LI); art. 10 II (mínimo 50%, piso)"),

        // regra_prazo_recurso — prazo/janela/instâncias do recurso
        new(SeedId(18), "RECURSO-MULTI-INSTANCIA", VersaoV1, TipoRegra.RegraPrazoRecurso,
            """
            {"prazo_valor":"numeric","prazo_unidade":"HORAS|DIAS|DIAS_UTEIS (sem default — dado do edital)","contra_ato":"codigo da fase produtora recorrível (NUNCA resultado definitivo)","marco_inicial":"a partir de quando conta (ex.: publicacao_resultado)","instancias":"lista ordenada [{instancia,dono,prazo_valor,prazo_unidade}] — 1ª CEPS, 2ª CONSEPE"}
            """,
            """
            ["interposição GATED pela janela aberta (sem recurso a qualquer momento)","sem recurso ao resultado definitivo (≠ recurso de habilitação CRCA)","2ª instância (CONSEPE) só após indeferimento da 1ª; CONSEPE emite parecer externo, CEPS anexa e aplica (vinculante)","processo PARALISA enquanto 2ª instância pendente; avanço só após expirar o prazo","append-only: parecer/retificação = novo fato, não sobrescreve o passado"]
            """,
            "Lei 9.784/1999 (processo administrativo federal); edital/processo (prazo configurável por edital); regimento CONSEPE (2ª instância)"),
    ];
}

/// <summary>
/// Definição serializável de uma regra do seed (fonte única), da qual o hash
/// content-addressable é derivado pelo mesmo algoritmo do domínio.
/// </summary>
public sealed record RegraCatalogoSeedItem(
    Guid Id,
    string Codigo,
    string Versao,
    TipoRegra Tipo,
    string EsquemaArgsJson,
    string InvariantesJson,
    string BaseLegal)
{
    /// <summary>Computa o hash canônico da definição (<see cref="HashCanonicalComputer.ComputeRegraCatalogo"/>).</summary>
    public string ComputarHash() => HashCanonicalComputer.ComputeRegraCatalogo(
        Codigo,
        Versao,
        Tipo,
        ParseElemento(EsquemaArgsJson),
        ParseElemento(InvariantesJson),
        BaseLegal);

    private static JsonElement ParseElemento(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
