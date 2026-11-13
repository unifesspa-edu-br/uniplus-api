namespace Unifesspa.UniPlus.Selecao.ArchTests;

using System.Reflection;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Services;

/// <summary>
/// Verifica que <see cref="AvaliadorConformidadeLegal"/> não guarda estado de instância.
/// Dependências de I/O e de relógio exigem outra garantia; ver issue #2423.
/// </summary>
public sealed class AvaliadorConformidadeLegalPurezaTests
{
    [Fact(DisplayName = "AvaliadorConformidadeLegal é static, sem campos e sem construtor — não guarda estado nem dependências")]
    public void ClasseEstaticaSemEstado()
    {
        Type tipo = typeof(AvaliadorConformidadeLegal);

        tipo.IsAbstract.Should().BeTrue("classes static do C# são compiladas como abstract sealed");
        tipo.IsSealed.Should().BeTrue();
        tipo.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Should().BeEmpty("uma função pura não guarda estado entre chamadas");
        tipo.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Should().BeEmpty();
    }

}
