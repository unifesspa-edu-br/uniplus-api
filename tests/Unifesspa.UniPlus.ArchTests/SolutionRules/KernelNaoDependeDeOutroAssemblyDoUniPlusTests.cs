namespace Unifesspa.UniPlus.ArchTests.SolutionRules;

using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Fluent.Syntax.Elements.Members.PropertyMembers;

using AwesomeAssertions;

using Unifesspa.UniPlus.ArchTests.Fixtures;
using Unifesspa.UniPlus.ArchTests.TestSupport;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

using ReflectionAssembly = System.Reflection.Assembly;

/// <summary>
/// O Kernel é a base de todos os projetos: nenhum tipo dele depende de tipo que resida em outro assembly do Uni+, nem
/// do <c>Infrastructure.Core</c> nem do <c>Application.Abstractions</c>. A regra do texto pesquisado mora no Kernel
/// justamente para que as duas pontas, aplicação e banco, a consumam. O valor do termo é imutável e só se cria pela
/// fábrica.
/// </summary>
public sealed class KernelNaoDependeDeOutroAssemblyDoUniPlusTests
{
    private static readonly ReflectionAssembly AssemblyDoKernel = typeof(TermoDeBusca).Assembly;

    private static readonly ReflectionAssembly[] OutrosAssembliesDoUniPlus =
        [.. AssembliesDoProduto.Todos.Where(static assembly => assembly != AssemblyDoKernel)];

    [Fact(DisplayName = "Nenhum tipo do Kernel depende de tipo de outro assembly do Uni+")]
    public void TiposDoKernel_NaoDependemDeOutroAssemblyDoUniPlus()
    {
        Types().That().ResideInAssembly(AssemblyDoKernel).GetObjects(ArquiteturaDasTravas.Produto)
            .Should().NotBeEmpty("sem os tipos do Kernel a conferência passaria vazia");

        IArchRule regra = Types()
            .That().ResideInAssembly(AssemblyDoKernel)
            .Should().NotDependOnAnyTypesThat().ResideInAssembly(OutrosAssembliesDoUniPlus[0], OutrosAssembliesDoUniPlus[1..]);

        ArquiteturaDasTravas.Reprovacoes(regra, ArquiteturaDasTravas.Produto).Should().BeEmpty();
    }

    [Fact(DisplayName = "TermoDeBusca só se cria pela fábrica: sem construtor público")]
    public void TermoDeBusca_NaoTemConstrutorPublico()
    {
        IArchRule regra = MethodMembers()
            .That().AreDeclaredIn(typeof(TermoDeBusca)).And().AreConstructors()
            .Should().NotBePublic();

        ArquiteturaDasTravas.Reprovacoes(regra, ArquiteturaDasTravas.Produto).Should().BeEmpty();
    }

    [Fact(DisplayName = "TermoDeBusca é imutável: nenhuma propriedade com set ou init")]
    public void TermoDeBusca_NaoTemPropriedadeComSetOuInit() =>
        ArquiteturaDasTravas.Reprovacoes(SoPropriedadesDeLeitura(typeof(TermoDeBusca)), ArquiteturaDasTravas.Produto)
            .Should().BeEmpty();

    [Fact(DisplayName = "Fitness_PropriedadeComInitOuSet_Falha")]
    public void Fitness_PropriedadeComInitOuSet_Falha()
    {
        ArquiteturaDasTravas.Reprovacoes(SoPropriedadesDeLeitura(typeof(FixturesDeImutabilidade.ComPropriedadeInit)), ArquiteturaDasTravas.Fixtures)
            .Should().NotBeEmpty("a propriedade com init permite mudar o valor depois de criado");
        ArquiteturaDasTravas.Reprovacoes(SoPropriedadesDeLeitura(typeof(FixturesDeImutabilidade.ComPropriedadeSet)), ArquiteturaDasTravas.Fixtures)
            .Should().NotBeEmpty("a propriedade com set público permite mudar o valor depois de criado");
        ArquiteturaDasTravas.Reprovacoes(SoPropriedadesDeLeitura(typeof(FixturesDeImutabilidade.SomenteLeitura)), ArquiteturaDasTravas.Fixtures)
            .Should().BeEmpty();
    }

    private static PropertyMembersShouldConjunction SoPropriedadesDeLeitura(Type tipo) =>
        PropertyMembers().That().AreDeclaredIn(tipo).Should().BeReadOnly();
}
