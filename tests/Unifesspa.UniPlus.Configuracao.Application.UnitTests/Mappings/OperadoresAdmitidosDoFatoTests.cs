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
        TipoDoDado dominio, QuantidadeDeValores cardinalidade, FonteValoresFato? fonte = null) =>
        [.. OperadoresAdmitidosDoFato.De(FatosCandidatoDeTeste.Declarado(dominio, cardinalidade, fonte))];

    [Fact(DisplayName = "Opção com vários valores devolve os operadores lidos sobre o conjunto, na ordem da tela")]
    public void De_OpcaoComVariosValores_LeSobreOConjunto() =>
        Operadores(TipoDoDado.Opcao, QuantidadeDeValores.VariosValores, FonteValoresFato.OpcoesDoProcesso).Should().Equal(
            Rotulado("IGUAL", "inclui"),
            Rotulado("DIFERENTE", "não inclui"),
            Rotulado("ALGUM_DE", "inclui algum de"),
            Rotulado("NENHUM_DE", "não inclui nenhum de"));

    [Fact(DisplayName = "Opção com um valor devolve os operadores lidos sobre um valor, na ordem da tela")]
    public void De_OpcaoComUmValor_LeSobreUmValor() =>
        Operadores(TipoDoDado.Opcao, QuantidadeDeValores.ValorUnico, FonteValoresFato.Catalogo).Should().Equal(
            Rotulado("IGUAL", "é igual a"),
            Rotulado("DIFERENTE", "é diferente de"),
            Rotulado("ALGUM_DE", "é um de"),
            Rotulado("NENHUM_DE", "não é nenhum de"));

    [Fact(DisplayName = "Numérico devolve igualdade, diferença e os limites, na ordem da tela")]
    public void De_Numerico_DevolveOsLimites() =>
        Operadores(TipoDoDado.Numerico, QuantidadeDeValores.ValorUnico).Should().Equal(
            Rotulado("IGUAL", "é igual a"),
            Rotulado("DIFERENTE", "é diferente de"),
            Rotulado("MAIOR_IGUAL", "maior ou igual a"),
            Rotulado("MENOR_IGUAL", "menor ou igual a"));

    [Fact(DisplayName = "Booleano devolve só igualdade e diferença, nessa ordem")]
    public void De_Booleano_DevolveIgualdadeEDiferenca() =>
        Operadores(TipoDoDado.SimOuNao, QuantidadeDeValores.ValorUnico).Should().Equal(
            Rotulado("IGUAL", "é igual a"),
            Rotulado("DIFERENTE", "é diferente de"));

    [Fact(DisplayName = "O primeiro operador é sempre a igualdade, que é o padrão da condição nova")]
    public void De_QualquerFatoCitavel_ComecaPelaIgualdade()
    {
        Operadores(TipoDoDado.SimOuNao, QuantidadeDeValores.ValorUnico).First().Codigo.Should().Be(Operador.Igual.Codigo());
        Operadores(TipoDoDado.Numerico, QuantidadeDeValores.ValorUnico).First().Codigo.Should().Be(Operador.Igual.Codigo());
        Operadores(TipoDoDado.Opcao, QuantidadeDeValores.ValorUnico, FonteValoresFato.Catalogo).First().Codigo.Should().Be(Operador.Igual.Codigo());
    }

    [Fact(DisplayName = "Fato de texto, que nenhuma regra cita, não devolve operador")]
    public void De_Texto_NaoDevolveOperador() =>
        Operadores(TipoDoDado.Texto, QuantidadeDeValores.ValorUnico).Should().BeEmpty();

    [Fact(DisplayName = "Fato não categórico com mais de um valor não devolve operador")]
    public void De_NumericoComVariosValores_NaoDevolveOperador() =>
        Operadores(TipoDoDado.Numerico, QuantidadeDeValores.VariosValores).Should().BeEmpty();

    [Fact(DisplayName = "Categórico de conjunto devolve os operadores admitidos pela regra, cada um com o rótulo sobre o conjunto")]
    public void De_CategoricoDeConjunto_DevolveOsAdmitidosComORotuloSobreOConjunto()
    {
        CodigoComRotulo<Operador>[] operadores = Operadores(TipoDoDado.Opcao, QuantidadeDeValores.VariosValores, FonteValoresFato.OpcoesDoProcesso);

        IReadOnlyList<Operador> admitidos = OperadoresDoFato.Admitidos(TipoDominioFato.CategoricoDinamico, QuantidadeDeValores.VariosValores);
        operadores.Select(static o => o.Codigo).Should().Equal(admitidos.Select(static o => o.Codigo()));
        operadores.Select(static o => o.Rotulo).Should().Equal(
            admitidos.Select(static o => OperadoresDoFato.Rotulo(o, QuantidadeDeValores.VariosValores)));
    }

    private static CodigoComRotulo<Operador> Rotulado(string codigo, string rotulo) =>
        CodigoComRotulo<Operador>.De(CatalogoDe<Operador>.Obter(codigo), rotulo);
}
