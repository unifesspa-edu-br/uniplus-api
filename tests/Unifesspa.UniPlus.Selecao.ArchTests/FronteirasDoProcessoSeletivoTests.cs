namespace Unifesspa.UniPlus.Selecao.ArchTests;

using System.Runtime.CompilerServices;

using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
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
    private const string EstadoFullName = NamespaceDasPoliticas + ".Edicao.EstadoDaConfiguracao";
    private const string ParteFullName = "Unifesspa.UniPlus.Selecao.Domain.Entities.IParteDoProcesso";

    private static readonly Architecture Arquitetura = new ArchLoader().LoadAssemblies(
        typeof(ProcessoSeletivo).Assembly,
        typeof(Application.Commands.ProcessosSeletivos.CriarProcessoSeletivoCommand).Assembly,
        typeof(Infrastructure.Persistence.SelecaoDbContext).Assembly).Build();

    [Fact(DisplayName = "A1: nenhuma política depende da raiz, salvo a fábrica da vista")]
    public void A1_PoliticasNaoDependemDaRaiz()
    {
        IArchRule regra = Types()
            .That().ResideInNamespaceMatching($@"^{NamespaceDasPoliticas.Replace(".", @"\.", StringComparison.Ordinal)}(\.|$)")
            .And().DoNotHaveFullNameStartingWith(EstadoFullName)
            .Should().NotDependOnAnyTypesThat().HaveFullName(typeof(ProcessoSeletivo).FullName!)
            .Because("a política recebe a vista da configuração; só a fábrica da vista conhece a raiz");

        regra.Check(Arquitetura);
    }

    [Fact(DisplayName = "A3: as classes das políticas são estáticas, salvo os registros")]
    public void A3_PoliticasSaoEstaticas()
    {
        TiposDasPoliticas().Count().Should().BeGreaterThan(30, "a varredura tem de encontrar as políticas");

        string[] naoEstaticas = [.. TiposDasPoliticas()
            .Where(static t => t.IsClass && !(t.IsAbstract && t.IsSealed) && !EhRegistro(t))
            .Select(static t => t.FullName!)];

        naoEstaticas.Should().BeEmpty("a política é função pura da vista; estado vive na raiz");
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

    [Fact(DisplayName = "A6: toda parte do processo está em Domain.Entities")]
    public void A6_PartesDoProcessoVivemNasEntidades()
    {
        string[] fora = [.. typeof(ProcessoSeletivo).Assembly.GetTypes()
            .Where(static t => t.IsClass && t.GetInterfaces().Any(static i => i.FullName == ParteFullName))
            .Where(static t => t.Namespace != typeof(ProcessoSeletivo).Namespace)
            .Select(static t => t.FullName!)];

        fora.Should().BeEmpty("o vínculo com o processo é da entidade filha, não de um tipo de política");
    }

    private static bool EhRegistro(System.Type tipo) => tipo.GetMethod("<Clone>$") is not null;

    private static IEnumerable<System.Type> TiposDasPoliticas() =>
        typeof(ProcessoSeletivo).Assembly.GetTypes()
            .Where(static t => t.Namespace is { } ns && ns.StartsWith(NamespaceDasPoliticas, StringComparison.Ordinal))
            .Where(static t => !System.Attribute.IsDefined(t, typeof(CompilerGeneratedAttribute)));

}
