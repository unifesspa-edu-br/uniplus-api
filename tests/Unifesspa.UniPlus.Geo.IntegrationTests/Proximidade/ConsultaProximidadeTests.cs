namespace Unifesspa.UniPlus.Geo.IntegrationTests.Proximidade;

using AwesomeAssertions;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Geo.API.Formatting;

/// <summary>
/// Validação no boundary HTTP (ADR-0031, #678) dos parâmetros de proximidade:
/// lat/long/raioKm obrigatórios e em faixa, limit com default e teto. Cobre o lado
/// unitário do CA-04 e CA-06 (sem banco).
/// </summary>
public sealed class ConsultaProximidadeTests
{
    private static readonly GeoProximidadeOptions Opcoes = new();

    [Fact(DisplayName = "CA-04: parâmetros válidos criam a consulta com limit default")]
    public void TentarCriar_Valido_UsaLimitDefault()
    {
        bool ok = ConsultaProximidade.TentarCriar(
            -5.35, -49.13, 100, null, Opcoes, out ConsultaProximidade? consulta, out ProblemDetails? erro);

        ok.Should().BeTrue();
        erro.Should().BeNull();
        consulta!.Latitude.Should().Be(-5.35);
        consulta.Longitude.Should().Be(-49.13);
        consulta.RaioKm.Should().Be(100);
        consulta.Limit.Should().Be(Opcoes.LimitPadrao);
    }

    [Fact(DisplayName = "CA-06: limit acima do teto é truncado para o máximo")]
    public void TentarCriar_LimitAcimaDoTeto_Trunca()
    {
        bool ok = ConsultaProximidade.TentarCriar(0, 0, 10, 100_000, Opcoes, out ConsultaProximidade? consulta, out _);

        ok.Should().BeTrue();
        consulta!.Limit.Should().Be(Opcoes.LimitMax);
    }

    [Fact(DisplayName = "limit informado dentro do teto é preservado")]
    public void TentarCriar_LimitValido_Preserva()
    {
        bool ok = ConsultaProximidade.TentarCriar(0, 0, 10, 7, Opcoes, out ConsultaProximidade? consulta, out _);

        ok.Should().BeTrue();
        consulta!.Limit.Should().Be(7);
    }

    [Theory(DisplayName = "CA-04: parâmetro obrigatório ausente retorna 400")]
    [InlineData(null, -49.13, 100.0)]
    [InlineData(-5.35, null, 100.0)]
    [InlineData(-5.35, -49.13, null)]
    public void TentarCriar_ObrigatorioAusente_Falha(double? lat, double? lon, double? raio)
    {
        bool ok = ConsultaProximidade.TentarCriar(
            lat, lon, raio, null, Opcoes, out ConsultaProximidade? consulta, out ProblemDetails? erro);

        ok.Should().BeFalse();
        consulta.Should().BeNull();
        erro!.Status.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Theory(DisplayName = "CA-04: lat/long/raio fora de faixa retornam 400")]
    [InlineData(91.0, 0.0, 10.0)]     // lat > 90
    [InlineData(-91.0, 0.0, 10.0)]    // lat < -90
    [InlineData(0.0, 181.0, 10.0)]    // long > 180
    [InlineData(0.0, -181.0, 10.0)]   // long < -180
    [InlineData(0.0, 0.0, 0.0)]       // raio = 0
    [InlineData(0.0, 0.0, -5.0)]      // raio < 0
    [InlineData(0.0, 0.0, 600.0)]     // raio > teto (500)
    public void TentarCriar_ForaDeFaixa_Falha(double lat, double lon, double raio)
    {
        bool ok = ConsultaProximidade.TentarCriar(
            lat, lon, raio, null, Opcoes, out ConsultaProximidade? consulta, out ProblemDetails? erro);

        ok.Should().BeFalse();
        consulta.Should().BeNull();
        erro!.Status.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact(DisplayName = "CA-04: lat/long/raio NaN ou infinito retornam 400")]
    public void TentarCriar_NaoFinito_Falha()
    {
        ConsultaProximidade.TentarCriar(double.NaN, 0, 10, null, Opcoes, out _, out ProblemDetails? eLat)
            .Should().BeFalse();
        eLat!.Status.Should().Be(StatusCodes.Status400BadRequest);

        ConsultaProximidade.TentarCriar(0, double.PositiveInfinity, 10, null, Opcoes, out _, out _)
            .Should().BeFalse();
        ConsultaProximidade.TentarCriar(0, 0, double.PositiveInfinity, null, Opcoes, out _, out _)
            .Should().BeFalse();
    }

    [Theory(DisplayName = "CA-04/CA-06: limit <= 0 é inválido (400), não é silenciado por clamp")]
    [InlineData(0)]
    [InlineData(-1)]
    public void TentarCriar_LimitInvalido_Falha(int limit)
    {
        bool ok = ConsultaProximidade.TentarCriar(
            0, 0, 10, limit, Opcoes, out ConsultaProximidade? consulta, out ProblemDetails? erro);

        ok.Should().BeFalse();
        consulta.Should().BeNull();
        erro!.Status.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact(DisplayName = "Boundaries de faixa (±90 lat, ±180 long, teto de raio) são aceitos")]
    public void TentarCriar_Boundaries_Ok()
    {
        ConsultaProximidade.TentarCriar(90, 180, Opcoes.RaioMaxKm, null, Opcoes, out _, out _).Should().BeTrue();
        ConsultaProximidade.TentarCriar(-90, -180, Opcoes.RaioMaxKm, null, Opcoes, out _, out _).Should().BeTrue();
    }

    [Fact(DisplayName = "Options: defaults são válidos (validação fail-fast no boot)")]
    public void Options_Defaults_SaoValidos()
    {
        new GeoProximidadeOptions().LimitesValidos().Should().BeTrue();
    }

    // Parâmetros: (raioMaxKm, limitPadrao, limitMax).
    [Theory(DisplayName = "Options: configuração inválida é rejeitada (RaioMaxKm/LimitMax/LimitPadrao)")]
    [InlineData(0, 50, 200)]      // RaioMaxKm = 0
    [InlineData(-1, 50, 200)]     // RaioMaxKm < 0
    [InlineData(500, 50, 0)]      // LimitMax < 1
    [InlineData(500, 0, 200)]     // LimitPadrao < 1
    [InlineData(500, 300, 200)]   // LimitPadrao > LimitMax
    public void Options_Invalida_Rejeitada(double raioMaxKm, int limitPadrao, int limitMax)
    {
        GeoProximidadeOptions opcoes = new()
        {
            RaioMaxKm = raioMaxKm,
            LimitPadrao = limitPadrao,
            LimitMax = limitMax,
        };

        opcoes.LimitesValidos().Should().BeFalse();
    }
}
