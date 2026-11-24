namespace Unifesspa.UniPlus.Kernel.UnitTests.CodigosDeContrato;

using System.Collections.Immutable;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

public sealed class FormatoDosCodigosTests
{
    [Theory]
    [InlineData("VALIDO")]
    [InlineData("FASE2_FINAL")]
    [InlineData("UMA_PARTE_OUTRA_3")]
    public void CodigoValido_Aceita(string codigo) =>
        FluentActions.Invoking(() => Declarados(codigo)).Should().NotThrow();

    [Fact]
    public void CodigoCom80Caracteres_Aceita() =>
        FluentActions.Invoking(() => Declarados(new string('A', 80))).Should().NotThrow();

    [Fact]
    public void CodigoCom81Caracteres_RecusaSemRepetirValor()
    {
        string codigo = new('A', 81);
        string defeito = Defeito(codigo);
        defeito.Should().Contain("EnumDeFormato.Membro0").And.Contain("80 caracteres").And.NotContain(codigo);
    }

    [Fact]
    public void CodigoLongoEForaDoFormato_AcumulaOsDoisDefeitos()
    {
        string codigo = new('a', 81);
        IReadOnlyList<string> defeitos = FluentActions.Invoking(() => Declarados(codigo))
            .Should().Throw<CodigosDeContratoInvalidosException>().Which.Defeitos;
        defeitos.Should().HaveCount(2).And.OnlyContain(d => !d.Contains(codigo, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(" ABC")]
    [InlineData("abc ")]
    public void CodigoComEspacoNasPontas_AcumulaEspacoEFormato(string codigo)
    {
        IReadOnlyList<string> defeitos = Defeitos(codigo);
        defeitos.Should().HaveCount(2).And.OnlyContain(d => !d.Contains(codigo, StringComparison.Ordinal));
        defeitos.Should().ContainSingle(d => d.Contains("espaço", StringComparison.Ordinal));
        defeitos.Should().ContainSingle(d => d.Contains("maiúscula", StringComparison.Ordinal));
    }

    [Fact]
    public void CodigoSomenteComEspacos_AcumulaVazioEspacoEFormato()
    {
        IReadOnlyList<string> defeitos = Defeitos("   ");
        defeitos.Should().HaveCount(3);
        defeitos.Should().ContainSingle(d => d.Contains("vazio", StringComparison.Ordinal));
        defeitos.Should().ContainSingle(d => d.Contains("espaço", StringComparison.Ordinal));
        defeitos.Should().ContainSingle(d => d.Contains("maiúscula", StringComparison.Ordinal));
    }

    [Fact]
    public void CodigoVazio_RecusaSomenteComoVazio() =>
        Defeito("").Should().Contain("vazio");

    [Fact]
    public void CodigoLongoComEspaco_AcumulaEspacoFormatoETamanho()
    {
        IReadOnlyList<string> defeitos = Defeitos(" " + new string('A', 80));
        defeitos.Should().HaveCount(3);
        defeitos.Should().ContainSingle(d => d.Contains("espaço", StringComparison.Ordinal));
        defeitos.Should().ContainSingle(d => d.Contains("maiúscula", StringComparison.Ordinal));
        defeitos.Should().ContainSingle(d => d.Contains("80 caracteres", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("minuscula")]
    [InlineData("ACENTUAÇÃO")]
    [InlineData("COM-HIFEN")]
    [InlineData("DOIS__SUBLINHADOS")]
    [InlineData("_INICIO")]
    [InlineData("FINAL_")]
    [InlineData("1INICIO")]
    [InlineData("ESPACO NO MEIO")]
    public void CodigoForaDoFormato_RecusaSemRepetirValor(string codigo)
    {
        string defeito = Defeito(codigo);
        defeito.Should().Contain("EnumDeFormato.Membro0").And.Contain("maiúscula").And.NotContain(codigo);
    }

    private static string Defeito(string codigo) =>
        Defeitos(codigo).Should().ContainSingle().Which;

    private static ImmutableArray<string> Defeitos(string codigo) =>
        FluentActions.Invoking(() => Declarados(codigo))
            .Should().Throw<CodigosDeContratoInvalidosException>().Which.Defeitos;

    private static void Declarados(string codigo)
    {
        Type tipo = EnumsCriadosEmExecucao.Definir(EnumsCriadosEmExecucao.NovoModulo(), "EnumDeFormato", (codigo, "Rótulo", 1));
        EnumsDeContrato.Declarados(tipo);
    }
}
