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

    [Fact(DisplayName = "Os códigos de contrato dos enums das regras passam na conferência da subida")]
    public void ConferirEnumsDe_ProjetoDasRegras_Passa() =>
        FluentActions.Invoking(() => EnumsDeContrato.ConferirEnumsDe([typeof(Ternario).Assembly])).Should().NotThrow();
}
