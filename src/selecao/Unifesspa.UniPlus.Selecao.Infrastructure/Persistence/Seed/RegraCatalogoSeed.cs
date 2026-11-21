namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Seed;

using System.Text.Json;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Fonte única do seed da biblioteca <c>regras_do_catalogo</c> (Story #772): as
/// regras <c>v1</c> tipadas e versionadas que a configuração do Processo
/// Seletivo referencia. Consumida tanto pelo modelo (<c>HasData</c>, que materializa as
/// linhas) quanto pelos testes (que recomputam o hash e conferem a
/// completude), garantindo uma única definição por regra.
/// </summary>
/// <remarks>
/// <para>
/// O conteúdo de domínio (código, tipo, <c>declaracao_da_regra</c>, invariantes e
/// base legal) é a modelagem do CEPS para distribuição de vagas e para
/// classificação, validada contra Postgres real. Portado fielmente; as
/// melhorias são estruturais — identificadores fixos determinísticos, hash
/// canônico calculado no domínio e preservado por
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
/// segunda instância — inexistente no Uni+ — com o período de suspensividade.
/// A ADR-0112 fixa a fronteira dessa correção: o seed é corrigível por
/// substituição enquanto nenhuma configuração congelada o referenciar; a partir
/// do primeiro congelamento, os registros anteriores são preservados (congelamento dos parâmetros na publicação, UNI-REQ-0019).
/// </para>
/// <para>
/// <b>Precondição (ADR-0112):</b> substituir ou remover uma entrada deste seed exige
/// verificar, antes de alterar qualquer linha, que nenhuma <c>VersaoConfiguracao</c>
/// referencia o par código e versão afetado. Enquanto não há produção, a Emenda 1 da
/// ADR-0112 autoriza, além de recusar a mudança, descartar o processo inteiro que
/// carrega a referência.
/// </para>
/// </remarks>
public static class RegraCatalogoSeed
{
    /// <summary>
    /// Versão única de toda regra semeada: sem produção, o catálogo não carrega uma segunda
    /// versão só para preservar configuração que ninguém publicou — a Emenda 1 da ADR-0112
    /// registra a fronteira.
    /// </summary>
    public const string VersaoV1 = "v1";

    private static Guid SeedId(int n) =>
        Guid.Parse($"d0a00000-0000-7000-8000-{n:D12}");

    /// <summary>As regras do catálogo, na ordem canônica, todas em <see cref="VersaoV1"/>.</summary>
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
            "{}",
            """["trunca na N-ésima casa, sem arredondar"]""",
            "Decisão CEPS/PO (gaps 1.1) — truncamento 2 casas (default)"),

        new(SeedId(4), "PRECISAO-ARREDONDAR-CIMA", VersaoV1, TipoRegra.RegraArredondamento,
            "{}",
            """["arredonda p/ cima se 3ª casa ≥ 5"]""",
            "Reprodução de editais antigos (PSE/Convênios)"),

        // regra_eliminacao — eliminação por cálculo (lista)
        new(SeedId(5), "ELIM-NOTA-MINIMA-ETAPA", VersaoV1, TipoRegra.RegraEliminacao,
            "{}",
            """["nota < mínima na etapa → elimina"]""",
            "Edital por processo (nota mínima eliminatória)"),

        // O ponto de corte do art. 5º vale para cada área do ENEM; o Anexo I só preenche a
        // Redação por enquanto.
        new(SeedId(6), RegraEliminacaoCodigo.ElimCorteEmArea, VersaoV1, TipoRegra.RegraEliminacao,
            "{}",
            """["nota na área < mínimo → elimina","no máximo um corte por área"]""",
            "Res. 805/2024 art. 5º e Anexo I (ponto de corte por área do ENEM; Redação = 400)"),

        new(SeedId(7), "ELIM-ZERO-EM-AREA", VersaoV1, TipoRegra.RegraEliminacao,
            "{}",
            """["nota zero em qualquer área do ENEM → elimina"]""",
            "Res. 805/2024 art. 5º (zero em qualquer área elimina)"),

        // Falta não é nota zero: quem falta fica sem nota nas áreas do dia, e quem tira zero
        // compareceu. A segunda invariante cobre o ano de referência do ENEM informado errado.
        new(SeedId(29), RegraEliminacaoCodigo.ElimFaltaEmDiaDeProvaEnem, VersaoV1, TipoRegra.RegraEliminacao,
            "{}",
            """["falta em pelo menos um dia de prova da edição do ENEM usada no processo → elimina","sem participação registrada na edição do ENEM indicada pelo candidato equivale a falta"]""",
            "Edital (falta em dia de prova do ENEM)"),

        // Vale para etapa com ou sem nota; a do ENEM tem a falta em dia de prova.
        new(SeedId(32), RegraEliminacaoCodigo.ElimAusenciaNaEtapa, VersaoV1, TipoRegra.RegraEliminacao,
            "{}",
            """["falta à etapa referenciada → elimina","não se aplica à etapa de nota do ENEM"]""",
            "Edital (eliminação do candidato que faltar à etapa)"),

        // regra_bonus — bônus sobre a nota final
        new(SeedId(8), "BONUS-MULTIPLICATIVO", VersaoV1, TipoRegra.RegraBonus,
            "{}",
            """["nota final multiplicada pelo fator, após os pesos; teto opcional"]""",
            "Bônus multiplicativo aplicado à nota final após os pesos das áreas do ENEM, sem teto por padrão"),

        // criterio_desempate — critérios de desempate (tipados)
        new(SeedId(9), "DESEMPATE-IDOSO", VersaoV1, TipoRegra.CriterioDesempate,
            "{}",
            """["prioriza quem tem idade igual ou superior à idade mínima declarada no critério"]""",
            "Lei 10.741/2003 art. 27 (Estatuto do Idoso)"),

        new(SeedId(10), "DESEMPATE-MAIOR-NOTA-ETAPA", VersaoV1, TipoRegra.CriterioDesempate,
            "{}",
            """["ordena por maior nota da etapa indicada"]""",
            "Edital (ordem de desempate)"),

        new(SeedId(11), "DESEMPATE-MAIOR-IDADE", VersaoV1, TipoRegra.CriterioDesempate,
            "{}",
            """["ordena por data de nascimento (nascido mais cedo vence)"]""",
            "Edital (maior idade cronológica)"),

        new(SeedId(12), "DESEMPATE-PREDICADO-FATO", VersaoV1, TipoRegra.CriterioDesempate,
            "{}",
            """["prioriza quem atende à condição sobre um dado do candidato; o dado deve estar cadastrado e ser solicitado"]""",
            "Edital (critério adicional via fato do candidato — ex.: professor rural)"),

        // Cita cada área pelo código do quadro de pesos por área registrado na versão da classificação;
        // o rótulo vem da mesma cópia, e o critério não guarda nenhum. A lista é ordenada: a
        // posição de cada código é a prioridade dele no desempate, como dizem as invariantes.
        new(SeedId(28), CriterioDesempateCodigo.MaiorNotaAreaEnem, VersaoV1, TipoRegra.CriterioDesempate,
            "{}",
            """["ordena por maior nota na primeira área da lista; o empate que resta passa à área seguinte","cada área é citada uma única vez, pelo código, e existe em todos os grupos do quadro de pesos por área registrado na versão da classificação","exige classificação baseada em ENEM, calculada pela média ponderada"]""",
            "Edital (ordem de desempate por nota de área do ENEM)"),

        // regra_ordem_alocacao — a 1ª opção tem prioridade; a 2ª só recebe a vaga que sobra
        new(SeedId(13), RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, VersaoV1, TipoRegra.RegraOrdemAlocacao,
            "{}",
            """["processam-se todas as 1ªs opções de cada curso","a vaga de modalidade não preenchida é remanejada dentro do curso, entre candidatos de 1ª opção","só a vaga que sobra depois do remanejamento vai à 2ª opção, em ordem de nota, sem deslocar aprovado de 1ª opção","quem não é classificado em nenhuma opção vai para a lista de espera"]""",
            "UNI-REQ-0045 — processamento da 1ª opção antes da 2ª"),

        // regra_elegibilidade — enquadramento em cota
        new(SeedId(14), "RENDA-PER-CAPITA-LEI-12711", VersaoV1, TipoRegra.RegraElegibilidade,
            "{}",
            """["média mensal da renda familiar bruta no período considerado, após exclusões, dividida pelo número de membros da família deve ser menor ou igual ao limite em salários mínimos multiplicado pelo valor de referência do salário mínimo"]""",
            "Lei 12.711/2012 art. 1º parágrafo único (red. Lei 14.723/2023) — renda familiar bruta per capita ≤ 1 SM (ensino superior); PN MEC 18/2012 art. 6º-7º — apuração da renda mensal per capita + exclusões obrigatórias"),

        // regra_ajuste_distribuicao_vagas — reconciliação do estouro
        new(SeedId(17), "RECONCILIACAO-VAGAS-ARTIGO-11-PARAGRAFO-UNICO", VersaoV1, TipoRegra.RegraAjusteDistribuicaoVagas,
            "{}",
            """
            ["a reserva nunca excede as vagas da oferta (art. 11 §único I)","na falta de vagas, as modalidades de baixa renda têm prioridade sobre as demais (art. 11 §único II)","a reserva pode superar o mínimo legal de 50%, reduzindo as vagas da ampla concorrência (art. 10 II)","a distribuição em cursos com poucas vagas segue o limite previsto em lei, sem impedir a oferta"]
            """,
            "Portaria MEC 18/2012 art. 11, parágrafo único (red. PN 2.027/2023) — reserva limitada às vagas da oferta e prioridade do inciso III (LB) sobre o inciso IV (LI); art. 10 II (mínimo 50%, piso)"),

        // regra_prazo_recurso — prazo do recurso contado de ato e período de suspensividade por instância
        new(SeedId(18), "RECURSO-PRAZO-ANCORADO-EM-ATO", VersaoV1, TipoRegra.RegraPrazoRecurso,
            "{}",
            """
            ["o Uni+ gere apenas a 1ª instância — o julgamento em instância superior (administrativa ou judicial) corre FORA do sistema; a sua existência e o seu desfecho são REGISTRADOS como ato publicado","a suspensividade é configurável por fase e por grau: sem prazo de suspensividade, a pendência não impede atos irreversíveis","o período de suspensividade termina no julgamento ou no fim do prazo, o que ocorrer primeiro — recurso sem julgamento não impede o andamento do certame indefinidamente","interposição só é aceita durante o período da fase de recurso","não cabe recurso contra resultado definitivo","o prazo começa no instante da publicação do ato: se a publicação atrasa, o prazo acompanha o atraso, sem retificação","o evento que inicia o prazo é a publicação de um produto preliminar da própria fase, identificado pela publicação e não pelo código do tipo de ato — a publicação do mesmo tipo por outra fase do cronograma não inicia o prazo desta","o evento que inicia o prazo nunca é um ato que registra a configuração da versão publicada","o prazo de interposição corre exclusivamente em dia útil e admite apenas dias úteis em valor inteiro ou horas — dias corridos são recusados, e fração de dia útil também, cada um com causa própria; nunca aproximado em silêncio","contagem sobre dia útil depende de duas declarações do processo, o calendário de dias úteis vigente da localidade regente e a convenção de contagem — vale para todo prazo de interposição, nas duas unidades, e para a suspensividade em dias úteis","a suspensividade em horas ou em dias corridos não depende de calendário nem de convenção de contagem — conta todos os dias, sem distinguir úteis de não úteis","julgamento e retificação são novos fatos e não sobrescrevem os registros anteriores"]
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
            """["oito origens federais, ordem fixa por origem e ampla concorrência como modalidade final — a ordem é aplicada sem recálculo"]""",
            "Portaria MEC nº 704/2025 (DOU 20/10/2025, Seção 1, p. 36-37), art. 2º e Anexo — insere o art. 20-A na Portaria Normativa MEC nº 21/2012; Lei 12.711/2012 art. 3º §1º (red. Lei 14.723/2023)"),

        // algoritmo_contagem_prazo — convenções nomeadas de contagem do prazo de
        // interposição (UNI-REQ-0112). A entrada descreve e congela a convenção;
        // o motor executa pelo par código e versão. Escolhido, não parametrizado:
        // declaracao_da_regra vazio. As invariantes incluem os exemplos de início do prazo
        // resolvidos no requisito (sexta 18h; domingo 18h), para que
        // intenção e resultado da escolha vivam na própria entrada.
        new(SeedId(20), AlgoritmoContagemPrazoCodigo.ExcluiDiaInicial, VersaoV1, TipoRegra.AlgoritmoContagemPrazo,
            "{}",
            """
            ["evento que inicia o prazo fora da meia-noite: a hora do evento que inicia o prazo não influencia o fechamento — o dia civil do evento que inicia o prazo é excluído por inteiro e a contagem parte do primeiro dia útil seguinte (1 dia útil iniciado sexta às 18h, sem feriado no intervalo, fecha no fim de segunda)","evento que inicia o prazo em dia não útil: o início desloca para o primeiro dia útil seguinte; o dia do evento que inicia o prazo, útil ou não, nunca conta (1 dia útil iniciado domingo às 18h, sem feriado no intervalo, fecha no fim de segunda)","em dias úteis: N dias úteis inteiros contados após o dia excluído; o prazo termina à meia-noite que inicia o dia seguinte ao N-ésimo dia útil; esse instante não está incluído no prazo, conforme o fuso horário registrado na versão publicada","em horas: a contagem começa no primeiro instante do primeiro dia útil seguinte ao dia do evento que inicia o prazo e consome apenas horas situadas em dia útil (48h iniciadas sexta às 18h, sem feriado no intervalo, começam segunda 00:00 e fecham quarta 00:00)"]
            """,
            AlgoritmoContagemPrazoCodigo.BaseLegalDeclaradaPeloEdital),

        new(SeedId(21), AlgoritmoContagemPrazoCodigo.HorasUteisDesdeAncora, VersaoV1, TipoRegra.AlgoritmoContagemPrazo,
            "{}",
            """
            ["evento que inicia o prazo fora da meia-noite: a contagem parte do instante exato do evento que inicia o prazo — a hora do fechamento deriva da hora do evento que inicia o prazo, sem deslocamento para o início de outro dia (48h iniciadas sexta às 18h, com sábado e domingo não úteis e sem feriado, fecham terça 18h)","evento que inicia o prazo em dia não útil: o início não desloca — o relógio não avança em instante situado em dia não útil e o primeiro avanço ocorre no primeiro instante útil seguinte (24h iniciadas domingo às 18h, com segunda útil, só começam a consumir segunda 00:00 e fecham terça 00:00)","em horas: consome exatamente o valor declarado em horas situadas em dia útil, atravessando a madrugada de dia útil normalmente; fecha no instante em que o saldo zera","em dias úteis: N dias úteis equivalem a N×24 horas situadas em dia útil consumidas desde o evento que inicia o prazo; dia civil de transição de fuso contribui com as horas que realmente tem, nunca um bloco presumido de 24 (1 dia útil iniciado sexta às 18h, sem feriado no intervalo, fecha segunda 18h)"]
            """,
            AlgoritmoContagemPrazoCodigo.BaseLegalDeclaradaPeloEdital),

        new(SeedId(22), AlgoritmoContagemPrazoCodigo.AvancaDataUtil, VersaoV1, TipoRegra.AlgoritmoContagemPrazo,
            "{}",
            """
            ["evento que inicia o prazo fora da meia-noite: mantém a hora do evento que inicia o prazo — a contagem parte do instante exato, sem deslocamento para o início de outro dia (1 dia útil iniciado sexta às 18h, com sábado e domingo não úteis e sem feriado, fecha segunda 18h)","evento que inicia o prazo em dia não útil: em dias úteis, desloca para o próximo dia útil na mesma hora (evento que inicia o prazo no domingo às 18h conta como segunda 18h, e 1 dia útil fecha terça 18h); em horas não há deslocamento, apenas a não contagem dos instantes de dia não útil","em dias úteis: fecha na mesma hora do evento que inicia o prazo, N datas úteis adiante, pulando cada data não útil; se a hora do evento que inicia o prazo não existir na data de fechamento por transição de fuso, fecha no primeiro instante válido seguinte","em horas: consome horas situadas em dia útil desde o evento que inicia o prazo, sem deslocar o início — nesta unidade a convenção coincide com a contagem de horas úteis a partir do evento que inicia o prazo; a diferença entre as duas está apenas na contagem em dias úteis"]
            """,
            AlgoritmoContagemPrazoCodigo.BaseLegalDeclaradaPeloEdital),

        // As cinco entradas abaixo declaram MODALIDADES_ADMITIDAS: o conjunto exato que a
        // regra reconhece, sem folga — a folga de AC_PCD dentro de DISTRIBUICAO-LEI-12711 é
        // o que a variação -COM-AC-PCD isola como regra própria. Sem o rol, qualquer
        // modalidade era aceita sob qualquer regra, e uma calculada fora do art. 10 falhava
        // com "não aparece no quadro calculado" — sintoma interno, não a causa.
        new(SeedId(23), RegraDistribuicaoVagasCodigo.Lei12711, VersaoV1, TipoRegra.RegraDistribuicaoVagas,
            """
            {"pr_minimo":"numeric (piso 0,5 — art. 10 II; teto 1,0)","modo_arredondamento":"teto (ceil) em todas as sub-reservas EXCETO LI_Q (floor) — art. 11","ordem_garantia_minima":["LB_PPI","LB_Q","LB_PCD","LB_EP","LI_PPI","LI_PCD","LI_EP"],"sub_reservas":["PPI","Q","PCD","EP"],"entradas_por_edital":["VO_base","PR","ReferenciaReservaDemografica"],"modalidades_admitidas":["AC","LB_PPI","LB_Q","LB_PCD","LB_EP","LI_PPI","LI_Q","LI_PCD","LI_EP"]}
            """,
            """
            ["VR=ceil(VO×PR)","VRRI=ceil(VR×0,5)","VRSI=VR−VRRI","sub-reservas ceil EXCETO LI_Q=floor (art. 11)","garantia mín-1 ordenada I-VII condicional à disponibilidade (art. 10 §2º), LI_Q fora","LB_EP≥0 e LI_EP≥0","AC≥0","VR_final+RETIRADAS+AC=VO_base","modalidade fora de modalidades_admitidas é recusada"]
            """,
            "Portaria Normativa MEC nº 18/2012 art. 10 e 11 (red. PN 2.027/2023) — distribuição e arredondamento das vagas reservadas; Lei 12.711/2012 (red. Lei 14.723/2023)"),

        // A mesma fórmula de DISTRIBUICAO-LEI-12711, com AC_PCD (retirada da ampla
        // concorrência) como décima modalidade do rol. Ofertar AC_PCD junto das cotas é
        // válido: a exclusividade entre cota da lei e ação afirmativa vale por inscrição, não
        // por oferta, e a AC_PCD é a exceção, a que quem optou pelas cotas também concorre
        // (UNI-REQ-0142 e UNI-REQ-0266).
        new(SeedId(27), RegraDistribuicaoVagasCodigo.Lei12711ComAcPcd, VersaoV1, TipoRegra.RegraDistribuicaoVagas,
            """
            {"pr_minimo":"numeric (piso 0,5 — art. 10 II; teto 1,0)","modo_arredondamento":"teto (ceil) em todas as sub-reservas EXCETO LI_Q (floor) — art. 11","ordem_garantia_minima":["LB_PPI","LB_Q","LB_PCD","LB_EP","LI_PPI","LI_PCD","LI_EP"],"sub_reservas":["PPI","Q","PCD","EP"],"entradas_por_edital":["VO_base","PR","ReferenciaReservaDemografica"],"modalidades_admitidas":["AC","LB_PPI","LB_Q","LB_PCD","LB_EP","LI_PPI","LI_Q","LI_PCD","LI_EP","AC_PCD"]}
            """,
            """
            ["VR=ceil(VO×PR)","VRRI=ceil(VR×0,5)","VRSI=VR−VRRI","sub-reservas ceil EXCETO LI_Q=floor (art. 11)","garantia mín-1 ordenada I-VII condicional à disponibilidade (art. 10 §2º), LI_Q fora","LB_EP≥0 e LI_EP≥0","AC≥0","VR_final+RETIRADAS+AC=VO_base","AC_PCD retira de AC, como qualquer outra retirada federal","modalidade fora de modalidades_admitidas é recusada"]
            """,
            "Portaria Normativa MEC nº 18/2012 art. 10 e 11 (red. PN 2.027/2023) — distribuição e arredondamento das vagas reservadas; Lei 12.711/2012 (red. Lei 14.723/2023)"),

        new(SeedId(24), RegraDistribuicaoVagasCodigo.Institucional, VersaoV1, TipoRegra.RegraDistribuicaoVagas,
            """
            {"quadro_fixo_por_modalidade":"objeto {codigo: quantidade} fixado por edital (NÃO art. 10)","aplicacao":"quadro institucional não nomeado por regra própria","modalidades_admitidas":null}
            """,
            """
            ["quadro fixo por edital (não recalculado pelo art. 10)","a soma das quantidades declaradas é o total publicado, e fecha no VO_base","modalidades_admitidas nulo: rol aberto, para o certame institucional que ainda não tem regra própria"]
            """,
            "Res. Unifesspa 532/2021 (vagas PcD/Indígena/Quilombola); Portaria MEC 18/2012 art. 12 (reservas suplementares e outras ações afirmativas)"),

        new(SeedId(25), RegraDistribuicaoVagasCodigo.Psiq, VersaoV1, TipoRegra.RegraDistribuicaoVagas,
            """
            {"quadro_fixo_por_modalidade":"objeto {codigo: quantidade} fixado por edital (NÃO art. 10)","aplicacao":"Processo Seletivo Indígena e Quilombola","modalidades_admitidas":["AC_I","AC_Q"]}
            """,
            """
            ["quadro fixo por edital (não recalculado pelo art. 10)","certame exclusivo: não há ampla concorrência","rol composto só de vagas por acréscimo — sem outro conjunto ao qual se somem, a soma delas é o total publicado"]
            """,
            "Res. Unifesspa 532/2021-CONSEPE, art. 2º (vagas por acréscimo para candidatos indígenas e quilombolas)"),

        // O processo que reserva vaga de pessoa com deficiência sem ofertar as cotas
        // federais, como o PSE Educação do Campo. A reserva é a mesma AC_PCD do ramo federal —
        // a ação afirmativa não depende de escola nem de renda (UNI-REQ-0141). AC permanece
        // obrigatória: AC_PCD é RETIRADA_DE_OUTRA_MODALIDADE origem AC, e ValidarReferenciasCruzadas recusa a
        // ausência em qualquer ramo.
        new(SeedId(26), RegraDistribuicaoVagasCodigo.ComAcPcd, VersaoV1, TipoRegra.RegraDistribuicaoVagas,
            """
            {"quadro_fixo_por_modalidade":"objeto {codigo: quantidade} fixado por edital (NÃO art. 10)","aplicacao":"quadro fixo sem as cotas federais — AC_PCD como reserva de pessoa com deficiência fora do regime federal","modalidades_admitidas":["AC","AC_PCD"]}
            """,
            """
            ["quadro fixo por edital (não recalculado pelo art. 10)","certame sem as cotas da Lei 12.711","AC_PCD retira de AC: o par fecha no VO_base"]
            """,
            "Res. Unifesspa 532/2021-CONSEPE, art. 1º (reserva de vaga para pessoa com deficiência); Portaria MEC 18/2012 art. 12"),

        // regra_consolidacao — como as notas lançadas pelos avaliadores de uma etapa de avaliação
        // humana viram a nota da etapa. A entrada descreve e congela a regra; quem a executa é a
        // apuração. Escolhida pelo edital, não parametrizada: a declaração da regra registra o que a versão declara de
        // si, as naturezas de conceito a que se aplica e os corretores distintos que exige (ADR-0150).
        // A regra de conceito categórico declara também o critério de apuração (unanimidade,
        // maioria_simples ou maioria_absoluta) e o tratamento quando o critério não converge
        // (novo_corretor ou decisao_da_banca); mudar qualquer um deles é versão nova da regra.
        new(SeedId(30), RegraConsolidacaoCodigo.AvaliacaoUnica, VersaoV1, TipoRegra.RegraConsolidacao,
            """{"corretores_distintos":1,"naturezas_do_conceito":["categorico","numerico"]}""",
            """["um só avaliador lança a nota de cada candidato na etapa","a nota lançada é a nota da etapa, sem combinação com outra"]""",
            "Decisão institucional da Unifesspa"),

        new(SeedId(31), RegraConsolidacaoCodigo.CorrecaoIndependente, VersaoV1, TipoRegra.RegraConsolidacao,
            """{"corretores_distintos":3,"naturezas_do_conceito":["numerico"]}""",
            """["mais de um avaliador corrige cada candidato na etapa, cada um sem conhecer a nota do outro","a nota da etapa é a combinação das correções independentes, na forma que o edital determina"]""",
            "UNI-REQ-0097 (dupla correção) e edital do processo seletivo"),

        new(SeedId(37), RegraConsolidacaoCodigo.ConceitoUnanimidade, VersaoV1, TipoRegra.RegraConsolidacao,
            """{"corretores_distintos":2,"criterio":"unanimidade","naturezas_do_conceito":["categorico"],"quando_nao_consolida":"decisao_da_banca"}""",
            """["cada corretor lança o conceito sem conhecer o dos demais","o conceito da etapa é o valor em que o critério converge: todos os corretores atribuem o mesmo valor","quando o critério não converge, o tratamento que a versão declara é aplicado"]""",
            "Decisão institucional da Unifesspa"),

        new(SeedId(38), RegraConsolidacaoCodigo.ConceitoMaioriaSimples, VersaoV1, TipoRegra.RegraConsolidacao,
            """{"corretores_distintos":3,"criterio":"maioria_simples","naturezas_do_conceito":["categorico"],"quando_nao_consolida":"novo_corretor"}""",
            """["cada corretor lança o conceito sem conhecer o dos demais","o conceito da etapa é o valor em que o critério converge: um valor com mais atribuições que cada um dos outros","quando o critério não converge, o tratamento que a versão declara é aplicado"]""",
            "Decisão institucional da Unifesspa"),

        new(SeedId(39), RegraConsolidacaoCodigo.ConceitoMaioriaAbsoluta, VersaoV1, TipoRegra.RegraConsolidacao,
            """{"corretores_distintos":3,"criterio":"maioria_absoluta","naturezas_do_conceito":["categorico"],"quando_nao_consolida":"novo_corretor"}""",
            """["cada corretor lança o conceito sem conhecer o dos demais","o conceito da etapa é o valor em que o critério converge: um valor com mais da metade das atribuições","quando o critério não converge, o tratamento que a versão declara é aplicado"]""",
            "Decisão institucional da Unifesspa"),

        // regra_procedimento_recurso — como o recurso de uma matéria é revisado. Rol fechado: o edital escolhe
        // um procedimento e a escolha congela com a configuração; a declaração da regra registra o que a versão declara de si
        // (o que revisa, a evidência e de onde vêm os corretores) e não admite parâmetro que descreva passos.
        new(SeedId(33), RegraProcedimentoRecursoCodigo.Recorrecao, VersaoV1, TipoRegra.RegraProcedimentoRecurso,
            """{"revisa":"desempenho avaliado por pessoas","evidencia":"nova correção cega do material","corretores":"os_da_regra_de_consolidacao_da_etapa"}""",
            """["a revisão é uma nova correção do material, feita sem conhecer a nota anterior","os corretores são os da regra de consolidação da etapa"]""",
            "UNI-REQ-0383 (procedimento de recurso por matéria) e UNI-REQ-0097 (correção cega)"),

        new(SeedId(34), RegraProcedimentoRecursoCodigo.Conferencia, VersaoV1, TipoRegra.RegraProcedimentoRecurso,
            """{"revisa":"comprovação de um fato e aplicação da pontuação prevista por tabela","evidencia":"o documento e a tabela","corretores":"os_da_regra_de_consolidacao_da_etapa_quando_ha_consolidacao_humana"}""",
            """["a revisão confere o documento contra a tabela do edital","há corretores só quando a etapa tem consolidação humana; do contrário, nenhum"]""",
            "UNI-REQ-0383 (procedimento de recurso por matéria)"),

        new(SeedId(35), RegraProcedimentoRecursoCodigo.CorrecaoMaterial, VersaoV1, TipoRegra.RegraProcedimentoRecurso,
            """{"revisa":"erro de registro (transcrição, soma, presença)","evidencia":"o registro e a evidência do erro","corretores":"nenhum"}""",
            """["a correção tem sempre um motivo da lista da etapa, sem a opção outro","não é saída genérica para o que a derivação da nota não explica"]""",
            "UNI-REQ-0383 (procedimento de recurso por matéria) e UNI-REQ-0281"),

        new(SeedId(36), RegraProcedimentoRecursoCodigo.Recalculo, VersaoV1, TipoRegra.RegraProcedimentoRecurso,
            """{"revisa":"cálculo sobre insumos já conferidos (gabarito, média ponderada do ENEM, classificação)","evidencia":"os dados registrados na versão publicada e o boletim","corretores":"nenhum"}""",
            """["o recálculo parte dos dados registrados na versão publicada, sem nova correção","a nota do ENEM é recalculada à vista do boletim"]""",
            "UNI-REQ-0383 (procedimento de recurso por matéria)"),
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
    string DeclaracaoDaRegraJson,
    string InvariantesJson,
    string BaseLegal)
{
    /// <summary>Computa o hash canônico da definição (<see cref="HashCanonicalComputer.ComputeRegraCatalogo"/>).</summary>
    public string ComputarHash() => RegraCatalogo.Criar(
        Codigo,
        Versao,
        Tipo,
        ParseElemento(DeclaracaoDaRegraJson),
        ParseElemento(InvariantesJson),
        BaseLegal).Hash;

    private static JsonElement ParseElemento(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
