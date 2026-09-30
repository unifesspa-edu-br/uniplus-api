namespace Unifesspa.UniPlus.Regras.UnitTests.Services;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// Derivado booleano por regra (ADR-0136): verdadeiro se alguma regra ativa, falso se nenhuma, e
/// indeterminado enquanto uma dependência não se sabe — nunca falso por falta de informação.
/// </summary>
public sealed class MotorDerivacaoBooleanoTests
{
    private static readonly RegrasDerivacaoFato EgressoDeEscolaPublica = RegrasDerivacaoFato.CriarBooleana(
        "EGRESSO_ESCOLA_PUBLICA",
        [RegraDerivacao.CriarBooleana(Predicado("TIPO_ESCOLA", Operador.Em, new[] { "PUBLICA_INTEGRAL", "COMUNITARIA_CAMPO_CONVENIADA" }))],
        ["TIPO_ESCOLA"]).Value!;

    [Theory(DisplayName = "Verdadeiro se alguma regra ativa, falso se nenhuma")]
    [InlineData("PUBLICA_INTEGRAL", true)]
    [InlineData("PRIVADA_BOLSA_INTEGRAL", false)]
    public void Derivar_Booleano(string tipoEscola, bool esperado)
    {
        ResultadoDerivacao resultado = MotorDerivacao.Derivar(
            EgressoDeEscolaPublica,
            new Dictionary<string, FatoResolvido>(StringComparer.Ordinal)
            {
                ["TIPO_ESCOLA"] = FatoResolvido.Resolvido(JsonSerializer.SerializeToElement(tipoEscola)),
            });

        resultado.Estado.Should().Be(EstadoFato.Resolvido);
        resultado.ValorBooleano.Should().Be(esperado);
    }

    [Fact(DisplayName = "Com a dependência indeterminada, o derivado booleano é indeterminado, não falso")]
    public void Derivar_DependenciaIndeterminada_Indeterminado() =>
        MotorDerivacao.Derivar(
            EgressoDeEscolaPublica,
            new Dictionary<string, FatoResolvido>(StringComparer.Ordinal) { ["TIPO_ESCOLA"] = FatoResolvido.Indeterminado() })
            .Estado.Should().Be(EstadoFato.Indeterminado);

    [Fact(DisplayName = "Derivação booleana recusa regra que contribui código")]
    public void CriarBooleana_RegraComContribuicao_Recusa() =>
        RegrasDerivacaoFato.CriarBooleana(
            "EGRESSO_ESCOLA_PUBLICA",
            [RegraDerivacao.Criar(Predicado("TIPO_ESCOLA", Operador.Igual, "PUBLICA_INTEGRAL"), "SIM").Value!],
            ["TIPO_ESCOLA"]).Error!.Code.Should().Be(RegrasDerivacaoFatoErrorCodes.ContribuiForaDoDominio);

    private static PredicadoDnf Predicado(string fato, Operador operador, object valor) =>
        PredicadoDnf.CriarDeCondicoesAgrupadas([(0, CondicaoDnf.Criar(fato, operador, JsonSerializer.SerializeToElement(valor)).Value!)]).Value!;
}
