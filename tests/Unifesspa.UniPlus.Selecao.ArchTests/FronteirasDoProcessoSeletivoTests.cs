namespace Unifesspa.UniPlus.Selecao.ArchTests;

using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnit;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;

using static ArchUnitNET.Fluent.ArchRuleDefinition;


/// <summary>
/// Verifica as dependências entre as políticas e as camadas, a ausência de estado mutável nas políticas
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

    [Fact(DisplayName = "A3: as políticas não têm campo nem propriedade reatribuível")]
    public void A3_PoliticasNaoGuardamEstadoMutavel() =>
        ConferirQueNaoHaEstadoReatribuivel(Arquitetura, NamespaceDasPoliticas);

    [Theory(DisplayName = "A3: a regra detecta o estado reatribuível")]
    [InlineData("Unifesspa.UniPlus.Selecao.ArchTests.Amostras.ComCampo", "_contador")]
    [InlineData("Unifesspa.UniPlus.Selecao.ArchTests.Amostras.ComPropriedade", "Nome")]
    public void A3_RegraDetectaEstadoReatribuivel(string espacoDeNomes, string membro)
    {
        Architecture amostra = new ArchLoader().LoadAssemblies(typeof(FronteirasDoProcessoSeletivoTests).Assembly).Build();

        Action conferir = () => ConferirQueNaoHaEstadoReatribuivel(amostra, espacoDeNomes);

        conferir.Should().Throw<FailedArchRuleException>().Which.Message.Should().Contain(membro);
    }

    // A imutabilidade é a do membro: a lista somente leitura de uma política pode receber itens, e o que impede
    // isso é a política não expor nem repassar a coleção, não esta regra.
    private static void ConferirQueNaoHaEstadoReatribuivel(Architecture arquitetura, string espacoDeNomes)
    {
        IObjectProvider<IType> politicas = Types()
            .That().ResideInNamespaceMatching($@"^{espacoDeNomes.Replace(".", @"\.", StringComparison.Ordinal)}(\.|$)")
            .And().DoNotHaveNameContaining("<");

        FieldMembers().That().AreDeclaredIn(politicas).And().AreNotStatic()
            .Should().BeImmutable()
            .Because("a política é função da vista: estática, registro ou selada de campos somente leitura; estado reatribuível vive na raiz")
            .Check(arquitetura);
        PropertyMembers().That().AreDeclaredIn(politicas)
            .Should().BeImmutable()
            .Because("a política não guarda propriedade reatribuível")
            .Check(arquitetura);
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
}
