namespace Unifesspa.UniPlus.Kernel.UnitTests.CodigosDeContrato;

using System.Collections.Immutable;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

public sealed class FormatoDosCodigosTests
{
    [Theory]
    [InlineData(typeof(FormatoValido))]
    [InlineData(typeof(FormatoFaseFinal))]
    [InlineData(typeof(FormatoVariasPartes))]
    public void CodigoValido_Aceita(Type tipo) =>
        FluentActions.Invoking(() => Declarados(tipo)).Should().NotThrow();

    [Fact]
    public void CodigoCom80Caracteres_Aceita() =>
        FluentActions.Invoking(() => Declarados(typeof(FormatoOitentaCaracteres))).Should().NotThrow();

    [Fact]
    public void CodigoCom81Caracteres_RecusaSemRepetirValor()
    {
        string codigo = new('A', 81);
        string defeito = Defeito(typeof(FormatoOitentaEUmCaracteres));
        defeito.Should().Contain($"{nameof(FormatoOitentaEUmCaracteres)}.{nameof(FormatoOitentaEUmCaracteres.Membro)}").And.Contain("80 caracteres").And.NotContain(codigo);
    }

    [Fact]
    public void CodigoLongoEForaDoFormato_AcumulaOsDoisDefeitos()
    {
        string codigo = new('a', 81);
        IReadOnlyList<string> defeitos = FluentActions.Invoking(() => Declarados(typeof(FormatoLongoForaDoFormato)))
            .Should().Throw<CodigosDeContratoInvalidosException>().Which.Defeitos;
        defeitos.Should().HaveCount(2).And.OnlyContain(d => !d.Contains(codigo, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(typeof(FormatoEspacoNoInicio), " ABC")]
    [InlineData(typeof(FormatoEspacoNoFim), "abc ")]
    public void CodigoComEspacoNasPontas_AcumulaEspacoEFormato(Type tipo, string codigo)
    {
        IReadOnlyList<string> defeitos = Defeitos(tipo);
        defeitos.Should().HaveCount(2).And.OnlyContain(d => !d.Contains(codigo, StringComparison.Ordinal));
        defeitos.Should().ContainSingle(d => d.Contains("espaço", StringComparison.Ordinal));
        defeitos.Should().ContainSingle(d => d.Contains("maiúscula", StringComparison.Ordinal));
    }

    [Fact]
    public void CodigoSomenteComEspacos_AcumulaVazioEspacoEFormato()
    {
        IReadOnlyList<string> defeitos = Defeitos(typeof(FormatoSomenteEspacos));
        defeitos.Should().HaveCount(3);
        defeitos.Should().ContainSingle(d => d.Contains("vazio", StringComparison.Ordinal));
        defeitos.Should().ContainSingle(d => d.Contains("espaço", StringComparison.Ordinal));
        defeitos.Should().ContainSingle(d => d.Contains("maiúscula", StringComparison.Ordinal));
    }

    [Fact]
    public void CodigoVazio_RecusaSomenteComoVazio() =>
        Defeito(typeof(FormatoVazio)).Should().Contain("vazio");

    [Fact]
    public void CodigoLongoComEspaco_AcumulaEspacoFormatoETamanho()
    {
        IReadOnlyList<string> defeitos = Defeitos(typeof(FormatoLongoComEspaco));
        defeitos.Should().HaveCount(3);
        defeitos.Should().ContainSingle(d => d.Contains("espaço", StringComparison.Ordinal));
        defeitos.Should().ContainSingle(d => d.Contains("maiúscula", StringComparison.Ordinal));
        defeitos.Should().ContainSingle(d => d.Contains("80 caracteres", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(typeof(FormatoMinuscula), "minuscula", nameof(FormatoMinuscula))]
    [InlineData(typeof(FormatoAcentuacao), "ACENTUAÇÃO", nameof(FormatoAcentuacao))]
    [InlineData(typeof(FormatoHifen), "COM-HIFEN", nameof(FormatoHifen))]
    [InlineData(typeof(FormatoSublinhados), "DOIS__SUBLINHADOS", nameof(FormatoSublinhados))]
    [InlineData(typeof(FormatoSublinhadoNoInicio), "_INICIO", nameof(FormatoSublinhadoNoInicio))]
    [InlineData(typeof(FormatoSublinhadoNoFim), "FINAL_", nameof(FormatoSublinhadoNoFim))]
    [InlineData(typeof(FormatoNumeroNoInicio), "1INICIO", nameof(FormatoNumeroNoInicio))]
    [InlineData(typeof(FormatoEspacoNoMeio), "ESPACO NO MEIO", nameof(FormatoEspacoNoMeio))]
    public void CodigoForaDoFormato_RecusaSemRepetirValor(Type tipo, string codigo, string nome)
    {
        string defeito = Defeito(tipo);
        defeito.Should().Contain($"{nome}.{nameof(FormatoVazio.Membro)}").And.Contain("maiúscula").And.NotContain(codigo);
    }

    private static string Defeito(Type tipo) =>
        Defeitos(tipo).Should().ContainSingle().Which;

    private static ImmutableArray<string> Defeitos(Type tipo) =>
        FluentActions.Invoking(() => Declarados(tipo))
            .Should().Throw<CodigosDeContratoInvalidosException>().Which.Defeitos;

    private static void Declarados(Type tipo) => EnumsDeContrato.Declarados(tipo);
}
