namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using Xunit;

/// <summary>
/// Issue #1068 — as cláusulas de um predicado DNF (<c>condicaoGatilho</c>, pré-condição de fato,
/// <c>quando</c> de derivação, predicado de desempate) passam a ordenar pelos bytes da cláusula
/// já canonicalizada, não pelo ordinal <c>Clausula</c> que quem envia a configuração escolhe
/// livremente para AGRUPAR os átomos. Os arrays de alternativas sob operador de pertencimento
/// (<c>EM</c>/<c>NAO_EM</c>) passam a ordenar do mesmo jeito.
/// </summary>
/// <remarks>
/// <para>
/// Cada teste tem discriminante PRÓPRIO — não existe mutante único que derrube todos: a troca de
/// ordinais discrimina a ordenação antiga por <c>Clausula</c>; o anti-achatamento discrimina um
/// <c>GroupBy</c> removido; as alternativas permutadas discriminam o auxiliar de valor por
/// operador ausente em cada um dos dois pontos de código (o método consolidado e o desempate).
/// </para>
/// <para>
/// Como em <see cref="OrdenacaoDeConjuntosCanonicosTests"/>, a comparação é sempre de FRAGMENTO,
/// nunca do envelope inteiro: dois processos montados separadamente recebem Guids distintos em
/// oferta/modalidade/etc., e comparar o envelope inteiro deixaria passar uma implementação que
/// achatasse as cláusulas — a diferença apareceria como vindo dos identificadores, não do
/// predicado.
/// </para>
/// </remarks>
public sealed class PredicadoDnfOrdenacaoCanonicaTests
{
    private static readonly SnapshotPublicacaoCanonicalizer Canonicalizador = new();

    private static ReferenciaRegra Regra(string codigo, char semente) =>
        ReferenciaRegra.Criar(codigo, "v1", new string(semente, 64)).Value!;

    private static DadosEdital Dados() => DadosEdital.Criar(
        numero: "001/2026",
        periodoInscricaoInicio: new DateOnly(2026, 1, 1),
        periodoInscricaoFim: new DateOnly(2026, 1, 31),
        documentoEditalId: Guid.CreateVersion7()).Value!;

    private static EntradaCanonicalizacao Entrada(ProcessoSeletivo processo) =>
        new(processo, Dados(), new string('0', 64), FusoInstitucional.ZoneId);

    private static JsonElement Escalar(string valor) => JsonSerializer.SerializeToElement(valor);

    private static JsonElement Alternativas(params string[] valores) => JsonSerializer.SerializeToElement(valores);

    /// <summary>
    /// Um processo mínimo, válido para <see cref="SnapshotPublicacaoCanonicalizer.Canonicalizar"/>,
    /// com uma única exigência CONDICIONAL carregando o gatilho <paramref name="condicoes"/> —
    /// mesmo esqueleto de <see cref="OrdenacaoDeConjuntosCanonicosTests.MontarProcesso"/>, mas com
    /// o gatilho como o knob variável em vez dos conjuntos da issue #1067.
    /// </summary>
    private static ProcessoSeletivo MontarProcessoComGatilho(IReadOnlyList<CondicaoGatilho> condicoes)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            "PS Ordenação de Predicados", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);

        processo.DefinirEtapas([
            EtapaProcesso.Criar("Prova Objetiva", CaraterEtapa.Classificatoria, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva").Value!, peso: 1m, ordem: 1),
        ], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirOfertaAtendimento(
            OfertaAtendimentoEspecializado.Criar([], [], []).Value!, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        ModalidadeSelecionada modalidade = ModalidadeSelecionada.Criar(
            modalidadeOrigemId: Guid.CreateVersion7(),
            codigo: "AC",
            descricao: null,
            naturezaLegal: NaturezaLegalModalidade.Ampla,
            composicaoVagas: ComposicaoVagasModalidade.ResidualDoVo,
            composicaoOrigemCodigo: null,
            regraRemanejamento: RegraRemanejamentoModalidade.Nenhuma,
            remanejamentoDestino: null,
            remanejamentoPar: null,
            remanejamentoFallback: null,
            criteriosCumulativos: [],
            acaoQuandoIndeferido: null,
            baseLegal: "Res. Unifesspa 532/2021",
            quantidadeDeclarada: 40).Value!;

        ConfiguracaoDistribuicaoVagas distribuicao = ConfiguracaoDistribuicaoVagas.Criar(
            ofertaCursoOrigemId: Guid.CreateVersion7(),
            voBase: 40,
            pr: 1m,
            regraDistribuicao: Regra(RegraDistribuicaoVagasCodigo.Institucional, 'a'),
            regraAjuste: null,
            referenciaDemografica: null,
            modalidades: [modalidade]).Value!;
        processo.DefinirDistribuicaoVagas([distribuicao], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirClassificacao(ConfiguracaoClassificacao.Criar(
            regraCalculo: Regra(RegraCalculoCodigo.ClassificacaoImportada, 'b'),
            regraArredondamento: null,
            casasArredondamento: null,
            regraOrdemAlocacao: Regra(RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, 'c'),
            nOpcoesAlocacao: 1,
            regrasEliminacao: [],
            baseadoEmEnem: false).Value!, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        FaseCronograma fase = FaseCronograma.Criar(
            ordem: 1,
            faseCanonicaOrigemId: Guid.CreateVersion7(),
            codigo: "INSCRICAO",
            donoInstitucional: "CEPS",
            origemData: OrigemDataFase.Propria,
            agrupaEtapas: true,
            permiteComplementacao: true,
            produzResultado: true,
            resultadoDefinitivo: true,
            coletaInscricao: true,
            inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero),
            atoProduzidoCodigo: "INSCRICAO",
            atoProduzidoEfeitoIrreversivel: false,
            bancasRequeridas: [],
            regraRecurso: null).Value!;
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        FormatosPermitidos qualquer = FormatosPermitidos.Criar(true, null).Value!;

        // obrigatorio: false, consequenciaIndeferimento: null — DeterminaResultado() fica falso,
        // então nem PendenciaDasExigenciasDocumentais (CA-01) nem a checagem de base legal de
        // AvaliadorConformidadeLegal exigem nada além do gatilho em si (mesmo raciocínio já
        // documentado em EnvelopeCodecRoundTripTests.RoundTrip14_ArvoreComGrupoCardinalidadeERepeticao).
        DocumentoExigido documento = DocumentoExigido.Criar(
            fase.Id, Guid.CreateVersion7(), "RG", "Documento de identidade", "PESSOAL",
            Aplicabilidade.Condicional, obrigatorio: false, consequenciaIndeferimento: null,
            condicoes, [], null, qualquer, null).Value!;

        processo.DefinirDocumentosExigidos(
            [NoExigencia.CriarFolha(documento, 0).Value!], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirTaxaInscricao(
            ConfiguracaoTaxaInscricao.Criar(cobra: false, valor: null, fundamentosCodigos: null, confirmacaoFundamentos: false).Value!,
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        return processo;
    }

    private static JsonNode CondicaoGatilhoDoEnvelope(SnapshotCanonico snapshot) =>
        EnvelopeCodecRoundTripTests.Envelope(snapshot)["documentosExigidos"]!["exigencias"]![0]!["condicaoGatilho"]!;

    // ── Teste central: o ordinal do cliente sai do digest ──

    /// <summary>
    /// O discriminante central da issue: o MESMO predicado — mesma partição em cláusulas, mesmo
    /// conteúdo — enviado com ordinais DIFERENTES tem de produzir os MESMOS bytes. Fundido com o
    /// cenário "cláusulas em ordem inversa" do corpo da issue (§5 do plano): tomado ao pé da
    /// letra, inverter só a ENUMERAÇÃO mantendo os mesmos ordinais já produz os mesmos bytes hoje
    /// (o código agrupa e ordena pelo ordinal) — vacuamente verde. O que discrimina de fato é
    /// TROCAR os ordinais entre as cláusulas inteiras, preservando a partição: aqui a config 2 tem
    /// as mesmas duas cláusulas da config 1 — {FATO_A, FATO_B} e {FATO_C} — mas com os ordinais 1
    /// e 2 TROCADOS entre elas, e a lista de entrada também em outra ordem.
    /// </summary>
    [Fact(DisplayName = "condicaoGatilho: trocar os ordinais entre cláusulas inteiras, preservando a partição, produz os MESMOS bytes")]
    public void CondicaoGatilho_TrocaDeOrdinaisEntreClausulas_ProduzMesmosBytes()
    {
        CondicaoGatilho a1 = CondicaoGatilho.Criar(1, "FATO_A", Operador.Igual, Escalar("valorA")).Value!;
        CondicaoGatilho b1 = CondicaoGatilho.Criar(1, "FATO_B", Operador.Igual, Escalar("valorB")).Value!;
        CondicaoGatilho c1 = CondicaoGatilho.Criar(2, "FATO_C", Operador.Igual, Escalar("valorC")).Value!;
        // Config 1: clausula 1 = {FATO_A, FATO_B}; clausula 2 = {FATO_C}.

        CondicaoGatilho c2 = CondicaoGatilho.Criar(1, "FATO_C", Operador.Igual, Escalar("valorC")).Value!;
        CondicaoGatilho b2 = CondicaoGatilho.Criar(2, "FATO_B", Operador.Igual, Escalar("valorB")).Value!;
        CondicaoGatilho a2 = CondicaoGatilho.Criar(2, "FATO_A", Operador.Igual, Escalar("valorA")).Value!;
        // Config 2: MESMA partição — {FATO_A, FATO_B} e {FATO_C} — mas os ordinais 1 e 2 trocados
        // de cláusula, e listados em ordem diferente.

        // Pré-condição do discriminante: ordenar pelo ordinal (a política ANTIGA) dá clausulas em
        // ordem OPOSTA entre as duas configs — é isso que prova que o teste discrimina de fato.
        static string ClausulaDeMenorOrdinal(IEnumerable<CondicaoGatilho> condicoes) => condicoes
            .GroupBy(static c => c.Clausula)
            .OrderBy(static g => g.Key)
            .First()
            .Select(static c => c.Fato)
            .OrderBy(static f => f, StringComparer.Ordinal)
            .First();

        ClausulaDeMenorOrdinal([a1, b1, c1]).Should().Be("FATO_A",
            "pré-condição: em config 1, a cláusula de MENOR ordinal (1) é a conjunção FATO_A e FATO_B");
        ClausulaDeMenorOrdinal([c2, b2, a2]).Should().Be("FATO_C",
            "pré-condição do discriminante: em config 2, a cláusula de MENOR ordinal (1) é só FATO_C — " +
            "o OPOSTO de config 1. Ordenar pelo ordinal (política antiga) produziria arrays em ordem " +
            "trocada para o mesmo predicado");

        ProcessoSeletivo processoUm = MontarProcessoComGatilho([a1, b1, c1]);
        ProcessoSeletivo processoDois = MontarProcessoComGatilho([c2, b2, a2]);

        JsonNode gatilhoUm = CondicaoGatilhoDoEnvelope(Canonicalizador.Canonicalizar(Entrada(processoUm)));
        JsonNode gatilhoDois = CondicaoGatilhoDoEnvelope(Canonicalizador.Canonicalizar(Entrada(processoDois)));

        OrdenacaoDeConjuntosCanonicosTests.BytesDoFragmento(gatilhoDois).Should().Equal(
            OrdenacaoDeConjuntosCanonicosTests.BytesDoFragmento(gatilhoUm),
            "o mesmo predicado — mesma partição em cláusulas, mesmo conteúdo — enviado com ordinais " +
            "diferentes tem de produzir os mesmos bytes: o ordinal só agrupa, nunca ordena");
    }

    // ── Anti-achatamento: agrupamentos distintos dos mesmos átomos são predicados DIFERENTES ──

    /// <summary>
    /// O risco número um da correção: achatar as cláusulas destruiria a semântica do predicado em
    /// silêncio. Três átomos distintos, valores escalares (para não misturar com a regra de
    /// conjuntos de EM/NAO_EM), o MESMO multiconjunto nas duas configs — só o agrupamento muda:
    /// P1 = (FATO_A E FATO_B) OU (FATO_C); P2 = (FATO_A E FATO_C) OU (FATO_B). Um mutante que
    /// removesse o <c>GroupBy</c> e achatasse tudo num nível só produziria os MESMOS bytes para os
    /// dois — é exatamente isso que este teste reprova.
    /// </summary>
    [Fact(DisplayName = "condicaoGatilho: agrupamentos DIFERENTES dos mesmos átomos produzem bytes DIFERENTES — a correção não achata as cláusulas")]
    public void CondicaoGatilho_AgrupamentosDistintosDosMesmosAtomos_ProduzBytesDiferentes()
    {
        CondicaoGatilho a1 = CondicaoGatilho.Criar(10, "FATO_A", Operador.Igual, Escalar("valorA")).Value!;
        CondicaoGatilho b1 = CondicaoGatilho.Criar(10, "FATO_B", Operador.Igual, Escalar("valorB")).Value!;
        CondicaoGatilho c1 = CondicaoGatilho.Criar(20, "FATO_C", Operador.Igual, Escalar("valorC")).Value!;
        // P1 = (FATO_A E FATO_B) OU (FATO_C)

        CondicaoGatilho a2 = CondicaoGatilho.Criar(10, "FATO_A", Operador.Igual, Escalar("valorA")).Value!;
        CondicaoGatilho c2 = CondicaoGatilho.Criar(10, "FATO_C", Operador.Igual, Escalar("valorC")).Value!;
        CondicaoGatilho b2 = CondicaoGatilho.Criar(20, "FATO_B", Operador.Igual, Escalar("valorB")).Value!;
        // P2 = (FATO_A E FATO_C) OU (FATO_B) — MESMOS três átomos, agrupamento DIFERENTE.

        ProcessoSeletivo processoUm = MontarProcessoComGatilho([a1, b1, c1]);
        ProcessoSeletivo processoDois = MontarProcessoComGatilho([a2, c2, b2]);

        JsonNode gatilhoUm = CondicaoGatilhoDoEnvelope(Canonicalizador.Canonicalizar(Entrada(processoUm)));
        JsonNode gatilhoDois = CondicaoGatilhoDoEnvelope(Canonicalizador.Canonicalizar(Entrada(processoDois)));

        static IReadOnlyList<(string Fato, string Valor)> AtomosAchatados(JsonNode gatilho) => [.. gatilho.AsArray()
            .SelectMany(static clausula => clausula!.AsArray())
            .Select(static atomo => (atomo!["fato"]!.GetValue<string>(), atomo["valor"]!.GetValue<string>()))
            .OrderBy(static atomo => atomo.Item1, StringComparer.Ordinal)];

        AtomosAchatados(gatilhoDois).Should().Equal(AtomosAchatados(gatilhoUm),
            "pré-condição do discriminante: os dois predicados têm exatamente o MESMO multiconjunto de " +
            "átomos achatado — só o agrupamento em cláusulas muda entre P1 e P2");

        OrdenacaoDeConjuntosCanonicosTests.BytesDoFragmento(gatilhoDois).Should().NotEqual(
            OrdenacaoDeConjuntosCanonicosTests.BytesDoFragmento(gatilhoUm),
            "P1 = (FATO_A E FATO_B) OU (FATO_C) e P2 = (FATO_A E FATO_C) OU (FATO_B) são predicados " +
            "logicamente DIFERENTES, mesmo achatando para o mesmo multiconjunto de átomos — achatar as " +
            "cláusulas antes de ordenar destruiria essa diferença em silêncio");
    }

    // ── D3: alternativas de EM/NAO_EM ordenam pela chave de conteúdo, nos DOIS pontos de código ──

    /// <summary>
    /// Ponto 1 — o método CONSOLIDADO (<c>SerializarDnf</c>, usado por <c>condicaoGatilho</c>,
    /// pré-condição de fato e <c>quando</c> de derivação). Parametrizado por
    /// <see cref="Operador.Em"/> e <see cref="Operador.NaoEm"/> — os DOIS operadores de
    /// pertencimento, não só um: um guarda em <c>SerializarValorDeAtomo</c> que reconhecesse
    /// apenas <c>Em</c> deixaria <c>NAO_EM</c> sem ordenar (o array cairia no ramo escalar,
    /// preservado tal como chegou) e este teste, com um só operador coberto, não pegaria isso.
    /// Alternativas deliberadamente fora de ordem alfabética.
    /// </summary>
    [Theory(DisplayName = "condicaoGatilho: alternativas de pertencimento (EM/NAO_EM) ordenam pela chave de conteúdo — ponto de código consolidado")]
    [InlineData(Operador.Em)]
    [InlineData(Operador.NaoEm)]
    public void CondicaoGatilho_AlternativasDePertencimento_OrdenamPelaChaveDeConteudo(Operador operador)
    {
        CondicaoGatilho condicaoUm = CondicaoGatilho.Criar(
            0, "COR_RACA", operador, Alternativas("PRETA", "PARDA", "AMARELA")).Value!;
        JsonNode gatilhoUm = CondicaoGatilhoDoEnvelope(
            Canonicalizador.Canonicalizar(Entrada(MontarProcessoComGatilho([condicaoUm]))));

        gatilhoUm[0]![0]!["valor"]!.AsArray().Select(static v => v!.GetValue<string>()).Should().Equal(
            ["AMARELA", "PARDA", "PRETA"],
            "'A' (0x41) < 'PA' (0x50 0x41) < 'PR' (0x50 0x52) — ordem alfabética ASCII simples, escrita à mão");

        // CA-02 — a entrada permutada produz os MESMOS bytes.
        CondicaoGatilho condicaoDois = CondicaoGatilho.Criar(
            0, "COR_RACA", operador, Alternativas("AMARELA", "PRETA", "PARDA")).Value!;
        JsonNode gatilhoDois = CondicaoGatilhoDoEnvelope(
            Canonicalizador.Canonicalizar(Entrada(MontarProcessoComGatilho([condicaoDois]))));

        OrdenacaoDeConjuntosCanonicosTests.BytesDoFragmento(gatilhoDois).Should().Equal(
            OrdenacaoDeConjuntosCanonicosTests.BytesDoFragmento(gatilhoUm),
            "as mesmas três alternativas em ordem de entrada diferente produzem o mesmo array canônico");
    }

    /// <summary>
    /// Ponto 2 — o consumidor de DESEMPATE (<see cref="ArgsDesempatePredicadoFato"/>). Os testes
    /// do ponto 1 provam a consolidação de D1 (<c>SerializarDnf</c>), mas NÃO cobrem este segundo
    /// ponto de serialização de valor: uma implementação que aplicasse o auxiliar só no método
    /// consolidado passaria em todos eles e deixaria o desempate emitindo <c>["PRETA","PARDA"]</c>
    /// e <c>["PARDA","PRETA"]</c> como bytes distintos — exatamente o que este teste, sozinho,
    /// pegaria. Parametrizado pelos mesmos dois operadores do ponto 1: são dois consumidores e
    /// dois operadores, e só a matriz completa (2×2) garante que nenhuma célula fica descoberta.
    /// </summary>
    [Theory(DisplayName = "criteriosDesempate (PredicadoFato): alternativas de pertencimento (EM/NAO_EM) ordenam pela chave de conteúdo — ponto de código do desempate")]
    [InlineData(Operador.Em)]
    [InlineData(Operador.NaoEm)]
    public void CriterioDesempatePredicadoFato_AlternativasDePertencimento_OrdenamPelaChaveDeConteudo(Operador operador)
    {
        ProcessoSeletivo MontarComDesempate(JsonElement valor)
        {
            ProcessoSeletivo processo = MontarProcessoComGatilho([]);
            ArgsDesempatePredicadoFato args = new(CondicaoDnf.Criar("COR_RACA", operador, valor).Value!);
            processo.DefinirCriteriosDesempate(
                [CriterioDesempate.Criar(1, Regra(CriterioDesempateCodigo.PredicadoFato, 'e'), args).Value!],
                PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();
            return processo;
        }

        JsonNode ArgsDoEnvelope(SnapshotCanonico snapshot) =>
            EnvelopeCodecRoundTripTests.Envelope(snapshot)["criteriosDesempate"]![0]!["args"]!["valor"]!;

        JsonNode valorUm = ArgsDoEnvelope(
            Canonicalizador.Canonicalizar(Entrada(MontarComDesempate(Alternativas("PRETA", "PARDA")))));

        valorUm.AsArray().Select(static v => v!.GetValue<string>()).Should().Equal(
            ["PARDA", "PRETA"], "'PA' (0x50 0x41) < 'PR' (0x50 0x52) — ordem alfabética ASCII simples");

        JsonNode valorDois = ArgsDoEnvelope(
            Canonicalizador.Canonicalizar(Entrada(MontarComDesempate(Alternativas("PARDA", "PRETA")))));

        OrdenacaoDeConjuntosCanonicosTests.BytesDoFragmento(valorDois).Should().Equal(
            OrdenacaoDeConjuntosCanonicosTests.BytesDoFragmento(valorUm),
            "['PRETA','PARDA'] e ['PARDA','PRETA'] são a MESMA alternativa em ordens diferentes — o " +
            "consumidor de desempate tem de ordenar pela chave de conteúdo, não só o método consolidado");
    }

    // ── Round-trip: ordinais não contíguos, a partir de configuração VIVA ──

    /// <summary>
    /// O decodificador atribui <c>Clausula</c> pela POSIÇÃO no array (índice do laço) — o envelope
    /// não carrega o ordinal original, só arrays posicionais. Um envelope congelado a partir de
    /// ordinais <c>7, 4, 9</c> é reidratado com ordinais <c>0, 1, 2</c>; a recodificação reagrupa e
    /// reordena por conteúdo, chegando aos MESMOS bytes — mas isso é uma afirmação a PROVAR, não a
    /// assumir (Fase 0 do plano). Ao menos uma cláusula de DOIS átomos: só cláusulas unitárias não
    /// provam preservação da conjunção (a reidratação de uma cláusula de um átomo só não
    /// discriminaria "agrupar corretamente" de "criar uma cláusula por átomo").
    /// </summary>
    [Fact(DisplayName = "condicaoGatilho: round-trip a partir de ordinais não contíguos (7, 4, 9) — reidratar reatribui por posição e recanonicalizar reproduz os bytes")]
    public void CondicaoGatilho_RoundTripComOrdinaisNaoContiguos_ReproduzOsBytes()
    {
        CondicaoGatilho clausula7A = CondicaoGatilho.Criar(7, "FATO_A", Operador.Igual, Escalar("valorA")).Value!;
        CondicaoGatilho clausula7B = CondicaoGatilho.Criar(7, "FATO_B", Operador.Igual, Escalar("valorB")).Value!;
        CondicaoGatilho clausula4 = CondicaoGatilho.Criar(4, "FATO_C", Operador.Igual, Escalar("valorC")).Value!;
        CondicaoGatilho clausula9 = CondicaoGatilho.Criar(9, "FATO_D", Operador.Igual, Escalar("valorD")).Value!;

        ProcessoSeletivo processo = MontarProcessoComGatilho([clausula7A, clausula7B, clausula4, clausula9]);

        DadosEdital dados = Dados();
        const string hashDocumento = "3333333333333333333333333333333333333333333333333333333333333333";
        SnapshotCanonico congelado = Canonicalizador.Canonicalizar(new EntradaCanonicalizacao(processo, dados, hashDocumento, FusoInstitucional.ZoneId));

        Result<VersaoConfiguracao> publicacao = processo.Publicar(
            dados, congelado.Bytes, congelado.SchemaVersion, congelado.AlgoritmoHash,
            hashDocumento, "user-sub-predicado-1068", TimeProvider.System);
        publicacao.IsSuccess.Should().BeTrue(publicacao.Error?.Message);
        VersaoConfiguracao v1 = publicacao.Value!;

        Result<EnvelopeReidratado> reidratado = CorpusEnvelope.Registro.Reidratar(v1);
        reidratado.IsSuccess.Should().BeTrue(reidratado.Error?.Message);

        // A afirmação de convergência, provada diretamente: o decoder reatribuiu Clausula por
        // posição (0, 1, 2 — três cláusulas no array, não os ordinais 7/4/9 originais), e a
        // cláusula de dois átomos permanece uma conjunção só, não duas cláusulas separadas.
        DocumentoExigido exigenciaReidratada = reidratado.Value!.Grafo.DocumentosExigidos.Single();
        exigenciaReidratada.Condicoes.Select(static c => c.Clausula).Distinct().Should().BeEquivalentTo([0, 1, 2],
            "o decoder atribui Clausula pela posição no array — três cláusulas no envelope, nunca os " +
            "ordinais originais 7/4/9, que não sobrevivem à canonicalização");
        exigenciaReidratada.Condicoes.GroupBy(static c => c.Clausula).Should().ContainSingle(
            static g => g.Count() == 2,
            "a cláusula de dois átomos (FATO_A, FATO_B) tem de permanecer uma ÚNICA conjunção após o " +
            "round-trip, não duas cláusulas de um átomo cada");

        processo.RestaurarConfiguracaoCongelada(v1, reidratado.Value.Grafo).IsSuccess.Should().BeTrue();

        Result<SnapshotCanonico> recodificado = CorpusEnvelope.Registro.Recodificar(
            v1.SchemaVersion,
            new EntradaCanonicalizacao(
                processo, reidratado.Value.Dados, reidratado.Value.HashDocumento, FusoInstitucional.ZoneId, reidratado.Value.Retificacao,
                reidratado.Value.Conformidade, reidratado.Value.MetadadosFatosCongelados,
                reidratado.Value.ValoresSelecionaveisCongelados));
        recodificado.IsSuccess.Should().BeTrue(recodificado.Error?.Message);

        recodificado.Value!.Bytes.Should().Equal(congelado.Bytes,
            "reidratar um envelope com ordinais não contíguos e recanonicalizar tem de reproduzir os " +
            "bytes congelados inteiros — o regrupamento pela posição reidratada converge para a mesma " +
            "forma canônica que os ordinais originais já produziam");
    }
}
