namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Cobertura de <see cref="RecursoDaEtapa.Criar"/> — as invariantes de forma sobre o prazo de
/// interposição e a suspensividade.
/// </summary>
/// <remarks>
/// <para>
/// A janela da etapa carrega o MESMO <see cref="ArgsRegraPrazoRecurso"/> que a regra da fase.
/// Enquanto essas invariantes viviam privadas em <see cref="RegraRecursoFase"/>, a etapa
/// aceitava prazo zero, prazo em dia corrido e par de suspensividade pela metade — e a
/// configuração que a fase recusava com 422 era gravada na etapa em silêncio.
/// </para>
/// <para>
/// O que legitimamente difere entre as duas é a <b>âncora</b>: a fase conta sempre da
/// publicação do ato, a etapa também admite a ciência individual. A âncora muda QUANDO o
/// relógio começa, não COMO ele conta — por isso a recusa de dia corrido vale nas duas.
/// </para>
/// </remarks>
public sealed class RecursoDaEtapaTests
{
    private static readonly ReferenciaRegra Regra = ReferenciaRegra.Criar(
        RegraPrazoRecursoCodigo.AncoradoEmAto, "v1", new string('a', 64)).Value!;

    private static readonly Guid Ancora = Guid.CreateVersion7();

    private static ArgsRegraPrazoRecurso Args(
        decimal prazoValor = 48m,
        UnidadePrazo prazoUnidade = UnidadePrazo.Horas,
        decimal? susp1Valor = null,
        UnidadePrazo? susp1Unidade = null,
        decimal? susp2Valor = null,
        UnidadePrazo? susp2Unidade = null) => new(
            PrazoValor: prazoValor,
            PrazoUnidade: prazoUnidade,
            SuspensividadePrimeiraInstanciaValor: susp1Valor,
            SuspensividadePrimeiraInstanciaUnidade: susp1Unidade,
            SuspensividadeSegundaInstanciaValor: susp2Valor,
            SuspensividadeSegundaInstanciaUnidade: susp2Unidade);

    private static Result<RecursoDaEtapa> Criar(
        ArgsRegraPrazoRecurso args,
        AncoraDoRecurso ancora = AncoraDoRecurso.AtoPublicado) =>
        RecursoDaEtapa.Criar(ancora, Regra, args, Ancora);

    [Fact(DisplayName = "Janela com prazo e âncora válidos é criada")]
    public void PrazoValido_Cria()
    {
        Criar(Args()).IsSuccess.Should().BeTrue();
    }

    [Theory(DisplayName = "Prazo não positivo é recusado")]
    [InlineData(0)]
    [InlineData(-5)]
    public void PrazoNaoPositivo_Recusa(int prazo)
    {
        Result<RecursoDaEtapa> resultado = Criar(Args(prazoValor: prazo));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("RecursoDaEtapa.PrazoNaoPositivo");
    }

    /// <summary>
    /// UNI-REQ-0113 fala do prazo em que o CANDIDATO tem de agir — feriado encolhe a janela
    /// dele qualquer que seja o instante que iniciou a contagem. Por isso a recusa alcança
    /// também a âncora de ciência individual.
    /// </summary>
    [Theory(DisplayName = "Prazo em dias corridos é recusado nas duas âncoras")]
    [InlineData(AncoraDoRecurso.AtoPublicado)]
    [InlineData(AncoraDoRecurso.CienciaIndividual)]
    public void PrazoEmDiasCorridos_Recusa(AncoraDoRecurso ancora)
    {
        Result<RecursoDaEtapa> resultado = Criar(Args(prazoValor: 3m, prazoUnidade: UnidadePrazo.Dias), ancora);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("RecursoDaEtapa.PrazoEmDiasCorridos");
    }

    [Fact(DisplayName = "Prazo sem unidade declarável é recusado")]
    public void PrazoSemUnidade_Recusa()
    {
        Result<RecursoDaEtapa> resultado = Criar(Args(prazoUnidade: UnidadePrazo.Nenhuma));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("RecursoDaEtapa.PrazoSemUnidadeDeclaravel");
    }

    [Fact(DisplayName = "Fração de dia útil é recusada — prazo menor que um dia se declara em horas")]
    public void FracaoDeDiaUtil_Recusa()
    {
        Result<RecursoDaEtapa> resultado = Criar(Args(prazoValor: 2.5m, prazoUnidade: UnidadePrazo.DiasUteis));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("RecursoDaEtapa.PrazoEmFracaoDeDiaUtil");
    }

    [Fact(DisplayName = "Suspensividade sem os dois campos é a desativação prevista da instância")]
    public void SuspensividadeAusente_Cria()
    {
        Criar(Args()).IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Suspensividade com valor e unidade é aceita")]
    public void SuspensividadeCompleta_Cria()
    {
        Result<RecursoDaEtapa> resultado = Criar(Args(
            susp1Valor: 3m, susp1Unidade: UnidadePrazo.DiasUteis,
            susp2Valor: 48m, susp2Unidade: UnidadePrazo.Horas));

        resultado.IsSuccess.Should().BeTrue();
    }

    /// <summary>
    /// UNI-REQ-0080 congela a suspensividade como par valor-unidade: um lado sem o outro não
    /// descreve janela alguma. Era o que a etapa aceitava calada.
    /// </summary>
    [Fact(DisplayName = "Suspensividade com valor e sem unidade é recusada")]
    public void SuspensividadeSemUnidade_Recusa()
    {
        Result<RecursoDaEtapa> resultado = Criar(Args(susp1Valor: 3m));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("RecursoDaEtapa.SuspensividadeIncompleta");
    }

    [Fact(DisplayName = "Suspensividade com unidade e sem valor é recusada")]
    public void SuspensividadeSemValor_Recusa()
    {
        Result<RecursoDaEtapa> resultado = Criar(Args(susp1Unidade: UnidadePrazo.Horas));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("RecursoDaEtapa.SuspensividadeIncompleta");
    }

    /// <summary>
    /// A magnitude errada é conferida ANTES da incompletude: mandar completar um par cujo
    /// valor já é inválido devolveria a pessoa com uma segunda recusa.
    /// </summary>
    [Fact(DisplayName = "Suspensividade não positiva é recusada antes da incompletude")]
    public void SuspensividadeNaoPositiva_Recusa()
    {
        Result<RecursoDaEtapa> resultado = Criar(Args(susp1Valor: -3m));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("RecursoDaEtapa.SuspensividadeNaoPositiva");
    }

    [Fact(DisplayName = "Unidade de suspensividade não declarável é recusada")]
    public void SuspensividadeUnidadeNaoDeclaravel_Recusa()
    {
        Result<RecursoDaEtapa> resultado = Criar(Args(susp1Valor: 3m, susp1Unidade: UnidadePrazo.Nenhuma));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("RecursoDaEtapa.SuspensividadeUnidadeNaoDeclaravel");
    }

    /// <summary>
    /// A suspensividade admite dia corrido, que a interposição acabou de recusar: é outro
    /// relógio, com outra regra.
    /// </summary>
    [Fact(DisplayName = "Suspensividade em dias corridos é aceita")]
    public void SuspensividadeEmDiasCorridos_Cria()
    {
        Criar(Args(susp1Valor: 5m, susp1Unidade: UnidadePrazo.Dias)).IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Âncora não declarada continua sendo recusada")]
    public void AncoraNenhuma_Recusa()
    {
        Result<RecursoDaEtapa> resultado = Criar(Args(), AncoraDoRecurso.Nenhuma);

        resultado.IsFailure.Should().BeTrue();
    }
    /// <summary>
    /// O decodificador do envelope exige este código exato ao reidratar a janela recursal da
    /// etapa. Aceitar outra entrada do catálogo — ainda que do tipo certo — produziria um
    /// certame publicável cujo envelope não volta, e o descarte da retificação deixaria de ser
    /// possível. A regra da fase já recusava o mesmo input.
    /// </summary>
    [Fact(DisplayName = "Recusa regra de código diferente do prazo ancorado em ato")]
    public void Recusa_RegraDeOutroCodigo()
    {
        ReferenciaRegra outra = ReferenciaRegra.Criar(
            "RECURSO-PRAZO-OUTRA-CONTAGEM", "v1", new string('b', 64)).Value!;

        Result<RecursoDaEtapa> resultado = RecursoDaEtapa.Criar(
            AncoraDoRecurso.AtoPublicado, outra, Args(), Ancora);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("RecursoDaEtapa.RegraCatalogoInvalida");
    }

    [Fact(DisplayName = "Aceita a regra de prazo ancorado em ato")]
    public void Aceita_RegraAncoradaEmAto()
    {
        Result<RecursoDaEtapa> resultado = RecursoDaEtapa.Criar(
            AncoraDoRecurso.AtoPublicado, Regra, Args(), Ancora);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

}
