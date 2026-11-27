namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Tempo;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Cenarios;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Agregado de referência da <b>golden fixture do envelope canônico</b> (ADR-0109 D2/D3) e as
/// entradas que o canonicalizam: conforme, com as dimensões reais preenchidas.
/// </summary>
/// <remarks>
/// A cobertura da exigência documental rica, dos metadados de fatos e da referência temporal
/// existe para que a fixture exercite de fato a serialização das exigências, das condições de
/// gatilho, das bases legais, do prazo de emissão e da validade — uma <c>exigencias[]</c> sempre
/// vazia não provaria nada sobre a forma.
/// </remarks>
internal static class EnvelopeDeReferencia
{
    /// <summary>Exposto para <c>EnvelopeCodecRoundTripTests.GoldenRica12_*</c> — mesmo hash fixo, mesma fixture.</summary>
    internal static readonly string HashFixo = new('a', 64);

    /// <summary>Ids de referência cross-módulo (snapshot-copy, ADR-0061): <b>conteúdo</b>, nunca normalizados.</summary>
    internal static readonly Guid OfertaCursoFixa = new("11111111-1111-1111-1111-111111111111");
    internal static readonly Guid ModalidadeFixa = new("22222222-2222-2222-2222-222222222222");
    internal static readonly Guid DocumentoFixo = new("33333333-3333-3333-3333-333333333333");
    internal static readonly Guid TipoDocumentoFixo = new("44444444-4444-4444-4444-444444444444");

    internal static readonly SnapshotPublicacaoCanonicalizer Canonicalizer = new();

    internal static ReferenciaRegra Regra(string codigo, string hashSeed) =>
        ReferenciaRegra.De(codigo, "v1", new string(hashSeed[0], 64));

    /// <param name="comBancasExigidasNaPublicacao">Opt-in: acrescenta a banca de homologação das inscrições que a publicação exige; a golden fixture não a tem.</param>
    internal static ProcessoSeletivo ProcessoDeReferencia(bool comBancasExigidasNaPublicacao = false)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Referencia 2026", TiposDeProcessoDeTeste.Sisu, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "PRO_REITORIA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());

        DefinirVagasEClassificacao(processo);
        FaseCronograma fase = DefinirCronogramaEEtapas(processo, comBancasExigidasNaPublicacao);
        DefinirDocumentosEFatos(processo, fase);
        DefinirFormularioEDerivacao(processo);
        DefinirTaxaDeInscricao(processo);
        return processo;
    }

    internal static DadosEdital DadosDeReferencia() => DadosEdital.Criar(
        numero: "001/2026",
        periodoInscricaoInicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(-3)),
        periodoInscricaoFim: new DateTimeOffset(2026, 1, 31, 23, 59, 59, TimeSpan.FromHours(-3)).AddTicks(9_999_990),
        documentoEditalId: DocumentoFixo).Value!;

    /// <summary>
    /// Metadado dos fatos citados na condição de gatilho do documento de referência
    /// (congelamento dos parâmetros na publicação, UNI-REQ-0019) — exercita <c>metadadosFatos</c> com DOIS itens reais.
    /// MODALIDADE é derivado e de escopo-processo (ADR-0116): seus valores vêm da oferta congelada,
    /// então <c>valoresDominio</c> e <c>valoresDominioDeclarados</c> são nulos. COR_RACA é categórico
    /// ESTÁTICO: <c>valoresDominioDeclarados</c> é populado — a variante que, sem este fato no
    /// gatilho, não teria cobertura alguma na golden fixture.
    /// </summary>
    internal static Dictionary<string, MetadadoFatoCongelado> MetadadosFatosDeReferencia() =>
        new(StringComparer.Ordinal)
        {
            ["MODALIDADE"] = MetadadoDaModalidade(),
            ["COR_RACA"] = MetadadoDaCorRaca(),
        };

    /// <summary>
    /// Os valores selecionáveis dos dois fatos coletados do processo de referência (COR_RACA,
    /// RENDA — ambos <c>ESCOLHA_UNICA</c>) — totalidade exigida pelo encoder (D1-ter).
    /// COR_RACA reusa o MESMO vocabulário dos metadados: é o mesmo fato do catálogo, congelado nos dois blocos.
    /// </summary>
    internal static Dictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?> ValoresSelecionaveisDeReferencia() =>
        CatalogoDoConjuntoBasico.ComValoresCongelados(new Dictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?>(StringComparer.Ordinal)
        {
            ["COR_RACA"] = ValoresDaCorRacaForaDeOrdem(),
            ["RENDA"] =
            [
                new ValorDominioDeclaradoCongelado("ATE_1_SM", "Renda familiar per capita de até 1 salário mínimo.", 0),
                new ValorDominioDeclaradoCongelado("ACIMA_1_SM", "Renda familiar per capita acima de 1 salário mínimo.", 1),
            ],
        });

    internal static EntradaCanonicalizacao EntradaDeReferencia(
        ProcessoSeletivo processo,
        DadosEdital dados,
        IReadOnlyDictionary<string, MetadadoFatoCongelado>? metadados = null,
        IReadOnlyDictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?>? valoresSelecionaveis = null) =>
        new(processo, dados, HashFixo, FusoInstitucional.ZoneId, Retificacao: null, Conformidade: null, DadosDeCatalogo: new DadosDeCatalogoDaConfiguracaoPublicada(MetadadosFatosCongelados: metadados, ValoresSelecionaveisCongelados: valoresSelecionaveis,
            TiposDeAtoCongelados: TiposDeAtoCongeladosDeTeste.Dos(processo), FatosDerivadosCongelados: FatosDerivadosCongeladosDeTeste.Padrao));

    internal static SnapshotCanonico CanonicalizarReferencia() =>
        Canonicalizer.Canonicalizar(new EntradaCanonicalizacao(ProcessoDeReferencia(), DadosDeReferencia(), HashFixo, FusoInstitucional.ZoneId, Retificacao: null, Conformidade: null, DadosDeCatalogo: new DadosDeCatalogoDaConfiguracaoPublicada(MetadadosFatosCongelados: MetadadosFatosDeReferencia(), ValoresSelecionaveisCongelados: ValoresSelecionaveisDeReferencia(), ApresentacaoDosPressupostos: ExemploDeProcessoSeletivoEntrada.ApresentacaoRica, TiposDeAtoCongelados: TiposDeAtoCongeladosDeTeste.Dos(ProcessoDeReferencia()), FatosDerivadosCongelados: FatosDerivadosCongeladosDeTeste.Padrao)));

    /// <summary>
    /// O agregado de referência COM a cascata de remanejamento configurada — a matriz
    /// legal completa (8×7, fallback AC). A oferta de referência é institucional (não federal): a
    /// canonicalização é pura (ADR-0109 D6) e não valida <c>PendenciaDaCascata</c>, então este
    /// agregado não precisa ser publicável — ele só existe para congelar a forma
    /// <c>presente:true</c> do bloco <c>ordemDeRemanejamento</c>.
    /// </summary>
    internal static SnapshotCanonico CanonicalizarReferenciaComCascata() =>
        Canonicalizer.Canonicalizar(new EntradaCanonicalizacao(ProcessoDeReferenciaComCascata(), DadosDeReferencia(), HashFixo, FusoInstitucional.ZoneId, Retificacao: null, Conformidade: null, DadosDeCatalogo: new DadosDeCatalogoDaConfiguracaoPublicada(MetadadosFatosCongelados: MetadadosFatosDeReferencia(), ValoresSelecionaveisCongelados: ValoresSelecionaveisDeReferencia(), TiposDeAtoCongelados: TiposDeAtoCongeladosDeTeste.Dos(ProcessoDeReferenciaComCascata()), FatosDerivadosCongelados: FatosDerivadosCongeladosDeTeste.Padrao)));

    internal static JsonObject EnvelopeComoObjeto() =>
        JsonNode.Parse(Encoding.UTF8.GetString(CanonicalizarReferencia().Bytes))!.AsObject();

    private static ProcessoSeletivo ProcessoDeReferenciaComCascata()
    {
        ProcessoSeletivo processo = ProcessoDeReferencia();
        processo.DefinirCascataRemanejamento(CascataDeReferencia()).IsSuccess.Should().BeTrue();
        return processo;
    }

    /// <summary>A matriz legal completa (8×7), fallback AC — mesma forma semeada em REMANEJAMENTO_ORDEM_LEI_12711 v1.</summary>
    private static ConfiguracaoCascataRemanejamento CascataDeReferencia()
    {
        IReadOnlyList<string> origens = ModalidadesFederaisLei12711.Codigos;
        List<DestinoRemanejamento> destinos = [];
        foreach (string origem in origens)
        {
            string[] destinosDaOrigem = [.. origens.Where(o => o != origem)];
            for (int i = 0; i < destinosDaOrigem.Length; i++)
            {
                destinos.Add(DestinoRemanejamento.Criar(origem, i + 1, destinosDaOrigem[i]).Value!);
            }
        }

        return ConfiguracaoCascataRemanejamento.Criar(
            Regra(CriterioDeRemanejamento.OrdemDaLei12711.Codigo(), "1"), ModalidadesFederaisLei12711.Ac, destinos).Value!;
    }

    private static void DefinirVagasEClassificacao(ProcessoSeletivo processo)
    {
        processo.DefinirAtendimentoEspecializado(
            AtendimentoEspecializado.Criar([], [], []).Value!).IsSuccess.Should().BeTrue();

        processo.DefinirDistribuicaoVagas([DistribuicaoDeReferencia()], FatosDeModalidadeDeTeste.DoCatalogo).IsSuccess.Should().BeTrue();

        processo.DefinirClassificacao(ConfiguracaoClassificacao.Criar(new DeclaracaoDaClassificacao
        {
            RegraCalculo = Regra(RegraDeCalculoDaNota.ClassificacaoImportada.Codigo(), "b"),
            RegraOrdemAlocacao = Regra(RegraDeOrdemDeAlocacao.PrimeiraOpcaoPrioritaria.Codigo(), "c"),
        }).Value!).IsSuccess.Should().BeTrue();
    }

    private static FaseCronograma DefinirCronogramaEEtapas(ProcessoSeletivo processo, bool comBancasExigidasNaPublicacao)
    {
        FaseCronograma fase = FaseDeInscricao(comBancasExigidasNaPublicacao);
        processo.DefinirCronogramaFases([fase], []).IsSuccess.Should().BeTrue();
        processo.DefinirEtapas([
            EtapaProcesso.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("etapa").Value!,
                new IdentificacaoDaEtapa(Nome: "Prova Objetiva", Carater: CaraterEtapa.Classificatoria, TipoEtapa: TipoEtapaSnapshot.Criar(new Guid("019fee1e-7000-7000-8000-000000000001"), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, Ordem: 1, FaseCodigo: "INSCRICAO"),
                new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: 10m, PassoDoLancamento: null, FormaDeAvaliacao: FormaDeAvaliacao.Objetiva, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!,
        ]).IsSuccess.Should().BeTrue();
        return fase;
    }

    // Com o documento de referência gatado pela modalidade do candidato, derivada da única
    // ofertada, o snapshot fecha as QUATRO classes de aresta do grafo conjunto (§7.4):
    // produção (campo→fato), pré-condição (COR_RACA gata RENDA), derivação (RENDA→REGIME_DE_VAGA)
    // e gatilho (MODALIDADE→exigência).
    private static void DefinirDocumentosEFatos(ProcessoSeletivo processo, FaseCronograma fase)
    {
        processo.DefinirDocumentosExigidosNosFormulariosMinimos(
            [RegraDeComprovacao.CriarRegraDeDocumento(DocumentoExigidoDeReferencia(fase.Id), 0).Value!])
            .IsSuccess.Should().BeTrue();

        processo.DefinirReferenciaTemporalFatos(
            ReferenciaTemporalFatos.Criar(ReferenciaTipo.DataEspecifica, new DateOnly(2026, 1, 31), null).Value!).IsSuccess.Should().BeTrue();

        processo.DefinirItens([FatoCorRaca(), FatoRendaGatadoPelaCorRaca()]).IsSuccess.Should().BeTrue();
    }

    // O formulário de inscrição (UNI-REQ-0086) traz título e um termo exigido: a
    // fixture congela a forma real do bloco "formularios", não só o caso sem termos.
    private static void DefinirFormularioEDerivacao(ProcessoSeletivo processo)
    {
        processo.DefinirTitulo("Formulário de Inscrição").IsSuccess.Should().BeTrue();
        processo.DefinirTermos([ExemploDeProcessoSeletivoEntrada.Termo("DECLARACAO_VERACIDADE", 0)])
            .IsSuccess.Should().BeTrue();

        processo.DefinirRegrasDerivacao([
            ConfiguracaoDerivacaoFato.Criar("REGIME_DE_VAGA", [
                RegraDerivacaoConfigurada.Criar(0, "AC", null).Value!,
                RegraDerivacaoConfigurada.Criar(1, "AC", [
                    CondicaoRegraDerivacao.Criar(0, "RENDA", Operador.Igual, JsonSerializer.SerializeToElement("ATE_1_SM")).Value!,
                ]).Value!,
            ]).Value!,
        ]).IsSuccess.Should().BeTrue();
    }

    // O exemplo BASE declara "não cobra": a combinação do CA-08 complementar ao
    // exemplo RICO ("cobra com fundamentos").
    private static void DefinirTaxaDeInscricao(ProcessoSeletivo processo)
    {
        processo.DefinirTaxaInscricao(
            ConfiguracaoTaxaInscricao.Criar(cobra: false, valor: null, fundamentosCodigos: null).Value!).IsSuccess.Should().BeTrue();
        DeclaracoesObrigatoriasDeTeste.Declarar(processo);
    }

    private static FatoColetado FatoCorRaca() =>
        FatoColetadoDeTeste.Coletando("COR_RACA", 0)
            .ComRotulo("Cor ou raça")
            .DoTipo(TipoDeCampo.EscolhaUnica)
            .ComObrigatoriedade(Obrigatoriedade.Sempre)
            .Construir();

    private static FatoColetado FatoRendaGatadoPelaCorRaca() =>
        FatoColetadoDeTeste.Coletando("RENDA", 1)
            .ComRotulo("Faixa de renda familiar")
            .DoTipo(TipoDeCampo.EscolhaUnica)
            .ComObrigatoriedade(Obrigatoriedade.Sempre)
            .ExibidoQuando(ExibicaoDeTeste.Quando("COR_RACA", Operador.Igual, "PRETA"))
            .Construir();

    private static FaseCronograma FaseDeInscricao(bool comBancasExigidasNaPublicacao) => FaseCronograma.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
        OrigensCanonicasDeTeste.ComNome(new OrigemCanonicaDaFase(
            OrigemId: Guid.CreateVersion7(), Codigo: "INSCRICAO", OrigemData: OrigemDataFase.NoEdital, AgrupaEtapas: true, PermiteComplementacao: false, ColetaInscricao: true,
            ColetaSolicitacaoIsencao: false)),
        new DecisoesDeCronogramaDaFase(
            Ordem: 1, Inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), Fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero), UnidadeResponsavel: UnidadesDeTeste.Ceps),
        HomologacaoDasInscricoesDeTeste.PublicacoesDaFaseQueHomologa(resultado: "INSCRICAO"),
        bancasRequeridas: comBancasExigidasNaPublicacao ? [ExemploDeProcessoSeletivoCronograma.BancaDeHomologacaoDasInscricoes()] : []).Value!;

    /// <summary>
    /// Exigência CONDICIONAL rica — todas as 12 dimensões da forma 1.2 preenchidas, para que a
    /// golden fixture congele a serialização completa do item.
    /// </summary>
    /// <remarks>
    /// O gatilho cita DOIS fatos na mesma cláusula (AND): <c>MODALIDADE</c> (categórico
    /// derivado/escopo-processo) e <c>COR_RACA</c> (categórico ESTÁTICO — <c>valoresDominioDeclarados</c>
    /// populado). Sem o segundo, a variante populada daquele bloco não teria cobertura alguma.
    /// </remarks>
    private static DocumentoExigido DocumentoExigidoDeReferencia(Guid exigidoNaFaseId) =>
        new Unifesspa.UniPlus.Selecao.Domain.UnitTests.Cenarios.DocumentoExigidoBuilder()
            .ComChave(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("exigencia_comprovante_residencia").Value!)
            .ComTipo(new CopiaDoTipoDeDocumento(TipoDocumentoFixo, "COMPROVANTE_RESIDENCIA", "Comprovante de residência", "PESSOAL", ProtecaoDoDocumentoDeTeste.Pessoal))
            .ComMomento(new MomentoDaExigencia(exigidoNaFaseId, null, FormulariosDoProcessoDeTeste.Codigo(FormulariosDoProcessoDeTeste.Inscricao)))
            .ComAplicabilidade(new AplicabilidadeDaExigencia(Aplicabilidade.Condicional, [GatilhoDaModalidade(), GatilhoDaCorRaca()]))
            .ComIndeferimento(new IndeferimentoDoDocumento(true, "ELIMINA_DO_PROCESSO", [BaseLegalDaResolucao()]))
            .ComRequisitos(new RequisitosDoDocumento(FormatosPdfComTetoEJpegSemTeto(), 5_000_000, ModeloDeDeclaracaoDeResidencia(), PrazoDeNoventaDias(), ValidadeNoFimDasInscricoes()))
            .ComFatos(null)
            .Criar().Value!;

    private static PrazoDeEmissao PrazoDeNoventaDias() => new PrazoDeEmissaoBuilder().Construir();

    private static ValidadeDoDocumento ValidadeNoFimDasInscricoes() => new ValidadeDoDocumentoBuilder().Construir();

    private static FormatosPermitidos FormatosPdfComTetoEJpegSemTeto() =>
        FormatosPermitidos.Criar(qualquer: false, entradas: [("PDF", 5_000_000), ("JPEG", null)]).Value!;

    private static ModeloDaExigencia ModeloDeDeclaracaoDeResidencia() => new(
        Guid.Parse("0199a000-0000-7000-8000-0000000000d1"), "Declaração de residência.odt", FormatoDeDocumento.Odt, new string('d', 64));

    private static CondicaoGatilho GatilhoDaModalidade() => CondicaoGatilho.Criar(
        0, "MODALIDADE", Operador.Igual, JsonSerializer.SerializeToElement("AC")).Value!;

    private static CondicaoGatilho GatilhoDaCorRaca() => CondicaoGatilho.Criar(
        0, "COR_RACA", Operador.AlgumDe, JsonSerializer.SerializeToElement(new[] { "PRETA", "PARDA" })).Value!;

    private static DocumentoExigidoBaseLegal BaseLegalDaResolucao() => DocumentoExigidoBaseLegal.Criar(
        "Res. Unifesspa 532/2021, art. 12", TipoAbrangencia.InternaNorma, StatusBaseLegal.Resolvido, "Norma interna do certame").Value!;

    internal static ConfiguracaoDistribuicaoVagas DistribuicaoDeReferencia() =>
        ConfiguracaoDistribuicaoVagas.Criar(OfertaCursoFixa,
            new DistribuicaoDeVagasDeclarada { VagasOfertadasBase = 40, PercentualDaReservaLegal = 1m, RegraDeDistribuicao = Regra(RegraDeDistribuicaoDeVagas.Institucional.Codigo(), "a"), Modalidades = [ModalidadeAmplaDeReferencia()] },
            new FatosDaOfertaNoCadastro { OfertaCurso = OfertaCursoDeTeste.Medicina }).Value!;

    private static ModalidadeSelecionada ModalidadeAmplaDeReferencia() =>
        ModalidadeSelecionada.Criar(ModalidadesNoCadastroDeTeste.Da("AC", NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.RestanteDasVagasOfertadas) with { Id = ModalidadeFixa, RegraRemanejamento = null, BaseLegal = "Res. Unifesspa 532/2021" }, 40).Value!;

    private static MetadadoFatoCongelado MetadadoDaModalidade() => new(
        Codigo: "MODALIDADE",
        Dominio: TipoDoDado.Opcao,
        Origem: OrigemFato.Calculado,
        Cardinalidade: QuantidadeDeValores.VariosValores,
        PontoResolucao: "INSCRICAO",
        Binding: "REGRA_DE_CALCULO:MODALIDADE",
        ValoresDominio: null,
        ValoresDominioDeclarados: null);

    private static MetadadoFatoCongelado MetadadoDaCorRaca() => new(
        Codigo: "COR_RACA",
        Dominio: TipoDoDado.Opcao,
        Origem: OrigemFato.Declarado,
        Cardinalidade: QuantidadeDeValores.ValorUnico,
        PontoResolucao: "INSCRICAO",
        Binding: "CAMPO_FORMULARIO:COR_RACA",
        ValoresDominio: ["BRANCA", "PRETA", "PARDA"],
        ValoresDominioDeclarados: ValoresDaCorRacaForaDeOrdem());

    // PRETA (ordem 1) entra antes de BRANCA (ordem 0) e PARDA tem ordem 2: prova que é o ENCODER,
    // e não a ordem de inserção neste dicionário de teste, quem ordena por Ordem/Codigo.
    private static ValorDominioDeclaradoCongelado[] ValoresDaCorRacaForaDeOrdem() =>
    [
        new ValorDominioDeclaradoCongelado("PRETA", "Autodeclaração de cor/raça preta.", 1),
        new ValorDominioDeclaradoCongelado("BRANCA", "Autodeclaração de cor/raça branca.", 0),
        new ValorDominioDeclaradoCongelado("PARDA", "Autodeclaração de cor/raça parda.", 2),
    ];
}
