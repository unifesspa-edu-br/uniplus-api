namespace Unifesspa.UniPlus.Selecao.ArchTests;

using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Fluent.Syntax.Elements.Types.Classes;
using ArchUnitNET.xUnit;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

/// <summary>
/// Verifica as dependências entre as políticas e as camadas, o formato estático das políticas
/// e a localização das partes do agregado <see cref="ProcessoSeletivo"/>.
/// A restrição dos chamadores dos mutadores é acompanhada na issue #2423.
/// </summary>
public sealed class FronteirasDoProcessoSeletivoTests
{
    private const string NamespaceDasPoliticas = "Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos";

    private static readonly Architecture Arquitetura = ArquiteturaDaSelecao.Produto;

    [Fact(DisplayName = "A1: nenhuma política depende da raiz")]
    public void A1_PoliticasNaoDependemDaRaiz()
    {
        IArchRule regra = Types()
            .That().ResideInNamespaceMatching($@"^{NamespaceDasPoliticas.Replace(".", @"\.", StringComparison.Ordinal)}(\.|$)")
            .Should().NotDependOnAnyTypesThat().HaveFullName(typeof(ProcessoSeletivo).FullName!)
            .Because("a política recebe a configuração declarada e valores, nunca a raiz");

        regra.Check(Arquitetura);
    }

    [Fact(DisplayName = "A3: as classes das políticas são estáticas, salvo os registros")]
    public void A3_PoliticasSaoEstaticas()
    {
        GivenClassesConjunction classes = Classes().That()
            .ResideInNamespaceMatching(@"^Unifesspa\.UniPlus\.Selecao\.Domain\.ProcessosSeletivos(\.|$)")
            .And().AreNotRecord();
        classes.GetObjects(Arquitetura).Should().NotBeEmpty();
        classes.Should().BeAbstract().AndShould().BeSealed().Check(Arquitetura);
    }

    [Fact(DisplayName = "A5: o Domain não depende de Application, Infrastructure, Wolverine nem EF Core")]
    public void A5_DomainNaoDependeDeCamadasExternas()
    {
        IArchRule regra = Types()
            .That().ResideInNamespaceMatching(@"^Unifesspa\.UniPlus\.Selecao\.Domain(\.|$)")
            .Should().NotDependOnAnyTypesThat().ResideInNamespaceMatching(@"^Unifesspa\.UniPlus\.Selecao\.(Application|Infrastructure)(\.|$)")
            .AndShould().NotDependOnAnyTypesThat().ResideInNamespaceMatching(@"^Wolverine(?!\.Attributes(\.|$))(\.|$)")
            .AndShould().NotDependOnAnyTypesThat().ResideInNamespaceMatching(@"^Microsoft\.EntityFrameworkCore(\.|$)");

        regra.Check(Arquitetura);
    }
}
