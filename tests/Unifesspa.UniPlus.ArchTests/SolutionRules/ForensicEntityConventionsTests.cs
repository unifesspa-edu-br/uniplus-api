namespace Unifesspa.UniPlus.ArchTests.SolutionRules;

using System.Linq;
using System.Reflection;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;

/// <summary>
/// Fitness function da ADR-0063: <see cref="IForensicEntity"/> é mutuamente
/// exclusivo com <see cref="EntityBase"/> — entidades append-only de evidência
/// forense não carregam soft-delete e expõem apenas <c>INSERT</c> via factory.
/// </summary>
/// <remarks>
/// <para>
/// Por que reflection ao invés de ArchUnitNET fluent: a regra é simples
/// (interface vs base class) e a verificação por <c>typeof().Assembly</c>
/// itera apenas os assemblies de domínio. Mais legível que o DSL de
/// ArchUnitNET para um único par de invariantes.
/// </para>
/// <para>
/// O conjunto de assemblies inspecionados acompanha o assembly de
/// referência <see cref="EntityBase"/> (Kernel) + assemblies que dependem
/// dele e portanto podem ter implementações concretas. Aplicação inicial:
/// <c>Selecao.Domain</c>. Quando outros módulos ganharem
/// <see cref="IForensicEntity"/>, basta referenciar o projeto no csproj e
/// o teste detecta automaticamente.
/// </para>
/// </remarks>
public sealed class ForensicEntityConventionsTests
{
    [Fact(DisplayName = "ADR-0063: IForensicEntity não herda EntityBase (exclusão mútua)")]
    public void Forensic_NaoHerda_EntityBase()
    {
        IReadOnlyList<Type> forensics = LocalizarTiposForensics();

        forensics.Should().NotBeEmpty(
            "Story #460 introduz IForensicEntity com aplicações concretas; "
            + "ausência de implementações indica regressão no marcador.");

        IEnumerable<string> violacoes = forensics
            .Where(t => typeof(EntityBase).IsAssignableFrom(t))
            .Select(t => t.FullName!);

        violacoes.Should().BeEmpty(
            "ADR-0063: IForensicEntity é mutuamente exclusivo com EntityBase. "
            + "Entidades forensic não herdam soft-delete nem audit interceptor.");
    }

    [Fact(DisplayName = "ADR-0063: tipos IForensicEntity são sealed (impedem herança)")]
    public void Forensic_Sao_Sealed()
    {
        IReadOnlyList<Type> forensics = LocalizarTiposForensics();

        IEnumerable<string> naoSealed = forensics
            .Where(t => !t.IsSealed)
            .Select(t => t.FullName!);

        naoSealed.Should().BeEmpty(
            "ADR-0063: tipos forensic são sealed — herança fragmenta a semântica "
            + "append-only e abre brecha para subclasses adicionarem mutação.");
    }

    [Fact(DisplayName = "ADR-0063: tipos IForensicEntity têm factory estática pública (sem construtor público)")]
    public void Forensic_ExigemFactoryEstatica()
    {
        IReadOnlyList<Type> forensics = LocalizarTiposForensics();

        IEnumerable<string> semFactory = forensics
            .Where(t => t.GetConstructors(BindingFlags.Public | BindingFlags.Instance).Length != 0
                       || !t.GetMethods(BindingFlags.Public | BindingFlags.Static)
                            .Any(m => m.ReturnType == t))
            .Select(t => t.FullName!);

        semFactory.Should().BeEmpty(
            "ADR-0063: tipos forensic não devem ter construtor público — apenas factory "
            + "estática que valida invariantes do snapshot antes de instanciar.");
    }

    private static IReadOnlyList<Type> LocalizarTiposForensics()
    {
        Assembly[] assemblies =
        [
            typeof(EntityBase).Assembly,
            // Apenas assemblies de Domain que JÁ definem IForensicEntity ficam aqui;
            // como typeof(EntityBase).Assembly é Kernel (sem implementações), e as
            // implementações vivem em Selecao.Domain, basta carregar pelo tipo de
            // domínio referenciado.
            typeof(Selecao.Domain.Entities.ObrigatoriedadeLegal).Assembly,
        ];

        return [.. assemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => typeof(IForensicEntity).IsAssignableFrom(t)
                       && t is { IsInterface: false, IsAbstract: false })];
    }
}
