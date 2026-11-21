namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Mappings;

using AwesomeAssertions;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Configuracao.Application.Mappings;
using Unifesspa.UniPlus.Configuracao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Regras.Enums;

public sealed class OperadoresAdmitidosDoFatoTests
{
    private static CodigoComRotulo<Operador>[] Operadores(
        DominioFato dominio, CardinalidadeFato cardinalidade, FonteValoresFato? fonte = null) =>
        [.. OperadoresAdmitidosDoFato.De(FatosCandidatoDeTeste.Declarado(dominio, cardinalidade, fonte))];

    [Fact(DisplayName = "Categórico multivalorado devolve os operadores lidos sobre o conjunto, na ordem da tela")]
    public void De_CategoricoMultivalorado_LeSobreOConjunto() =>
        Operadores(DominioFato.Categorico, CardinalidadeFato.Multivalorado, FonteValoresFato.Processo).Should().Equal(
            Rotulado("IGUAL", "inclui"),
            Rotulado("DIFERENTE", "não inclui"),
            Rotulado("EM", "inclui algum de"),
            Rotulado("NAO_EM", "não inclui nenhum de"));

    [Fact(DisplayName = "Categórico de valor único devolve os operadores lidos sobre um valor, na ordem da tela")]
    public void De_CategoricoEscalar_LeSobreUmValor() =>
        Operadores(DominioFato.Categorico, CardinalidadeFato.Escalar, FonteValoresFato.Global).Should().Equal(
            Rotulado("IGUAL", "é igual a"),
            Rotulado("DIFERENTE", "é diferente de"),
            Rotulado("EM", "é um de"),
            Rotulado("NAO_EM", "não é nenhum de"));

    [Fact(DisplayName = "Numérico devolve igualdade, diferença e os limites, na ordem da tela")]
    public void De_Numerico_DevolveOsLimites() =>
        Operadores(DominioFato.Numerico, CardinalidadeFato.Escalar).Should().Equal(
            Rotulado("IGUAL", "é igual a"),
            Rotulado("DIFERENTE", "é diferente de"),
            Rotulado("MAIOR_IGUAL", "maior ou igual a"),
            Rotulado("MENOR_IGUAL", "menor ou igual a"));

    [Fact(DisplayName = "Booleano devolve só igualdade e diferença, nessa ordem")]
    public void De_Booleano_DevolveIgualdadeEDiferenca() =>
        Operadores(DominioFato.Booleano, CardinalidadeFato.Escalar).Should().Equal(
            Rotulado("IGUAL", "é igual a"),
            Rotulado("DIFERENTE", "é diferente de"));

    [Fact(DisplayName = "O primeiro operador é sempre a igualdade, que é o padrão da condição nova")]
    public void De_QualquerFatoCitavel_ComecaPelaIgualdade()
    {
        Operadores(DominioFato.Booleano, CardinalidadeFato.Escalar).First().Codigo.Should().Be(Operador.Igual.Codigo());
        Operadores(DominioFato.Numerico, CardinalidadeFato.Escalar).First().Codigo.Should().Be(Operador.Igual.Codigo());
        Operadores(DominioFato.Categorico, CardinalidadeFato.Escalar, FonteValoresFato.Global).First().Codigo.Should().Be(Operador.Igual.Codigo());
    }

    [Fact(DisplayName = "Fato de texto, que nenhuma regra cita, não devolve operador")]
    public void De_Texto_NaoDevolveOperador() =>
        Operadores(DominioFato.Texto, CardinalidadeFato.Escalar).Should().BeEmpty();

    [Fact(DisplayName = "Fato não categórico com mais de um valor não devolve operador")]
    public void De_NumericoMultivalorado_NaoDevolveOperador() =>
        Operadores(DominioFato.Numerico, CardinalidadeFato.Multivalorado).Should().BeEmpty();

    [Fact(DisplayName = "Categórico de conjunto devolve os operadores admitidos pela regra, cada um com o rótulo sobre o conjunto")]
    public void De_CategoricoDeConjunto_DevolveOsAdmitidosComORotuloSobreOConjunto()
    {
        CodigoComRotulo<Operador>[] operadores = Operadores(DominioFato.Categorico, CardinalidadeFato.Multivalorado, FonteValoresFato.Processo);

        IReadOnlyList<Operador> admitidos = OperadoresDoFato.Admitidos(TipoDominioFato.CategoricoDinamico, CardinalidadeDoFato.Conjunto);
        operadores.Select(static o => o.Codigo).Should().Equal(admitidos.Select(static o => o.Codigo()));
        operadores.Select(static o => o.Rotulo).Should().Equal(
            admitidos.Select(static o => OperadoresDoFato.Rotulo(o, CardinalidadeDoFato.Conjunto)));
    }

    private static CodigoComRotulo<Operador> Rotulado(string codigo, string rotulo) =>
        CodigoComRotulo<Operador>.De(CatalogoDe<Operador>.Obter(codigo), rotulo);
}
