namespace Unifesspa.UniPlus.Regras.UnitTests.Enums;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;

public sealed class CodigosDeContratoDasRegrasTests
{
    [Fact(DisplayName = "Cada resultado ternário tem o código que o contrato publica")]
    public void Codigo_DeCadaTernario_EOQueOContratoPublica() =>
        Enum.GetValues<Ternario>().ToDictionary(static v => v, static v => v.Codigo())
            .Should().Equal(new Dictionary<Ternario, string>
            {
                [Ternario.Indeterminado] = "INDETERMINADO",
                [Ternario.Verdadeiro] = "VERDADEIRO",
                [Ternario.Falso] = "FALSO",
            });

    [Fact(DisplayName = "Cada estado de fato tem o código que o contrato publica")]
    public void Codigo_DeCadaEstadoDeFato_EOQueOContratoPublica() =>
        Enum.GetValues<EstadoFato>().ToDictionary(static v => v, static v => v.Codigo())
            .Should().Equal(new Dictionary<EstadoFato, string>
            {
                [EstadoFato.Indeterminado] = "INDETERMINADO",
                [EstadoFato.NaoAplicavel] = "NAO_APLICAVEL",
                [EstadoFato.Resolvido] = "RESOLVIDO",
                [EstadoFato.NaoInformado] = "NAO_INFORMADO",
            });

    [Fact(DisplayName = "Cada resultado ternário tem o rótulo de negócio")]
    public void Rotulo_DeCadaTernario_EODeNegocio() =>
        Enum.GetValues<Ternario>().ToDictionary(static v => v, static v => v.Rotulo())
            .Should().Equal(new Dictionary<Ternario, string>
            {
                [Ternario.Indeterminado] = "Indeterminado",
                [Ternario.Verdadeiro] = "Verdadeiro",
                [Ternario.Falso] = "Falso",
            });

    [Fact(DisplayName = "Cada estado de fato tem o rótulo de negócio")]
    public void Rotulo_DeCadaEstadoDeFato_EODeNegocio() =>
        Enum.GetValues<EstadoFato>().ToDictionary(static v => v, static v => v.Rotulo())
            .Should().Equal(new Dictionary<EstadoFato, string>
            {
                [EstadoFato.Indeterminado] = "Indeterminado",
                [EstadoFato.NaoAplicavel] = "Não se aplica",
                [EstadoFato.Resolvido] = "Resolvido",
                [EstadoFato.NaoInformado] = "Não informado",
            });

    [Fact(DisplayName = "Cada operador tem o número e o código que o contrato publica")]
    public void Codigo_DeCadaOperador_EOQueOContratoPublica() =>
        Enum.GetValues<Operador>().ToDictionary(static v => (int)v, static v => v.Codigo())
            .Should().Equal(new Dictionary<int, string>
            {
                [1] = "IGUAL",
                [2] = "EM",
                [3] = "MAIOR_IGUAL",
                [4] = "MENOR_IGUAL",
                [5] = "DIFERENTE",
                [6] = "NAO_EM",
            });

    [Fact(DisplayName = "Cada operador tem o rótulo de negócio")]
    public void Rotulo_DeCadaOperador_EODeNegocio() =>
        Enum.GetValues<Operador>().ToDictionary(static v => v, static v => v.Rotulo())
            .Should().Equal(new Dictionary<Operador, string>
            {
                [Operador.Igual] = "igual a",
                [Operador.Em] = "pertence a",
                [Operador.MaiorIgual] = "maior ou igual a",
                [Operador.MenorIgual] = "menor ou igual a",
                [Operador.Diferente] = "diferente de",
                [Operador.NaoEm] = "não pertence a",
            });

    [Fact(DisplayName = "Os operadores admitidos por domínio seguem a matriz da ADR-0111")]
    public void Admitidos_DeCadaDominio_SeguemAMatriz()
    {
        Operador[] booleano = [Operador.Igual, Operador.Diferente];
        Operador[] numerico = [Operador.Igual, Operador.Diferente, Operador.MaiorIgual, Operador.MenorIgual];
        Operador[] categorico = [Operador.Igual, Operador.Diferente, Operador.Em, Operador.NaoEm];

        OperadoresDoDominio.Admitidos(TipoDominioFato.Booleano).Should().Equal(booleano);
        OperadoresDoDominio.Admitidos(TipoDominioFato.Numerico).Should().Equal(numerico);
        OperadoresDoDominio.Admitidos(TipoDominioFato.CategoricoEstatico).Should().Equal(categorico);
        OperadoresDoDominio.Admitidos(TipoDominioFato.CategoricoDinamico).Should().Equal(categorico);
    }

    [Fact(DisplayName = "Os códigos de contrato dos enums das regras passam na conferência da subida")]
    public void ConferirEnumsDe_ProjetoDasRegras_Passa() =>
        FluentActions.Invoking(() => EnumsDeContrato.ConferirEnumsDe([typeof(Ternario).Assembly])).Should().NotThrow();
}
