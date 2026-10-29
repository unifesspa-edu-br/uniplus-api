namespace Unifesspa.UniPlus.ArchTests.SolutionRules;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;

/// <summary>
/// O domínio do perfil de acesso conhece só o Kernel. Nem o projeto de autorização que mantém
/// o catálogo nem outro módulo entram nele: o que o perfil precisa do catálogo chega pela
/// porta que o domínio declara.
/// </summary>
public sealed class DominioDaAutorizacaoNaoReferenciaOutroModuloTests
{
    [Fact(DisplayName = "Autorizacao.Domain referencia apenas o Kernel entre os assemblies do Uni+")]
    public void Domain_ReferenciaApenasOKernel()
    {
        string[] referenciadosDoUniPlus =
        [
            .. typeof(Perfil).Assembly
                .GetReferencedAssemblies()
                .Select(static a => a.Name ?? string.Empty)
                .Where(static n => n.StartsWith("Unifesspa.UniPlus.", StringComparison.Ordinal)),
        ];

        referenciadosDoUniPlus.Should().BeEquivalentTo(["Unifesspa.UniPlus.Kernel"]);
    }
}
