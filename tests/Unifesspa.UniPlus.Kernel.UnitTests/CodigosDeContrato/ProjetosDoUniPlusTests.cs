namespace Unifesspa.UniPlus.Kernel.UnitTests.CodigosDeContrato;

using System.Reflection;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

public sealed class ProjetosDoUniPlusTests
{
    private static readonly Assembly Testes = typeof(ProjetosDoUniPlusTests).Assembly;

    private static readonly Assembly Kernel = typeof(ProjetosDoUniPlus).Assembly;

    [Fact(DisplayName = "O resultado traz o próprio executável e o Kernel que ele usa")]
    public void UsadosPor_ExecutavelQueUsaOKernel_TrazOsDois() =>
        ProjetosDoUniPlus.UsadosPor(Testes).Should().Contain([Testes, Kernel]);

    [Fact(DisplayName = "O resultado só traz assemblies cujo nome começa com o prefixo do Uni+")]
    public void UsadosPor_SoTrazProjetosDoUniPlus() =>
        ProjetosDoUniPlus.UsadosPor(Testes).Select(static a => a.GetName().Name)
            .Should().OnlyContain(static nome => nome != null && nome.StartsWith("Unifesspa.UniPlus.", StringComparison.Ordinal));

    [Fact(DisplayName = "O resultado não repete assembly")]
    public void UsadosPor_NaoRepeteAssembly() =>
        ProjetosDoUniPlus.UsadosPor(Testes).Should().OnlyHaveUniqueItems();

    [Fact(DisplayName = "O resultado vem em ordem ordinal do nome do assembly")]
    public void UsadosPor_VemOrdenadoPeloNome() =>
        ProjetosDoUniPlus.UsadosPor(Testes).Select(static a => a.GetName().Name)
            .Should().BeInAscendingOrder(StringComparer.Ordinal);

    [Fact(DisplayName = "O executável é devolvido mesmo sem o prefixo do Uni+ e sem referência alguma a ele")]
    public void UsadosPor_ExecutavelSemReferenciasDoUniPlus_DevolveSoOExecutavel()
    {
        Assembly semReferencias = typeof(object).Assembly;

        ProjetosDoUniPlus.UsadosPor(semReferencias).Should().ContainSingle().Which.Should().BeSameAs(semReferencias);
    }

    [Fact(DisplayName = "O executável nulo é recusado")]
    public void UsadosPor_ExecutavelNulo_Recusa() =>
        FluentActions.Invoking(static () => ProjetosDoUniPlus.UsadosPor(null!)).Should().Throw<ArgumentNullException>();
}
