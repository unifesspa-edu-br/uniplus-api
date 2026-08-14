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
/// base legal) é a modelagem do CEPS para distribuição de vagas e para
/// classificação, validada contra Postgres real. Portado fielmente; as
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
/// <para>
/// A linha <c>SeedId(18)</c> (<c>RECURSO-PRAZO-ANCORADO-EM-ATO</c>) substituiu a
/// antiga <c>RECURSO-MULTI-INSTANCIA</c>, que conflacionava a gestão de uma
/// segunda instância — inexistente no Uni+ — com a janela de suspensividade.
/// A ADR-0112 fixa a fronteira dessa correção: o seed é corrigível por
/// substituição enquanto nenhuma configuração congelada o referenciar; a partir
/// do primeiro congelamento, vale append-only estrito (RN08).
/// </para>
/// <para>
/// <b>Precondição obrigatória de migration (ADR-0112):</b> qualquer migration
/// futura que substitua ou remova entrada deste seed deve verificar, antes de
/// alterar qualquer linha, que nenhuma <c>VersaoConfiguracao</c> referencia o
/// par código e versão afetado — encontrando referência, a migration aborta.
/// O <c>Down</c> da migration <c>AddAlgoritmosContagemPrazo</c> é o exemplo
/// executável do padrão (bloco <c>DO</c> com <c>RAISE EXCEPTION</c> antes do
/// <c>DeleteData</c>).
/// </para>
/// </remarks>
public static class RegraCatalogoSeed
{
    /// <summary>Versão corrente de toda regra semeada nesta rodada.</summary>
    public const string VersaoV1 = "v1";

    private static Guid SeedId(int n) =>
        Guid.Parse($"d0a00000-0000-7000-8000-{n:D12}");

    /// <summary>As 22 regras <c>v1</c> do catálogo, na ordem canônica.</summary>
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

        // regra_prazo_recurso — prazo do recurso ancorado em ato + janela de suspensividade por instância
        new(SeedId(18), "RECURSO-PRAZO-ANCORADO-EM-ATO", VersaoV1, TipoRegra.RegraPrazoRecurso,
            """
            {"prazo_valor":"numeric (> 0)","prazo_unidade":"HORAS|DIAS|DIAS_UTEIS (sem default — dado do edital)","ato_ancora_codigo":"código do tipo de ato — o prazo conta do INSTANTE DE PUBLICAÇÃO do ato, nunca de data fixa; a âncora nunca é um ato que congela configuração","suspensividade_primeira_instancia":"{valor:numeric, unidade:HORAS|DIAS|DIAS_UTEIS} | null — null = a pendência na fase não bloqueia atos irreversíveis","suspensividade_segunda_instancia":"{valor:numeric, unidade:HORAS|DIAS|DIAS_UTEIS} | null — null = a pendência em instância superior não bloqueia (via judicial, prazo indeterminado)"}
            """,
            """
            ["o Uni+ gere apenas a 1ª instância — o julgamento em instância superior (administrativa ou judicial) corre FORA do sistema; a sua existência e o seu desfecho são REGISTRADOS como ato publicado","a suspensividade é configurável por fase e por grau: null = a pendência não bloqueia atos irreversíveis","a janela de suspensividade fecha no julgamento OU no fim do prazo, o que vier primeiro — recurso nunca julgado não trava o certame para sempre","interposição só é aceita com a janela da fase de recurso aberta","não cabe recurso contra resultado definitivo","prazo ancorado no instante de publicação do ato âncora: se o ato atrasa, o prazo desliza junto, sem retificação","a âncora nunca é um tipo de ato que congela configuração","DIAS_UTEIS é recusado na INTERPOSIÇÃO enquanto não houver calendário — nunca aproximado em silêncio","append-only: julgamento e retificação são NOVO fato, não sobrescrevem o passado"]
            """,
            "Lei 9.784/1999 art. 56 (cabimento do recurso administrativo) e art. 61 (efeito suspensivo por decisão fundamentada); prazo configurável por edital"),

        // criterio_remanejamento — sequência legal de redirecionamento das cotas federais
        new(SeedId(19), "REMANEJ-CASCATA-LEI-12711", VersaoV1, TipoRegra.CriterioRemanejamento,
            """
            {"fallbackCodigo":"AC","ordens":[
              {"origem":"LB_PPI","destinos":["LB_Q","LB_PCD","LB_EP","LI_PPI","LI_Q","LI_PCD","LI_EP"]},
              {"origem":"LB_Q","destinos":["LB_PPI","LB_PCD","LB_EP","LI_PPI","LI_Q","LI_PCD","LI_EP"]},
              {"origem":"LB_PCD","destinos":["LB_PPI","LB_Q","LB_EP","LI_PPI","LI_Q","LI_PCD","LI_EP"]},
              {"origem":"LB_EP","destinos":["LB_PPI","LB_Q","LB_PCD","LI_PPI","LI_Q","LI_PCD","LI_EP"]},
              {"origem":"LI_PPI","destinos":["LB_PPI","LB_Q","LB_PCD","LB_EP","LI_Q","LI_PCD","LI_EP"]},
              {"origem":"LI_Q","destinos":["LB_PPI","LB_Q","LB_PCD","LB_EP","LI_PPI","LI_PCD","LI_EP"]},
              {"origem":"LI_PCD","destinos":["LB_PPI","LB_Q","LB_PCD","LB_EP","LI_PPI","LI_Q","LI_EP"]},
              {"origem":"LI_EP","destinos":["LB_PPI","LB_Q","LB_PCD","LB_EP","LI_PPI","LI_Q","LI_PCD"]}
            ]}
            """,
            """["oito origens federais, ordem fixa por origem, terminal sempre AC — matriz não recalculada, só aplicada"]""",
            "ADR-0120; Portaria MEC nº 704/2025 (DOU 20/10/2025, Seção 1, p. 36-37), art. 20-A e Anexo — insere o art. 20-A na Portaria Normativa MEC nº 18/2012; Lei 12.711/2012 art. 3º §1º (red. Lei 14.723/2023)"),

        // algoritmo_contagem_prazo — convenções nomeadas de contagem do prazo de
        // interposição (UNI-REQ-0112). A entrada descreve e congela a convenção;
        // o motor executa pelo par código e versão. Escolhido, não parametrizado:
        // esquema_args vazio. As invariantes embutem os exemplos resolvidos nas
        // âncoras canônicas do requisito (sexta 18h; domingo 18h), para que
        // intenção e resultado da escolha vivam na própria entrada.
        new(SeedId(20), AlgoritmoContagemPrazoCodigo.ExcluiDiaInicial, VersaoV1, TipoRegra.AlgoritmoContagemPrazo,
            "{}",
            """
            ["âncora fora da meia-noite: a hora da âncora não influencia o fechamento — o dia civil da âncora é excluído por inteiro e a contagem parte do primeiro dia útil seguinte (1 dia útil ancorado sexta 18h, sem feriado no intervalo, fecha no fim de segunda)","âncora em dia não útil: o início desloca para o primeiro dia útil seguinte; o dia da âncora, útil ou não, nunca conta (1 dia útil ancorado domingo 18h, sem feriado no intervalo, fecha no fim de segunda)","em dias úteis: N dias úteis inteiros contados após o dia excluído; a janela fecha na fronteira final do N-ésimo dia útil — dia civil fechado no início e aberto no fim, no fuso congelado","em horas: a contagem começa no primeiro instante do primeiro dia útil seguinte ao dia da âncora e consome apenas horas situadas em dia útil (48h ancoradas sexta 18h, sem feriado no intervalo, começam segunda 00:00 e fecham quarta 00:00)"]
            """,
            AlgoritmoContagemPrazoCodigo.BaseLegalPendente),

        new(SeedId(21), AlgoritmoContagemPrazoCodigo.HorasUteisDesdeAncora, VersaoV1, TipoRegra.AlgoritmoContagemPrazo,
            "{}",
            """
            ["âncora fora da meia-noite: a contagem parte do instante exato da âncora — a hora do fechamento deriva da hora da âncora, sem deslocamento para fronteira de dia (48h ancoradas sexta 18h, com sábado e domingo não úteis e sem feriado, fecham terça 18h)","âncora em dia não útil: o início não desloca — o relógio não avança em instante situado em dia não útil e o primeiro avanço ocorre no primeiro instante útil seguinte (24h ancoradas domingo 18h, com segunda útil, só começam a consumir segunda 00:00 e fecham terça 00:00)","em horas: consome exatamente o valor declarado em horas situadas em dia útil, atravessando a madrugada de dia útil normalmente; fecha no instante em que o saldo zera","em dias úteis: N dias úteis equivalem a N×24 horas situadas em dia útil consumidas desde a âncora; dia civil de transição de fuso contribui com as horas que realmente tem, nunca um bloco presumido de 24 (1 dia útil ancorado sexta 18h, sem feriado no intervalo, fecha segunda 18h)"]
            """,
            AlgoritmoContagemPrazoCodigo.BaseLegalPendente),

        new(SeedId(22), AlgoritmoContagemPrazoCodigo.AvancaDataUtil, VersaoV1, TipoRegra.AlgoritmoContagemPrazo,
            "{}",
            """
            ["âncora fora da meia-noite: mantém a hora da âncora — a contagem parte do instante exato, sem deslocamento para fronteira de dia (1 dia útil ancorado sexta 18h, com sábado e domingo não úteis e sem feriado, fecha segunda 18h)","âncora em dia não útil: em dias úteis, desloca para o próximo dia útil na mesma hora (âncora domingo 18h conta como segunda 18h, e 1 dia útil fecha terça 18h); em horas não há deslocamento, apenas a não contagem dos instantes de dia não útil","em dias úteis: fecha na mesma hora da âncora, N datas úteis adiante, pulando cada data não útil; se a hora da âncora não existir na data de fechamento por transição de fuso, fecha no primeiro instante válido seguinte","em horas: consome horas situadas em dia útil desde a âncora, sem deslocar o início — nesta unidade a convenção coincide com CONTAGEM-PRAZO-HORAS-UTEIS-DESDE-ANCORA, e a diferença entre as duas está só na unidade dias úteis"]
            """,
            AlgoritmoContagemPrazoCodigo.BaseLegalPendente),
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
