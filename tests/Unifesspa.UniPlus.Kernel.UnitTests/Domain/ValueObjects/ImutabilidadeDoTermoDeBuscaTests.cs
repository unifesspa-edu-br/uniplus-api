namespace Unifesspa.UniPlus.Kernel.UnitTests.Domain.ValueObjects;

using System.Reflection;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;

public sealed class ImutabilidadeDoTermoDeBuscaTests
{
    [Fact]
    public void TermoDeBusca_Construtores_NaoSaoPublicos() =>
        typeof(TermoDeBusca).GetConstructors().Should().BeEmpty();

    [Fact]
    public void TermoDeBusca_Propriedades_SaoSomenteLeitura() =>
        SomenteLeitura(typeof(TermoDeBusca)).Should().BeTrue();

    [Fact]
    public void SomenteLeitura_PropriedadesComSetOuInit_SaoRecusadas()
    {
        SomenteLeitura(typeof(ComInit)).Should().BeFalse();
        SomenteLeitura(typeof(ComSet)).Should().BeFalse();
        SomenteLeitura(typeof(SemSet)).Should().BeTrue();
    }

    private static bool SomenteLeitura(Type tipo) =>
        tipo.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .All(static propriedade => propriedade.SetMethod is null);

    private sealed class ComInit
    {
        public string Valor { get; init; } = string.Empty;
    }

    private sealed class ComSet
    {
        public string Valor { get; set; } = string.Empty;
    }

    private sealed class SemSet
    {
        public string Valor { get; } = string.Empty;
    }
}
