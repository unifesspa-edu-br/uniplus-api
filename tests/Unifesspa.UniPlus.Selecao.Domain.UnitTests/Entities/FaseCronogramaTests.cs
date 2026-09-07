namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Cobertura de <see cref="FaseCronograma.Criar"/> (Story #851): as invariantes que a
/// factory prova sozinha — janela × <see cref="OrigemDataFase"/> (CA-07), a unicidade do
/// tipo de ato dentro da fase, a promessa de parecer individual sustentada por publicação
/// de resultado, e a âncora do prazo de recurso derivada do produto preliminar da própria
/// fase.
/// </summary>
public sealed class FaseCronogramaTests
{
    private static ReferenciaRegra RegraAncorada() =>
        ReferenciaRegra.Criar(RegraPrazoRecursoCodigo.AncoradoEmAto, "v1", new string('a', 64)).Value!;

    private static RegraRecursoFase Recurso(Guid produtoAncoraId) =>
        RegraRecursoFase.Criar(RegraAncorada(), ArgsValidos(), produtoAncoraId).Value!;

    private static FaseCronograma Reidratar(
        IReadOnlyList<ProdutoDaFase> produtos,
        RegraRecursoFase? regraRecurso) =>
        FaseCronograma.Reidratar(
            Guid.CreateVersion7(),
            ordem: 1,
            faseCanonicaOrigemId: Guid.CreateVersion7(),
            codigo: "RESULTADO_PRELIMINAR",
            donoInstitucional: "CEPS",
            origemData: OrigemDataFase.Delegada,
            agrupaEtapas: false,
            permiteComplementacao: false,
            coletaInscricao: false, coletaSolicitacaoIsencao: false,
            inicio: null, fim: null,
            produtos, faseConcluinteCodigo: null, emiteParecerIndividual: false,
            bancasRequeridas: [],
            regraRecurso);

    private static ArgsRegraPrazoRecurso ArgsValidos() => new(
        PrazoValor: 48m,
        PrazoUnidade: UnidadePrazo.Horas,
        SuspensividadePrimeiraInstanciaValor: null,
        SuspensividadePrimeiraInstanciaUnidade: null,
        SuspensividadeSegundaInstanciaValor: null,
        SuspensividadeSegundaInstanciaUnidade: null);

    private static Result<FaseCronograma> Criar(
        int ordem = 1,
        OrigemDataFase origemData = OrigemDataFase.Propria,
        DateTimeOffset? inicio = null,
        DateTimeOffset? fim = null,
        bool agrupaEtapas = false,
        IReadOnlyList<ProdutoDaFase>? produtos = null,
        string? faseConcluinteCodigo = null,
        bool emiteParecerIndividual = false,
        RegraRecursoFase? regraRecurso = null) =>
        FaseCronograma.Criar(
            ordem,
            Guid.CreateVersion7(),
            "RESULTADO_PRELIMINAR",
            "CEPS",
            origemData,
            agrupaEtapas,
            permiteComplementacao: false,
            coletaInscricao: false, coletaSolicitacaoIsencao: false,
            inicio,
            fim,
            produtos ?? [],
            faseConcluinteCodigo,
            emiteParecerIndividual,
            bancasRequeridas: [],
            regraRecurso);

    // ── CA-07 — janela × OrigemData ──

    [Fact(DisplayName = "CA-07: fase de origem PROPRIA sem janela é recusada")]
    public void Janela_ObrigatoriaEmDataPropria_SemInicioNemFim_Recusa()
    {
        Result<FaseCronograma> resultado = Criar(origemData: OrigemDataFase.Propria, inicio: null, fim: null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("FaseCronograma.JanelaObrigatoriaEmDataPropria");
    }

    [Theory(DisplayName = "CA-07: fase de origem PROPRIA com apenas um lado da janela é recusada")]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Janela_ObrigatoriaEmDataPropria_ParcialmenteInformada_Recusa(bool comInicio, bool comFim)
    {
        DateTimeOffset? inicio = comInicio ? new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero) : null;
        DateTimeOffset? fim = comFim ? new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero) : null;

        Result<FaseCronograma> resultado = Criar(origemData: OrigemDataFase.Propria, inicio: inicio, fim: fim);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("FaseCronograma.JanelaObrigatoriaEmDataPropria");
    }

    [Fact(DisplayName = "CA-07 (contraprova): fase de origem DELEGADA sem janela é aceita — 'sem data' é estado válido")]
    public void Janela_OrigemDelegada_SemInicioNemFim_Aceita()
    {
        Result<FaseCronograma> resultado = Criar(origemData: OrigemDataFase.Delegada, inicio: null, fim: null);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.Inicio.Should().BeNull();
        resultado.Value.Fim.Should().BeNull();
    }

    [Fact(DisplayName = "CA-07: fase com Fim antes do Inicio é recusada (JanelaInvertida) — vale para qualquer OrigemData")]
    public void Janela_FimAntesDoInicio_Recusa()
    {
        Result<FaseCronograma> resultado = Criar(
            origemData: OrigemDataFase.Delegada,
            inicio: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero),
            fim: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("FaseCronograma.JanelaInvertida");
    }

    // ── A janela é guardada em UTC (issue #1124) ──

    [Theory(DisplayName = "A janela informada com offset é guardada em UTC, preservando o instante")]
    [InlineData(-3, 0)]
    [InlineData(5, 30)]
    [InlineData(0, 0)]
    public void Janela_ComOffset_EGuardadaEmUtc(int horas, int minutos)
    {
        TimeSpan offset = new(horas, minutos, 0);
        DateTimeOffset instanteUtc = new(2027, 1, 25, 11, 0, 0, TimeSpan.Zero);

        Result<FaseCronograma> resultado = Criar(
            origemData: OrigemDataFase.Propria,
            inicio: instanteUtc.ToOffset(offset),
            fim: instanteUtc.AddDays(2).ToOffset(offset));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.Inicio.Should().Be(instanteUtc, "o instante informado é preservado");
        resultado.Value.Inicio!.Value.Offset.Should().Be(TimeSpan.Zero,
            "a coluna 'timestamp with time zone' só aceita a representação em UTC");
        resultado.Value.Fim.Should().Be(instanteUtc.AddDays(2));
        resultado.Value.Fim!.Value.Offset.Should().Be(TimeSpan.Zero);
    }

    [Fact(DisplayName = "A janela invertida é reconhecida pelo instante, não pela hora local de cada offset")]
    public void Janela_InvertidaEntreOffsetsDiferentes_Recusa()
    {
        // 10:00-03:00 é 13:00Z; 12:00Z é anterior — a hora local do fim (12) parece maior
        // que a do início (10), mas o instante é menor.
        Result<FaseCronograma> resultado = Criar(
            origemData: OrigemDataFase.Delegada,
            inicio: new DateTimeOffset(2027, 1, 25, 10, 0, 0, TimeSpan.FromHours(-3)),
            fim: new DateTimeOffset(2027, 1, 25, 12, 0, 0, TimeSpan.Zero));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("FaseCronograma.JanelaInvertida");
        resultado.Error.Message.Should().NotContain("-03:00",
            "a mensagem descreve a janela na mesma representação em que ela é guardada");
    }

    [Fact(DisplayName = "Reidratar também guarda a janela em UTC")]
    public void Reidratar_ComOffset_GuardaEmUtc()
    {
        DateTimeOffset instanteUtc = new(2027, 1, 25, 11, 0, 0, TimeSpan.Zero);

        FaseCronograma fase = FaseCronograma.Reidratar(
            Guid.CreateVersion7(),
            ordem: 1,
            faseCanonicaOrigemId: Guid.CreateVersion7(),
            codigo: "RESULTADO_PRELIMINAR",
            donoInstitucional: "CEPS",
            origemData: OrigemDataFase.Propria,
            agrupaEtapas: false,
            permiteComplementacao: false,
            coletaInscricao: false, coletaSolicitacaoIsencao: false,
            inicio: instanteUtc.ToOffset(TimeSpan.FromHours(-3)),
            fim: instanteUtc.AddDays(2).ToOffset(TimeSpan.FromHours(-3)),
            produtos: [], faseConcluinteCodigo: null, emiteParecerIndividual: false,
            bancasRequeridas: [],
            regraRecurso: null);

        fase.Inicio.Should().Be(instanteUtc);
        fase.Inicio!.Value.Offset.Should().Be(TimeSpan.Zero);
        fase.Fim!.Value.Offset.Should().Be(TimeSpan.Zero);
    }

    // ── CA-01/CA-04 — a coleção de produtos e o ProduzResultado derivado ──

    [Fact(DisplayName = "CA-04: fase sem produto nenhum não produz resultado, e é aceita")]
    public void Produtos_Vazios_NaoProduzResultado()
    {
        Result<FaseCronograma> resultado = Criar(origemData: OrigemDataFase.Delegada, produtos: []);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.ProduzResultado.Should().BeFalse();
    }

    [Fact(DisplayName = "CA-01/CA-04: produto SEM papel não torna a fase produtora de resultado")]
    public void Produtos_SemPapel_NaoProduzResultado()
    {
        Result<FaseCronograma> resultado = Criar(
            origemData: OrigemDataFase.Delegada,
            produtos: [ProdutoDaFase.Criar("COMUNICADO", null)]);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        FaseCronograma fase = resultado.Value!;
        fase.ProduzResultado.Should().BeFalse(
            "o ato que não é resultado é publicado sem papel, e publicar aviso não torna a fase produtora");
        fase.Produtos.Should().ContainSingle().Which.Papel.Should().BeNull();
    }

    [Theory(DisplayName = "CA-04: produto COM papel torna a fase produtora de resultado")]
    [InlineData(PapelProdutoFase.Preliminar)]
    [InlineData(PapelProdutoFase.Definitivo)]
    public void Produtos_ComPapel_ProduzResultado(PapelProdutoFase papel)
    {
        Result<FaseCronograma> resultado = Criar(
            origemData: OrigemDataFase.Delegada,
            produtos: [ProdutoDaFase.Criar("RESULTADO_PRELIMINAR", papel)]);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.ProduzResultado.Should().BeTrue();
    }

    // ── CA-03 — o mesmo ato uma única vez por fase ──

    [Fact(DisplayName = "CA-03: declarar o MESMO tipo de ato duas vezes na fase é recusado")]
    public void Produtos_AtoDuplicado_Recusa()
    {
        Result<FaseCronograma> resultado = Criar(
            origemData: OrigemDataFase.Delegada,
            produtos:
            [
                ProdutoDaFase.Criar("RESULTADO_PRELIMINAR", PapelProdutoFase.Preliminar),
                ProdutoDaFase.Criar("RESULTADO_PRELIMINAR", PapelProdutoFase.Definitivo),
            ]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle()
            .Which.Error.Code.Should().Be("FaseCronograma.AtoDuplicadoNaFase");
    }

    [Fact(DisplayName = "CA-03 (contraprova): dois atos DISTINTOS com o mesmo papel na mesma fase são aceitos")]
    public void Produtos_AtosDistintosComOMesmoPapel_Aceita()
    {
        Result<FaseCronograma> resultado = Criar(
            origemData: OrigemDataFase.Delegada,
            produtos:
            [
                ProdutoDaFase.Criar("GABARITO_PRELIMINAR", PapelProdutoFase.Preliminar),
                ProdutoDaFase.Criar("RESULTADO_PRELIMINAR", PapelProdutoFase.Preliminar),
            ],
            faseConcluinteCodigo: "RESULTADO_FINAL");

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.Produtos.Should().HaveCount(2,
            "gabarito preliminar e resultado preliminar são documentos diferentes, e a fase publica os dois");
    }

    // ── CA-13/CA-14 — parecer individual ──

    [Fact(DisplayName = "CA-14: fase que promete parecer individual sem publicar resultado é recusada")]
    public void ParecerIndividual_SemResultado_Recusa()
    {
        Result<FaseCronograma> resultado = Criar(
            origemData: OrigemDataFase.Delegada,
            produtos: [ProdutoDaFase.Criar("COMUNICADO", null)],
            emiteParecerIndividual: true);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle()
            .Which.Error.Code.Should().Be("FaseCronograma.ParecerIndividualSemResultado");
    }

    [Fact(DisplayName = "CA-14 (contraprova): fase que promete parecer individual e publica resultado é aceita")]
    public void ParecerIndividual_ComResultado_Aceita()
    {
        Result<FaseCronograma> resultado = Criar(
            origemData: OrigemDataFase.Delegada,
            produtos: [ProdutoDaFase.Criar("RESULTADO_FINAL", PapelProdutoFase.Definitivo)],
            emiteParecerIndividual: true);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.EmiteParecerIndividual.Should().BeTrue();
    }

    [Fact(DisplayName = "CA-13 (fronteira): fase que NÃO promete parecer é aceita sem publicar resultado nenhum")]
    public void ParecerIndividual_NaoPrometido_SemResultado_Aceita()
    {
        Result<FaseCronograma> resultado = Criar(
            origemData: OrigemDataFase.Delegada,
            produtos: [],
            emiteParecerIndividual: false);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.EmiteParecerIndividual.Should().BeFalse();
    }

    [Fact(DisplayName = "Reidratar recusa âncora em produto que não é preliminar, como Criar e o decodificador recusam")]
    public void Reidratar_AncoraEmProdutoNaoPreliminar_Lanca()
    {
        ProdutoDaFase definitivo = ProdutoDaFase.Criar("RESULTADO_FINAL", PapelProdutoFase.Definitivo);
        ProdutoDaFase preliminar = ProdutoDaFase.Criar("RESULTADO_PRELIMINAR", PapelProdutoFase.Preliminar);

        Action reidratar = () => Reidratar([preliminar, definitivo], Recurso(definitivo.Id));

        // A âncora PERTENCE à fase e mesmo assim é incoerente: conferir só a pertinência
        // deixaria a fase nascer com o prazo contando de um ato que encerra a matéria em vez
        // de abri-la — metade da invariante que Criar prova.
        reidratar.Should().Throw<ArgumentException>()
            .WithMessage("*não ancora em nenhum dos produtos preliminares*");
    }

    [Fact(DisplayName = "Reidratar recusa âncora em produto de outra fase")]
    public void Reidratar_AncoraForaDaFase_Lanca()
    {
        ProdutoDaFase preliminarDaOutraFase = ProdutoDaFase.Criar("RESULTADO_PRELIMINAR", PapelProdutoFase.Preliminar);
        ProdutoDaFase preliminarDesta = ProdutoDaFase.Criar("RESULTADO_PRELIMINAR", PapelProdutoFase.Preliminar);

        Action reidratar = () => Reidratar([preliminarDesta], Recurso(preliminarDaOutraFase.Id));

        reidratar.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "Contraprova: Reidratar aceita a âncora no produto preliminar da própria fase")]
    public void Reidratar_AncoraNoPreliminarDaPropriaFase_Aceita()
    {
        ProdutoDaFase preliminar = ProdutoDaFase.Criar("RESULTADO_PRELIMINAR", PapelProdutoFase.Preliminar);

        FaseCronograma fase = Reidratar([preliminar], Recurso(preliminar.Id));

        fase.RegraRecurso!.ProdutoAncoraId.Should().Be(preliminar.Id);
    }

    // ── CA-01/CA-02/CA-03 — a âncora do prazo é um produto preliminar da própria fase ──

    [Fact(DisplayName = "CA-03: fase que admite recurso e não publica produto preliminar é recusada")]
    public void RegraRecurso_SemProdutoPreliminar_Recusa()
    {
        ProdutoDaFase definitivo = ProdutoDaFase.Criar("RESULTADO_FINAL", PapelProdutoFase.Definitivo);

        Result<FaseCronograma> resultado = Criar(
            origemData: OrigemDataFase.Delegada,
            produtos: [definitivo],
            regraRecurso: Recurso(definitivo.Id));

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(e => e.Error.Code).Should().BeEquivalentTo(["RegraRecursoFase.FaseSemProdutoPreliminar"],
            "ancorar no definitivo é a mesma falta de sempre — a fase não publica decisão contestável");
        resultado.Errors.Should().ContainSingle().Which.Field.Should().Be("produtos");
    }

    [Fact(DisplayName = "CA-03: fase que admite recurso e não publica produto nenhum é recusada pela ausência do preliminar")]
    public void RegraRecurso_SemProdutoAlgum_RecusaPelaAusenciaDoPreliminar()
    {
        Result<FaseCronograma> resultado = Criar(
            origemData: OrigemDataFase.Delegada,
            produtos: [],
            regraRecurso: Recurso(Guid.CreateVersion7()));

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(e => e.Error.Code).Should().BeEquivalentTo(["RegraRecursoFase.FaseSemProdutoPreliminar"],
            "a recusa que orienta manda declarar o preliminar, e não é diluída numa segunda sobre a âncora");
    }

    [Fact(DisplayName = "CA-01: a fase que publica preliminar E definitiva ancora o prazo no preliminar")]
    public void RegraRecurso_AncoraNoProdutoPreliminar_Aceita()
    {
        ProdutoDaFase preliminar = ProdutoDaFase.Criar("RESULTADO_PRELIMINAR", PapelProdutoFase.Preliminar);
        RegraRecursoFase regraRecurso = Recurso(preliminar.Id);

        Result<FaseCronograma> resultado = Criar(
            origemData: OrigemDataFase.Delegada,
            produtos: [preliminar, ProdutoDaFase.Criar("RESULTADO_FINAL", PapelProdutoFase.Definitivo)],
            regraRecurso: regraRecurso);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.RegraRecurso!.ProdutoAncoraId.Should().Be(preliminar.Id,
            "a fase que conclui a própria matéria publica os dois resultados, e o prazo corre do preliminar");
    }

    [Fact(DisplayName = "CA-02: ancorar num produto que NÃO é preliminar desta fase é recusado")]
    public void RegraRecurso_AncoraForaDosPreliminares_Recusa()
    {
        ProdutoDaFase preliminar = ProdutoDaFase.Criar("RESULTADO_PRELIMINAR", PapelProdutoFase.Preliminar);
        ProdutoDaFase definitivo = ProdutoDaFase.Criar("RESULTADO_FINAL", PapelProdutoFase.Definitivo);

        Result<FaseCronograma> resultado = Criar(
            origemData: OrigemDataFase.Delegada,
            produtos: [preliminar, definitivo],
            regraRecurso: Recurso(definitivo.Id));

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(e => e.Error.Code).Should()
            .BeEquivalentTo(["RegraRecursoFase.AncoraNaoEhProdutoPreliminarDaFase"]);
        resultado.Errors.Should().ContainSingle().Which.Error.Message.Should().Contain("RESULTADO_PRELIMINAR");
    }

    [Fact(DisplayName = "CA-02: ancorar no produto de OUTRA fase que publica o MESMO tipo de ato é recusado")]
    public void RegraRecurso_AncoraNoProdutoHomonimoDeOutraFase_Recusa()
    {
        ProdutoDaFase preliminarDaOutraFase = ProdutoDaFase.Criar("RESULTADO_PRELIMINAR", PapelProdutoFase.Preliminar);
        ProdutoDaFase preliminarDesta = ProdutoDaFase.Criar("RESULTADO_PRELIMINAR", PapelProdutoFase.Preliminar);

        Result<FaseCronograma> resultado = Criar(
            origemData: OrigemDataFase.Delegada,
            produtos: [preliminarDesta],
            regraRecurso: Recurso(preliminarDaOutraFase.Id));

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(e => e.Error.Code).Should()
            .BeEquivalentTo(["RegraRecursoFase.AncoraNaoEhProdutoPreliminarDaFase"],
                "o código do tipo de ato coincide nas duas fases, e é a identidade do produto que as distingue");
    }

    [Fact(DisplayName = "CA-01: a fase que publica DOIS preliminares ancora no que declarou, não no outro")]
    public void RegraRecurso_DoisPreliminares_AncoraNoDeclarado()
    {
        ProdutoDaFase gabarito = ProdutoDaFase.Criar("GABARITO_PRELIMINAR", PapelProdutoFase.Preliminar);
        ProdutoDaFase resultadoPreliminar = ProdutoDaFase.Criar("RESULTADO_PRELIMINAR", PapelProdutoFase.Preliminar);

        Result<FaseCronograma> resultado = Criar(
            origemData: OrigemDataFase.Delegada,
            produtos: [gabarito, resultadoPreliminar],
            faseConcluinteCodigo: "RESULTADO_FINAL",
            regraRecurso: Recurso(resultadoPreliminar.Id));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.RegraRecurso!.ProdutoAncoraId.Should().Be(resultadoPreliminar.Id);
        resultado.Value!.RegraRecurso!.ProdutoAncoraId.Should().NotBe(gabarito.Id);
    }

    [Fact(DisplayName = "Ordem menor ou igual a zero é recusada")]
    public void Ordem_MenorOuIgualAZero_Lanca()
    {
        Action act = () => Criar(ordem: 0);

        act.Should().Throw<ArgumentException>().WithParameterName("ordem");
    }

    [Fact(DisplayName = "ADR-0125: JanelaInvertida e ParecerIndividualSemResultado acumulam no mesmo lote")]
    public void Criar_JanelaInvertidaEParecerSemResultado_AcumulaAsDuasViolacoes()
    {
        Result<FaseCronograma> resultado = Criar(
            origemData: OrigemDataFase.Delegada,
            inicio: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero),
            fim: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            produtos: [],
            emiteParecerIndividual: true);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(e => e.Error.Code).Should().BeEquivalentTo(
        [
            "FaseCronograma.JanelaInvertida",
            "FaseCronograma.ParecerIndividualSemResultado",
        ]);
        resultado.Errors.Should().Contain(e => e.Field == "fim" && e.Error.Code == "FaseCronograma.JanelaInvertida");
        resultado.Errors.Should().Contain(e => e.Field == "emiteParecerIndividual" && e.Error.Code == "FaseCronograma.ParecerIndividualSemResultado");
    }
}
