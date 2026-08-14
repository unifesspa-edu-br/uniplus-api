namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Cobertura de <see cref="RegraRecursoFase.Criar"/>: as invariantes puras que a entidade
/// prova sozinha — a referência por símbolo e as unidades declaráveis no prazo de
/// interposição (UNI-REQ-0113). A resolução contra o catálogo vivo (existe, TipoRegra
/// correto, hash bate) é do handler — ver <c>DefinirCronogramaFasesCommandHandlerTests</c>;
/// a exigência da convenção de contagem é invariante do processo, não desta entidade.
/// </summary>
public sealed class RegraRecursoFaseTests
{
    private static ArgsRegraPrazoRecurso ArgsBase(
        UnidadePrazo prazoUnidade = UnidadePrazo.Horas,
        decimal prazoValor = 48m,
        UnidadePrazo? susp1Unidade = null,
        UnidadePrazo? susp2Unidade = null) => new(
            PrazoValor: prazoValor,
            PrazoUnidade: prazoUnidade,
            AtoAncoraCodigo: "RESULTADO_PRELIMINAR",
            SuspensividadePrimeiraInstanciaValor: susp1Unidade is null ? null : 5m,
            SuspensividadePrimeiraInstanciaUnidade: susp1Unidade,
            SuspensividadeSegundaInstanciaValor: susp2Unidade is null ? null : 5m,
            SuspensividadeSegundaInstanciaUnidade: susp2Unidade);

    private static ReferenciaRegra RegraAncorada() => ReferenciaRegra.Criar(
        RegraPrazoRecursoCodigo.AncoradoEmAto, "v1", new string('a', 64)).Value!;

    [Fact(DisplayName = "CA-01: referencia a regra por SÍMBOLO (RegraPrazoRecursoCodigo.AncoradoEmAto), não literal solto")]
    public void ReferenciaRegraPorSimbolo()
    {
        Result<RegraRecursoFase> resultado = RegraRecursoFase.Criar(RegraAncorada(), ArgsBase());

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.Regra.Codigo.Should().Be(RegraPrazoRecursoCodigo.AncoradoEmAto);
    }

    [Fact(DisplayName = "CA-02 (contraprova): referenciar qualquer OUTRA regra do catálogo é recusado")]
    public void RegraDeTipoIncompativel_Recusa()
    {
        ReferenciaRegra outraRegra = ReferenciaRegra.Criar(
            "BONUS-MULTIPLICATIVO", "v1", new string('b', 64)).Value!;

        Result<RegraRecursoFase> resultado = RegraRecursoFase.Criar(outraRegra, ArgsBase());

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("RegraRecursoFase.RegraCatalogoInvalida");
    }

    [Theory(DisplayName = "As duas unidades declaráveis na interposição são aceitas: dias úteis em valor inteiro, e horas")]
    [InlineData(UnidadePrazo.DiasUteis, 2)]
    [InlineData(UnidadePrazo.DiasUteis, 1)]
    [InlineData(UnidadePrazo.Horas, 48)]
    [InlineData(UnidadePrazo.Horas, 6)]
    public void UnidadesDeclaraveis_Aceitas(UnidadePrazo unidade, int valor)
    {
        Result<RegraRecursoFase> resultado = RegraRecursoFase.Criar(
            RegraAncorada(), ArgsBase(prazoUnidade: unidade, prazoValor: valor));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.Args.PrazoUnidade.Should().Be(unidade);
        resultado.Value.Args.PrazoValor.Should().Be(valor);
    }

    [Fact(DisplayName = "Horas com valor fracionário é aceita — a restrição de valor inteiro é da unidade dias úteis")]
    public void HorasFracionarias_Aceitas()
    {
        Result<RegraRecursoFase> resultado = RegraRecursoFase.Criar(
            RegraAncorada(), ArgsBase(prazoUnidade: UnidadePrazo.Horas, prazoValor: 1.5m));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.Args.PrazoValor.Should().Be(1.5m);
    }

    [Fact(DisplayName = "Prazo de interposição em dias CORRIDOS é recusado — a janela não pode encolher por cair em feriado")]
    public void PrazoDeInterposicaoEmDiasCorridos_Recusa()
    {
        Result<RegraRecursoFase> resultado = RegraRecursoFase.Criar(
            RegraAncorada(), ArgsBase(prazoUnidade: UnidadePrazo.Dias, prazoValor: 5m));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("RegraRecursoFase.PrazoEmDiasCorridos");

        // A mensagem chega ao administrador como `detail` do ProblemDetails: orienta as
        // unidades admitidas, em vez de só negar a recebida.
        resultado.Error.Message.Should().Be(
            "O prazo de interposição deve ser informado em dias úteis ou horas; dias corridos não são aceitos.");
    }

    [Theory(DisplayName = "Prazo de interposição em FRAÇÃO de dia útil é recusado, com causa distinta da de dia corrido")]
    [InlineData(1.5)]
    [InlineData(0.5)]
    [InlineData(2.25)]
    public void PrazoDeInterposicaoEmFracaoDeDiaUtil_Recusa(decimal valorFracionario)
    {
        Result<RegraRecursoFase> resultado = RegraRecursoFase.Criar(
            RegraAncorada(), ArgsBase(prazoUnidade: UnidadePrazo.DiasUteis, prazoValor: valorFracionario));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("RegraRecursoFase.PrazoEmFracaoDeDiaUtil",
            "fração e dia corrido são causas diferentes, com remediação diferente — quem declarou fração é orientado a usar horas");
        resultado.Error.Message.Should().Contain("horas");
    }

    [Theory(DisplayName = "A suspensividade aceita as três unidades, em qualquer uma das duas instâncias — é outro relógio")]
    [InlineData(UnidadePrazo.DiasUteis, null)]
    [InlineData(null, UnidadePrazo.DiasUteis)]
    [InlineData(UnidadePrazo.DiasUteis, UnidadePrazo.DiasUteis)]
    [InlineData(UnidadePrazo.Dias, UnidadePrazo.Horas)]
    public void Suspensividade_AceitaAsTresUnidades(UnidadePrazo? susp1, UnidadePrazo? susp2)
    {
        Result<RegraRecursoFase> resultado = RegraRecursoFase.Criar(
            RegraAncorada(), ArgsBase(susp1Unidade: susp1, susp2Unidade: susp2));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.Args.SuspensividadePrimeiraInstanciaUnidade.Should().Be(susp1);
        resultado.Value.Args.SuspensividadeSegundaInstanciaUnidade.Should().Be(susp2);
    }

    [Fact(DisplayName = "Suspensividade em dias corridos é congelada com a segunda instância nula — null é valor legítimo, não omissão")]
    public void Suspensividade_DiasCorridos_PrimeiraPreenchidaSegundaNula_Aceita()
    {
        Result<RegraRecursoFase> resultado = RegraRecursoFase.Criar(
            RegraAncorada(), ArgsBase(susp1Unidade: UnidadePrazo.Dias, susp2Unidade: null));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.Args.SuspensividadePrimeiraInstanciaUnidade.Should().Be(UnidadePrazo.Dias);
        resultado.Value.Args.SuspensividadePrimeiraInstanciaValor.Should().Be(5m);
        resultado.Value.Args.SuspensividadeSegundaInstanciaUnidade.Should().BeNull(
            "null numa instância é valor legítimo — significa que ela não bloqueia (caso normal do Ingresso via judicial)");
        resultado.Value.Args.SuspensividadeSegundaInstanciaValor.Should().BeNull();
    }
}
