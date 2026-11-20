namespace Unifesspa.UniPlus.Kernel.UnitTests.CodigosDeContrato;

using System.Reflection;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

public sealed class AssembliesDoUniPlusTests
{
    private static readonly Assembly Testes = typeof(AssembliesDoUniPlusTests).Assembly;

    private static readonly Assembly Kernel = typeof(AssembliesDoUniPlus).Assembly;

    [Fact(DisplayName = "O resultado traz o próprio executável e o Kernel que ele referencia")]
    public void AlcancadosAPartirDe_ExecutavelQueReferenciaOKernel_TrazOsDois() =>
        AssembliesDoUniPlus.AlcancadosAPartirDe(Testes).Should().Contain([Testes, Kernel]);

    [Fact(DisplayName = "Fora a raiz, o resultado só traz assemblies cujo nome começa com o prefixo do Uni+")]
    public void AlcancadosAPartirDe_ForaARaiz_SoTrazAssembliesDoUniPlus() =>
        AssembliesDoUniPlus.AlcancadosAPartirDe(Testes).Where(static a => a != Testes).Select(static a => a.GetName().Name)
            .Should().OnlyContain(static nome => nome != null && nome.StartsWith("Unifesspa.UniPlus.", StringComparison.Ordinal));

    [Fact(DisplayName = "O resultado não repete assembly")]
    public void AlcancadosAPartirDe_NaoRepeteAssembly() =>
        AssembliesDoUniPlus.AlcancadosAPartirDe(Testes).Should().OnlyHaveUniqueItems();

    [Fact(DisplayName = "O resultado vem em ordem ordinal do nome do assembly")]
    public void AlcancadosAPartirDe_VemOrdenadoPeloNome() =>
        AssembliesDoUniPlus.AlcancadosAPartirDe(Testes).Select(static a => a.GetName().Name)
            .Should().BeInAscendingOrder(StringComparer.Ordinal);

    [Fact(DisplayName = "O executável é devolvido mesmo sem o prefixo do Uni+ e sem referência alguma a ele")]
    public void AlcancadosAPartirDe_ExecutavelSemReferenciasDoUniPlus_DevolveSoOExecutavel()
    {
        Assembly semReferencias = typeof(object).Assembly;

        AssembliesDoUniPlus.AlcancadosAPartirDe(semReferencias).Should().ContainSingle().Which.Should().BeSameAs(semReferencias);
    }

    [Fact(DisplayName = "O executável nulo é recusado")]
    public void AlcancadosAPartirDe_ExecutavelNulo_Recusa() =>
        FluentActions.Invoking(static () => AssembliesDoUniPlus.AlcancadosAPartirDe(null!)).Should().Throw<ArgumentNullException>();
}
