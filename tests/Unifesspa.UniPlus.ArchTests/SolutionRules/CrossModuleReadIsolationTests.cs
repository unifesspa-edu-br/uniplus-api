namespace Unifesspa.UniPlus.ArchTests.SolutionRules;

using System.Text.RegularExpressions;

using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Fluent.Syntax.Elements.Types;
using ArchUnitNET.xUnit;

using AwesomeAssertions;

using Unifesspa.UniPlus.ArchTests.TestSupport;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

/// <summary>
/// Fitness test <strong>R8</strong> da ADR-0056 — isolamento de leitura cross-módulo:
/// <list type="bullet">
///   <item><description>Nenhum projeto de módulo (Domain/Application/Infrastructure/API)
///   pode depender dos namespaces <c>.Domain</c> ou <c>.Application</c> de outro módulo.</description></item>
///   <item><description>Dependências cross-módulo são permitidas apenas contra
///   <c>{Module}.Contracts</c> ou <c>Governance.Contracts</c> (ADR-0055).</description></item>
///   <item><description><strong>Whitelist S4</strong>:
///   <c>Application.Abstractions</c> pode depender de <c>Governance.Contracts</c>
///   (foundation contracts cross-módulo) e do <c>Kernel</c>.</description></item>
/// </list>
/// </summary>
public sealed class CrossModuleReadIsolationTests
{
    private static readonly Architecture SolutionArchitecture = ArquiteturaDasTravas.Produto;

    private static readonly System.Type[] TiposDasApis =
    [
        typeof(Unifesspa.UniPlus.Autorizacao.API.AutorizacaoApiAssemblyMarker),
        typeof(Unifesspa.UniPlus.Configuracao.API.ConfiguracaoApiAssemblyMarker),
        typeof(Unifesspa.UniPlus.Discentes.API.DiscentesApiAssemblyMarker),
        typeof(Unifesspa.UniPlus.Ingresso.API.IngressoApiAssemblyMarker),
        typeof(Unifesspa.UniPlus.OrganizacaoInstitucional.API.OrganizacaoApiAssemblyMarker),
        typeof(Unifesspa.UniPlus.Portal.API.PortalApiAssemblyMarker),
        typeof(Unifesspa.UniPlus.Publicacoes.API.PublicacoesApiAssemblyMarker),
        typeof(Unifesspa.UniPlus.Selecao.API.SelecaoApiAssemblyMarker),
    ];

    private static readonly string[] ApisEsperadas = [.. TiposDasApis.Select(static tipo =>
        Types().That().Are(tipo).GetObjects(SolutionArchitecture).Single().Assembly.Name)];

    private static readonly string[] ModulesRoster = [.. ApisEsperadas
        .Select(static nome => nome["Unifesspa.UniPlus.".Length..^".API".Length])];

    private static readonly string[] NamespacesDosModulos = [.. ModulesRoster.Select(static nome => $"Unifesspa.UniPlus.{nome}")];

    [Fact(DisplayName = "R8: nenhum módulo depende do Domain, Application, Infrastructure ou API de outro módulo")]
    public void Modulos_NaoDependemDeDomainOuApplicationDeOutros()
    {
        List<string> violations = ViolacoesDeFronteira(SolutionArchitecture, NamespacesDosModulos);

        Assert.True(
            violations.Count == 0,
            "ADR-0056 §\"Fitness tests\" exige que dependências cross-módulo sejam apenas contra "
            + "{Module}.Contracts ou Governance.Contracts. Violações encontradas:\n  - "
            + string.Join("\n  - ", violations));
    }

    [Fact(DisplayName = "Fitness_ModuloQueUsaOutroModulo_Falha")]
    public void Fitness_ModuloQueUsaOutroModulo_Falha()
    {
        List<string> reprovados = ViolacoesDeFronteira(ArquiteturaDasTravas.Fixtures, ["Unifesspa.UniPlus.ArchTests.Fixtures.Modulos.Alfa", "Unifesspa.UniPlus.ArchTests.Fixtures.Modulos.Beta"]);

        reprovados.Should().Contain(static v => v.Contains("UsaODominioDeBeta", StringComparison.Ordinal));
        reprovados.Should().Contain(static v => v.Contains("UsaAInfraestruturaDeBeta", StringComparison.Ordinal));
        reprovados.Should().NotContain(static v => v.Contains("UsaOContratoDeBeta", StringComparison.Ordinal));
        reprovados.Should().NotContain(static v => v.Contains("UsaODominioDeAlfa", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "R8 (Program top-level): tipos no global namespace de cada API não dependem de outros módulos")]
    public void ProgramsApi_NaoDependemDeOutrosModulos()
    {
        foreach (string modulo in ModulesRoster.Where(static m => m != "Portal"))
        {
            string destino = $@"^Unifesspa\.UniPlus\.{modulo}\.(Domain|Application|Infrastructure|API)(\.|$)";
            Types().That().Are(SolutionArchitecture.Types.Where(static t => t.Name == "Program"
                    && t.Assembly.Name == "Unifesspa.UniPlus.Portal.API"))
                .Should().NotDependOnAnyTypesThat().ResideInNamespaceMatching(destino)
                .Check(SolutionArchitecture);
        }
    }

    [Fact(DisplayName = "R8: o host referencia as APIs dos módulos internos, exceto o Portal")]
    public void HostCompositionRoot_ComposeTodosOsModulosInternos()
    {
        ArchUnitNET.Domain.Assembly host = SolutionArchitecture.Assemblies.Single(static a => a.Name == "Unifesspa.UniPlus.Host");
        ApisAusentes(host.ReferencedAssemblyNames).Should().BeEmpty();
        host.ReferencedAssemblyNames.Should().NotContain("Unifesspa.UniPlus.Portal.API");
    }

    [Fact]
    public void Composicao_SemUmaApi_IdentificaModuloAusente()
    {
        ArchUnitNET.Domain.Assembly host = SolutionArchitecture.Assemblies.Single(static a => a.Name == "Unifesspa.UniPlus.Host");
        string[] referencias = [.. host.ReferencedAssemblyNames.Where(static nome => nome != "Unifesspa.UniPlus.Ingresso.API")];
        ApisAusentes(referencias).Should().ContainSingle().Which.Should().Be("Unifesspa.UniPlus.Ingresso.API");
    }

    private static string[] ApisAusentes(IEnumerable<string> referencias) =>
        [.. ApisEsperadas.Where(static api => api != "Unifesspa.UniPlus.Portal.API").Except(referencias)];

    [Fact(DisplayName = "R8 S4: Application.Abstractions só depende de Governance.Contracts e Kernel cross-módulo")]
    public void ApplicationAbstractions_SoDependeDeFoundationCrossModulo()
    {
        foreach (string modulo in ModulesRoster)
        {
            RegraDasAbstracoes(modulo).Check(SolutionArchitecture);
        }
    }

    private static TypesShouldConjunctionWithDescription RegraDasAbstracoes(string modulo) =>
        Types().That().ResideInNamespaceMatching(@"^Unifesspa\.UniPlus\.Application\.Abstractions(\.|$)")
            .Should().NotDependOnAnyTypesThat().ResideInNamespaceMatching($@"^Unifesspa\.UniPlus\.{modulo}(\.|$)")
            .Because("Application.Abstractions só depende de Governance.Contracts e Kernel entre os projetos do Uni+");

    // Para cada par de módulos, a origem não depende de Domain, Application, Infrastructure nem API do destino (.Infrastructure e
    // .API ficam cobertos transitivamente por quem referencia Application, mas a regra os nomeia). Só o Contracts do destino e o
    // Governance.Contracts são alcançáveis. `Program` top-level vive no namespace global e tem o teste próprio acima.
    private static List<string> ViolacoesDeFronteira(Architecture arquitetura, string[] namespacesDosModulos) =>
        [
            .. namespacesDosModulos
                .SelectMany(origem => namespacesDosModulos.Where(destino => destino != origem), (origem, destino) => (Origem: origem, Destino: destino))
                .SelectMany(par => ArquiteturaDasTravas.Reprovacoes(RegraDeFronteira(par.Origem, par.Destino), arquitetura)
                    .Select(descricao => $"{par.Origem} → {par.Destino}: {descricao.Split('\n')[0]}")),
        ];

    private static TypesShouldConjunction RegraDeFronteira(string origem, string destino) =>
        Types()
            .That()
            .ResideInNamespaceMatching($@"^{Regex.Escape(origem)}(\.|$)")
            .Should()
            .NotDependOnAnyTypesThat()
            .ResideInNamespaceMatching($@"^{Regex.Escape(destino)}\.(Domain|Application|Infrastructure|API)(\.|$)");

}
