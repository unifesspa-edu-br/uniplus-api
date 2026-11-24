namespace Unifesspa.UniPlus.Regras.UnitTests.Enums;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Enums;

public sealed class OperadoresDoFatoTests
{
    private static string[] RotulosDe(TipoDominioFato dominio, QuantidadeDeValores quantidade) =>
        [.. OperadoresDoFato.Admitidos(dominio, quantidade).Select(operador => OperadoresDoFato.Rotulo(operador, quantidade))];

    [Fact(DisplayName = "Os operadores admitidos por domínio seguem a matriz da ADR-0111")]
    public void Admitidos_DeCadaDominio_SeguemAMatriz()
    {
        Operador[] booleano = [Operador.Igual, Operador.Diferente];
        Operador[] numerico = [Operador.Igual, Operador.Diferente, Operador.MaiorIgual, Operador.MenorIgual];
        Operador[] categorico = [Operador.Igual, Operador.Diferente, Operador.AlgumDe, Operador.NenhumDe];

        OperadoresDoFato.Admitidos(TipoDominioFato.Booleano).Should().Equal(booleano);
        OperadoresDoFato.Admitidos(TipoDominioFato.Numerico).Should().Equal(numerico);
        OperadoresDoFato.Admitidos(TipoDominioFato.CategoricoEstatico).Should().Equal(categorico);
        OperadoresDoFato.Admitidos(TipoDominioFato.CategoricoDinamico).Should().Equal(categorico);
    }

    [Fact(DisplayName = "Categórico de valor único lê os operadores sobre um valor")]
    public void Rotulo_CategoricoDeValorUnico_LeSobreUmValor() =>
        RotulosDe(TipoDominioFato.CategoricoEstatico, QuantidadeDeValores.ValorUnico)
            .Should().Equal("é igual a", "é diferente de", "é um de", "não é nenhum de");

    [Fact(DisplayName = "Categórico de vários valores lê os operadores sobre o conjunto")]
    public void Rotulo_CategoricoMultivalorado_LeSobreOConjunto() =>
        RotulosDe(TipoDominioFato.CategoricoDinamico, QuantidadeDeValores.VariosValores)
            .Should().Equal("inclui", "não inclui", "inclui algum de", "não inclui nenhum de");

    [Fact(DisplayName = "Numérico admite igualdade, diferença e os limites, lidos sobre um valor")]
    public void Rotulo_Numerico_LeSobreUmValor() =>
        RotulosDe(TipoDominioFato.Numerico, QuantidadeDeValores.ValorUnico)
            .Should().Equal("é igual a", "é diferente de", "maior ou igual a", "menor ou igual a");

    [Fact(DisplayName = "Booleano admite só igualdade e diferença, lidas sobre um valor")]
    public void Rotulo_Booleano_LeSobreUmValor() =>
        RotulosDe(TipoDominioFato.Booleano, QuantidadeDeValores.ValorUnico).Should().Equal("é igual a", "é diferente de");

    [Theory(DisplayName = "Fato não categórico com mais de um valor não admite operador")]
    [InlineData(TipoDominioFato.Booleano)]
    [InlineData(TipoDominioFato.Numerico)]
    public void Admitidos_NaoCategoricoMultivalorado_NaoAdmiteOperador(TipoDominioFato dominio) =>
        OperadoresDoFato.Admitidos(dominio, QuantidadeDeValores.VariosValores).Should().BeEmpty();

    [Theory(DisplayName = "Operador que só vale sobre um valor não tem rótulo de conjunto")]
    [InlineData(Operador.MaiorIgual)]
    [InlineData(Operador.MenorIgual)]
    public void Rotulo_OperadorNumericoSobreConjunto_Recusa(Operador operador) =>
        FluentActions.Invoking(() => OperadoresDoFato.Rotulo(operador, QuantidadeDeValores.VariosValores))
            .Should().Throw<InvalidOperationException>();
}
