namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.UnitTests.TestSupport;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class TipoConceitoNumericoTests
{
    private static Result<TipoConceito> CriarComEscala(EscalaInformada? escala) =>
        TipoConceito.Criar("TIPO", DefinicoesDeTipoConceito.Numerica() with { Numerico = escala });

    [Fact(DisplayName = "Numérico sem a escala é recusado")]
    public void Escala_Ausente_Recusa() =>
        CriarComEscala(null).DeveRecusarComo(TipoConceitoErrorCodes.EscalaNumericaObrigatoria, "numerico");

    [Theory(DisplayName = "Numérico sem a escala mínima, a máxima ou as casas decimais é recusado")]
    [InlineData(null, 10, 2)]
    [InlineData(0, null, 2)]
    [InlineData(0, 10, null)]
    public void Escala_Incompleta_Recusa(int? minima, int? maxima, int? casas) =>
        CriarComEscala(new EscalaInformada(minima, maxima, casas))
            .DeveRecusarComo(TipoConceitoErrorCodes.EscalaNumericaObrigatoria, "numerico");

    [Theory(DisplayName = "Escala com mínimo igual ou maior que o máximo é recusada")]
    [InlineData(10, 10)]
    [InlineData(10, 0)]
    [InlineData(0, -1)]
    public void Escala_MinimoNaoMenorQueMaximo_Recusa(int minima, int maxima) =>
        CriarComEscala(new EscalaInformada(minima, maxima, 2))
            .DeveRecusarComo(TipoConceitoErrorCodes.EscalaNumericaInvalida, "numerico");

    [Theory(DisplayName = "Aceita escala com limites negativos ou fracionários quando o mínimo é menor que o máximo")]
    [InlineData(-10, -1)]
    [InlineData(0.5, 0.75)]
    public void Escala_MinimoMenorQueMaximo_Aceita(double minima, double maxima) =>
        CriarComEscala(new EscalaInformada((decimal)minima, (decimal)maxima, 2)).IsSuccess.Should().BeTrue();

    [Theory(DisplayName = "Recusa casas decimais fora de 0 a 4")]
    [InlineData(-1)]
    [InlineData(5)]
    public void Casas_ForaDoIntervalo_Recusa(int casas) =>
        CriarComEscala(new EscalaInformada(0m, 10m, casas))
            .DeveRecusarComo(TipoConceitoErrorCodes.CasasDecimaisInvalidas, "numerico.casasDecimais");

    [Theory(DisplayName = "Aceita casas decimais de 0 a 4")]
    [InlineData(0)]
    [InlineData(4)]
    public void Casas_NosLimites_Aceita(int casas) =>
        CriarComEscala(new EscalaInformada(0m, 10m, casas)).Value!.CasasDecimais.Should().Be(casas);

    [Fact(DisplayName = "Numérico não admite valores categóricos")]
    public void Numerico_ComValores_Recusa() =>
        TipoConceito.Criar("TIPO", DefinicoesDeTipoConceito.Numerica() with { Valores = [DefinicoesDeTipoConceito.Valor("A", "A")] })
            .DeveRecusarComo(TipoConceitoErrorCodes.ParametrosIncompativeisComANatureza, "valores");

    [Fact(DisplayName = "Numérico recusa até a lista de valores vazia")]
    public void Numerico_ComListaVazia_Recusa() =>
        TipoConceito.Criar("TIPO", DefinicoesDeTipoConceito.Numerica() with { Valores = [] })
            .DeveRecusarComo(TipoConceitoErrorCodes.ParametrosIncompativeisComANatureza, "valores");
}
