namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Xunit;

using static CenarioDeProcesso;

/// <summary>Os tipos de ato que o cronograma declara: o resultado declara papel, o tipo que congela não é resultado e o tipo único sai uma vez só.</summary>
public sealed class ConferenciaDosTiposDeAtoTests
{
    private static readonly TiposDeAtoUnicosPorObjeto Catalogo = TiposDeAtoUnicosPorObjeto.ParaChecklist(
        new Dictionary<string, string> { ["EDITAL"] = "Edital" },
        [],
        new Dictionary<string, ClassificacaoDoTipoDeAto>
        {
            ["RESULTADO_FINAL"] = new(EhResultado: true, CongelaConfiguracao: false),
            ["EDITAL"] = new(EhResultado: false, CongelaConfiguracao: true),
        });

    private static ModeloDeFase FaseCom(params ProdutoDaFase[] produtos) => new ModeloDeFase("RESULTADO", 1).Publicando(produtos);

    private static ModeloDeEtapa EtapaCom(params ProdutoDaEtapa[] produtos) => new ModeloDeEtapa("Redação") with { Produtos = produtos };

    [Fact(DisplayName = "O tipo que é resultado, declarado sem papel, é recusado no papel do produto, na fase e na etapa")]
    public void ResultadoSemPapel_FaseEEtapa_RecusaNoPapel()
    {
        CronogramaDoProcesso cronograma = Fases(FaseCom(ProdutoDaFase.Criar("COMUNICADO", null), ProdutoDaFase.Criar("RESULTADO_FINAL", null)));
        EtapasDoProcesso etapas = Etapas(EtapaCom(ProdutoDaEtapa.Criar("RESULTADO_FINAL", null)) with { Ordem = 4 });

        Causas(ConferenciaDosTiposDeAto.ConferirResultadosSemPapel(cronograma, etapas, Catalogo)).Should().Equal(
            ("fases[0].produtos[1].papel", "ProcessoSeletivo.ResultadoDeclaradoSemPapel"),
            ("etapas[0].produtos[0].papel", "ProcessoSeletivo.ResultadoDeclaradoSemPapel"));
        ConferenciaDosTiposDeAto.ConferirResultadosSemPapel(cronograma, etapas, Catalogo)[1].Error.Message
            .Should().Contain("declarado na etapa 'Redação' (ordem 4)");
    }

    [Fact(DisplayName = "O resultado com papel e o ato que não é resultado sem papel não são recusados")]
    public void ResultadoSemPapel_ComPapelOuNaoResultado_SemCausa()
    {
        CronogramaDoProcesso cronograma = Fases(FaseCom(ProdutoDaFase.Criar("COMUNICADO", null), ProdutoDaFase.Criar("RESULTADO_FINAL", PapelProdutoFase.Definitivo)));

        ConferenciaDosTiposDeAto.ConferirResultadosSemPapel(cronograma, Etapas(), Catalogo).Should().BeEmpty();
    }

    [Fact(DisplayName = "O tipo que congela a configuração declarado com papel é recusado no papel do produto")]
    public void TipoQueCongela_ComPapel_RecusaNoPapel()
    {
        CronogramaDoProcesso cronograma = Fases(FaseCom(ProdutoDaFase.Criar("EDITAL", PapelProdutoFase.Preliminar)));

        Causas(ConferenciaDosTiposDeAto.ConferirTiposQueCongelamComoResultado(cronograma, Etapas(), Catalogo))
            .Should().Equal(("fases[0].produtos[0].papel", "ProcessoSeletivo.TipoQueCongelaDeclaradoComoResultado"));
        ConferenciaDosTiposDeAto.ConferirTiposQueCongelamComoResultado(Fases(FaseCom(ProdutoDaFase.Criar("EDITAL", null))), Etapas(), Catalogo).Should().BeEmpty();
    }

    [Fact(DisplayName = "O tipo único declarado em dois lugares é recusado no campo do produto da segunda ocorrência, citando os lugares")]
    public void TipoUnico_DeclaradoDuasVezes_RecusaNoCampoDaSegundaOcorrencia()
    {
        CronogramaDoProcesso cronograma = Fases(FaseCom(ProdutoDaFase.Criar("EDITAL", null)));
        EtapasDoProcesso etapas = Etapas(EtapaCom(ProdutoDaEtapa.Criar("EDITAL", null)));

        IReadOnlyList<FieldError> recusas = ConferenciaDosTiposDeAto.ConferirTiposUnicosRepetidos(cronograma, etapas, Catalogo);

        Causas(recusas).Should().Equal(("etapas[0].produtos[0].atoCodigo", "ProcessoSeletivo.TipoDeAtoUnicoDeclaradoMaisDeUmaVez"));
        recusas.Single().Error.Message.Should().Be("O tipo de ato 'Edital' só pode ser publicado uma vez por processo e está declarado na fase 'RESULTADO' e na etapa 'Redação' (ordem não definida).");
    }

    [Fact(DisplayName = "O tipo único declarado duas vezes no mesmo lugar cita a contagem, e declarado uma vez não é recusado")]
    public void TipoUnico_MesmoLugar_CitaAContagem()
    {
        CronogramaDoProcesso duas = Fases(FaseCom(ProdutoDaFase.Criar("EDITAL", null), ProdutoDaFase.Criar("EDITAL", PapelProdutoFase.Definitivo)));

        FieldError causa = ConferenciaDosTiposDeAto.ConferirTiposUnicosRepetidos(duas, Etapas(), Catalogo).Single();

        causa.Error.Message.Should().Contain("na fase 'RESULTADO' (2 vezes)");
        causa.Field.Should().Be("fases[0].produtos[1].atoCodigo");
        ConferenciaDosTiposDeAto.ConferirTiposUnicosRepetidos(Fases(FaseCom(ProdutoDaFase.Criar("EDITAL", null))), Etapas(), Catalogo).Should().BeEmpty();
    }

    [Fact(DisplayName = "Cada produto repetido, da segunda ocorrência em diante, é uma causa no campo dele, as fases antes das etapas")]
    public void TipoUnico_TresOcorrencias_UmaCausaPorProdutoRepetido()
    {
        CronogramaDoProcesso cronograma = Fases(FaseCom(ProdutoDaFase.Criar("EDITAL", null), ProdutoDaFase.Criar("EDITAL", PapelProdutoFase.Definitivo)));
        EtapasDoProcesso etapas = Etapas(EtapaCom(ProdutoDaEtapa.Criar("EDITAL", null)));

        Causas(ConferenciaDosTiposDeAto.ConferirTiposUnicosRepetidos(cronograma, etapas, Catalogo)).Select(static causa => causa.Campo).Should().Equal(
            "fases[0].produtos[1].atoCodigo", "etapas[0].produtos[0].atoCodigo");
    }

    [Fact(DisplayName = "O ato já registrado do processo ocupa a vaga do tipo único; a repetição está no ato pedido e fica sem campo")]
    public void TipoUnico_AtoJaRegistrado_OcupaAVaga()
    {
        TiposDeAtoUnicosPorObjeto comRegistrado = TiposDeAtoUnicosPorObjeto.ParaChecklist(
            new Dictionary<string, string> { ["EDITAL"] = "Edital" }, ["EDITAL"]);

        ConferenciaDosTiposDeAto.ConferirTiposUnicosRepetidos(Fases(FaseCom(ProdutoDaFase.Criar("EDITAL", null))), Etapas(), comRegistrado)
            .Single().Should().Match<FieldError>(static causa => causa.Field == null && causa.Error.Message.Contains("no ato já registrado do processo", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "Os três itens saem nesta ordem, verdes quando conformes e vermelhos cada um com a sua causa")]
    public void Itens_TresAssuntos_NaOrdemEComACausaDeCada()
    {
        CronogramaDoProcesso conforme = Fases(FaseCom(ProdutoDaFase.Criar("COMUNICADO", null)));
        CronogramaDoProcesso comCausas = Fases(FaseCom(ProdutoDaFase.Criar("RESULTADO_FINAL", null), ProdutoDaFase.Criar("EDITAL", PapelProdutoFase.Preliminar), ProdutoDaFase.Criar("EDITAL", null)));

        IReadOnlyList<ItemConformidade> verdes = ConferenciaDosTiposDeAto.Itens(conforme, Etapas(), Catalogo);
        IReadOnlyList<ItemConformidade> vermelhos = ConferenciaDosTiposDeAto.Itens(comCausas, Etapas(), Catalogo);

        verdes.Select(static item => item.Codigo).Should().Equal(
            "cronograma_resultado_declarado_com_papel",
            "cronograma_tipo_que_congela_nao_eh_resultado",
            "cronograma_tipo_de_ato_unico_declarado_uma_vez");
        verdes.Should().OnlyContain(static item => item.Ok);
        vermelhos.Should().OnlyContain(static item => !item.Ok);
    }
}
