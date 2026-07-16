namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Cobertura de <see cref="FaseCronograma.Criar"/> (Story #851): as invariantes que a
/// factory prova sozinha — janela × <see cref="OrigemDataFase"/> (CA-07), e as duas
/// primeiras invariantes de <see cref="RegraRecursoFase"/> que dependem da fase-mãe
/// (item 1/2 do §3.6, CA-16/CA-17).
/// </summary>
public sealed class FaseCronogramaTests
{
    private static ReferenciaRegra RegraAncorada() =>
        ReferenciaRegra.Criar(RegraPrazoRecursoCodigo.AncoradoEmAto, "v1", new string('a', 64)).Value!;

    private static ArgsRegraPrazoRecurso ArgsValidos(string atoAncoraCodigo) => new(
        PrazoValor: 48m,
        PrazoUnidade: UnidadePrazo.Horas,
        AtoAncoraCodigo: atoAncoraCodigo,
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
        bool produzResultado = false,
        bool resultadoDefinitivo = false,
        string? atoProduzidoCodigo = null,
        RegraRecursoFase? regraRecurso = null) =>
        FaseCronograma.Criar(
            ordem,
            Guid.CreateVersion7(),
            "RESULTADO_PRELIMINAR",
            "CEPS",
            origemData,
            agrupaEtapas,
            permiteComplementacao: false,
            produzResultado,
            resultadoDefinitivo,
            coletaInscricao: false,
            inicio,
            fim,
            atoProduzidoCodigo,
            atoProduzidoEfeitoIrreversivel: false,
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

    [Fact(DisplayName = "Fase que produz resultado sem declarar o ato produzido é recusada")]
    public void ProduzResultado_SemAtoProduzido_Recusa()
    {
        Result<FaseCronograma> resultado = Criar(
            origemData: OrigemDataFase.Delegada, produzResultado: true, atoProduzidoCodigo: null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("FaseCronograma.AtoProduzidoObrigatorio");
    }

    // ── §3.6 itens 1/2 — invariantes de RegraRecursoFase que dependem da fase-mãe ──

    [Fact(DisplayName = "CA-16: RegraRecursoFase numa fase que NÃO produz resultado é recusada")]
    public void RegraRecurso_FaseNaoProduzResultado_Recusa()
    {
        RegraRecursoFase regraRecurso = RegraRecursoFase.Criar(RegraAncorada(), ArgsValidos("RESULTADO_PRELIMINAR")).Value!;

        Result<FaseCronograma> resultado = Criar(
            origemData: OrigemDataFase.Delegada,
            produzResultado: false,
            atoProduzidoCodigo: null,
            regraRecurso: regraRecurso);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("RegraRecursoFase.FaseNaoProduzResultado");
    }

    [Fact(DisplayName = "CA-16: RegraRecursoFase numa fase de resultado DEFINITIVO é recusada")]
    public void RegraRecurso_ResultadoDefinitivo_NaoAdmiteRecurso()
    {
        RegraRecursoFase regraRecurso = RegraRecursoFase.Criar(RegraAncorada(), ArgsValidos("RESULTADO_PRELIMINAR")).Value!;

        Result<FaseCronograma> resultado = Criar(
            origemData: OrigemDataFase.Delegada,
            produzResultado: true,
            resultadoDefinitivo: true,
            atoProduzidoCodigo: "RESULTADO_PRELIMINAR",
            regraRecurso: regraRecurso);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("RegraRecursoFase.RecursoContraResultadoDefinitivo");
    }

    [Fact(DisplayName = "CA-17: a âncora do prazo tem de ser o ato PRODUZIDO PELA PRÓPRIA fase — ancorar em outro é recusado")]
    public void RegraRecurso_Ancora_DeOutraFase_Recusa()
    {
        RegraRecursoFase regraRecurso = RegraRecursoFase.Criar(RegraAncorada(), ArgsValidos("GABARITO_PRELIMINAR")).Value!;

        Result<FaseCronograma> resultado = Criar(
            origemData: OrigemDataFase.Delegada,
            produzResultado: true,
            resultadoDefinitivo: false,
            atoProduzidoCodigo: "RESULTADO_PRELIMINAR",
            regraRecurso: regraRecurso);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("RegraRecursoFase.AncoraDeOutraFase");
    }

    [Fact(DisplayName = "Fase conforme com RegraRecursoFase ancorada no PRÓPRIO ato é aceita")]
    public void RegraRecurso_AncoraNaPropriaFase_Aceita()
    {
        RegraRecursoFase regraRecurso = RegraRecursoFase.Criar(RegraAncorada(), ArgsValidos("RESULTADO_PRELIMINAR")).Value!;

        Result<FaseCronograma> resultado = Criar(
            origemData: OrigemDataFase.Delegada,
            produzResultado: true,
            resultadoDefinitivo: false,
            atoProduzidoCodigo: "RESULTADO_PRELIMINAR",
            regraRecurso: regraRecurso);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.RegraRecurso.Should().BeSameAs(regraRecurso);
    }

    [Fact(DisplayName = "Ordem menor ou igual a zero é recusada")]
    public void Ordem_MenorOuIgualAZero_Lanca()
    {
        Action act = () => Criar(ordem: 0);

        act.Should().Throw<ArgumentException>().WithParameterName("ordem");
    }
}
