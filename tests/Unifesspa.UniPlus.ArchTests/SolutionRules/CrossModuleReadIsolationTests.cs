namespace Unifesspa.UniPlus.ArchTests.SolutionRules;

using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnit;

using AwesomeAssertions;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

using ReflectionAssembly = System.Reflection.Assembly;
using ReflectionType = System.Type;

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
/// <remarks>
/// <para>Roster real dos 6 módulos cobertos:
/// <list type="bullet">
///   <item><description>Selecao (4 layers — Domain, Application, Infrastructure, API)</description></item>
///   <item><description>Ingresso (3 layers — sem Application separada, handlers em Infrastructure)</description></item>
///   <item><description>Portal (3 layers — sem Application separada)</description></item>
///   <item><description>OrganizacaoInstitucional (4 layers)</description></item>
///   <item><description>Configuracao (5 layers — inclui Contracts próprio)</description></item>
///   <item><description>Geo (5 layers — inclui Contracts próprio; banco isolado com PostGIS)</description></item>
/// </list>
/// Quando um novo módulo entrar, adicionar ao <see cref="ModulesRoster"/> com os
/// namespace prefixes dele.</para>
///
/// <para>A regra usa <c>NamespaceMatching</c> em vez de project reference para
/// pegar dependências de tipos (uso real) — ProjectReference sem uso não é dep
/// arquitetural; uso de tipo é.</para>
/// </remarks>
public sealed class CrossModuleReadIsolationTests
{
    /// <summary>
    /// Roster dos módulos da plataforma. Cada entrada é o namespace raiz; as
    /// camadas internas (<c>.Domain</c>, <c>.Application</c>, etc.) seguem por convenção.
    /// </summary>
    private static readonly string[] ModulesRoster =
    [
        "Selecao",
        "Ingresso",
        "Portal",
        "OrganizacaoInstitucional",
        "Configuracao",
        "Geo",
    ];

    private static readonly Architecture SolutionArchitecture = LoadSolutionArchitecture();

    [Fact(DisplayName = "R8: nenhum módulo depende do Domain ou Application de outro módulo")]
    public void Modulos_NaoDependemDeDomainOuApplicationDeOutros()
    {
        List<string> violations = [];

        foreach (string moduloOrigem in ModulesRoster)
        {
            string origemPattern = $@"^Unifesspa\.UniPlus\.{moduloOrigem}(\.|$)";

            foreach (string moduloDestino in ModulesRoster)
            {
                if (string.Equals(moduloOrigem, moduloDestino, StringComparison.Ordinal))
                {
                    continue;
                }

                // Dois namespaces "off-limits" cross-módulo: .Domain e .Application
                // (.Infrastructure e .API também são proibidos por construção — quem
                // referencia Application de outro módulo já viola, então Infrastructure/API
                // ficam cobertos transitivamente).
                AssertSemDependencia(moduloOrigem, origemPattern, moduloDestino, ".Domain", violations);
                AssertSemDependencia(moduloOrigem, origemPattern, moduloDestino, ".Application", violations);
                AssertSemDependencia(moduloOrigem, origemPattern, moduloDestino, ".Infrastructure", violations);
                AssertSemDependencia(moduloOrigem, origemPattern, moduloDestino, ".API", violations);
            }
        }

        Assert.True(
            violations.Count == 0,
            "ADR-0056 §\"Fitness tests\" exige que dependências cross-módulo sejam apenas contra "
            + "{Module}.Contracts ou Governance.Contracts. Violações encontradas:\n  - "
            + string.Join("\n  - ", violations));
    }

    [Fact(DisplayName = "R8 (Program top-level): tipos no global namespace de cada API não dependem de outros módulos")]
    public void ProgramsApi_NaoDependemDeOutrosModulos()
    {
        // Top-level statements emitem `Program` no global namespace (Type.Namespace
        // null), invisíveis ao predicate por-namespace do fitness principal.
        // ArchUnitNET inspeciona método body via metadata IL — usar a fluent rule
        // com filtro `HaveNameMatching("^Program$")` AND `ResideInAssembly(...)`
        // captura DI wiring, extension method calls e qualquer dep arquitetural
        // que apareça apenas no body de Main, não em signature.

        (string Modulo, ReflectionAssembly Asm)[] apiAssembliesPorModulo =
        [
            ("Selecao", typeof(global::Unifesspa.UniPlus.Selecao.API.Controllers.EditalController).Assembly),
            ("Ingresso", typeof(global::Unifesspa.UniPlus.Ingresso.API.IngressoApiAssemblyMarker).Assembly),
            ("Portal", typeof(global::Unifesspa.UniPlus.Portal.API.PortalApiAssemblyMarker).Assembly),
            ("OrganizacaoInstitucional", typeof(global::Unifesspa.UniPlus.OrganizacaoInstitucional.API.OrganizacaoApiAssemblyMarker).Assembly),
            ("Configuracao", typeof(global::Unifesspa.UniPlus.Configuracao.API.ConfiguracaoApiAssemblyMarker).Assembly),
            ("Geo", typeof(global::Unifesspa.UniPlus.Geo.API.GeoApiAssemblyMarker).Assembly),
        ];

        List<string> violations = [];

        foreach ((string modulo, ReflectionAssembly asm) in apiAssembliesPorModulo)
        {
            // Carrega só este assembly — ArchUnitNET enxergará as dependências
            // método-corpo nele E nos assemblies referenciados.
            Architecture single = new ArchLoader().LoadAssemblies(asm).Build();

            foreach (string moduloOutro in ModulesRoster)
            {
                if (string.Equals(modulo, moduloOutro, StringComparison.Ordinal))
                {
                    continue;
                }

                // Filtro alvo: layers internos do outro módulo (Domain/Application/
                // Infrastructure/API). Contracts próprio do outro módulo é exceção
                // documentada — apenas Configuracao tem em V1.
                string destinoPattern = $@"^Unifesspa\.UniPlus\.{moduloOutro}\.(Domain|Application|Infrastructure|API)(\.|$)";

                IArchRule rule = Types()
                    .That()
                    .HaveNameMatching("^Program$")
                    .Should()
                    .NotDependOnAnyTypesThat()
                    .ResideInNamespaceMatching(destinoPattern);

                try
                {
                    rule.Check(single);
                }
                catch (Xunit.Sdk.XunitException ex)
                {
                    // ArchUnitNET.xUnit traduz violação em FailedArchRuleException, que
                    // herda de XunitException. Outros tipos não são esperados aqui — se
                    // surgir, propaga (mantém fail-fast em erros desconhecidos).
                    violations.Add($"{modulo}/Program → {moduloOutro}: {ex.Message.Split('\n')[0]}");
                }
            }
        }

        violations.Should().BeEmpty(
            "R8 (Program top-level): Program.cs de uma API não pode usar tipos de Domain/Application/"
            + "Infrastructure/API de outro módulo (Contracts próprio do outro módulo OK). "
            + $"Violações:\n  - {string.Join("\n  - ", violations)}");
    }

    [Fact(DisplayName = "R8 (host composition root): o host é isento do roster e PODE compor os 4 módulos internos")]
    public void HostCompositionRoot_ComposeTodosOsModulosInternos()
    {
        // Contraponto POSITIVO ao R8: enquanto nenhum MÓDULO pode depender de
        // outro (fatos acima), o host do monólito modular (spike) é a ÚNICA
        // exceção autorizada — o composition root compõe os 4 módulos internos
        // num processo único via Add{Modulo}Module + discovery Wolverine. Por
        // isso fica FORA do ModulesRoster (senão os fatos R8 o acusariam de
        // depender de todos). Este fato trava a regressão oposta: se o host
        // parar de compor algum módulo, a composição do monólito quebrou.
        ModulesRoster.Should().NotContain(
            "Host",
            "o composition root é isento do R8 — ele é o único autorizado a compor todos os módulos");

        // Reflexão sobre os assemblies referenciados (não ArchUnitNET fluent): o
        // host referencia as 4 .API, cada uma com seu próprio `Program` top-level
        // — carregar o host no ArchLoader traria 5 `Program` ao grafo, tornando o
        // filtro por nome ambíguo. A referência de assembly é evidência direta e
        // determinística da composição (o Add{Modulo}Module vive no .API).
        ReflectionAssembly hostAssembly =
            typeof(global::Unifesspa.UniPlus.Host.HostAssemblyMarker).Assembly;

        HashSet<string> referenciados = hostAssembly
            .GetReferencedAssemblies()
            .Select(nome => nome.Name ?? string.Empty)
            .ToHashSet(StringComparer.Ordinal);

        string[] modulosInternos = ["Selecao", "Ingresso", "Configuracao", "OrganizacaoInstitucional"];
        List<string> naoCompostos =
        [
            .. modulosInternos.Where(modulo => !referenciados.Contains($"Unifesspa.UniPlus.{modulo}.API")),
        ];

        naoCompostos.Should().BeEmpty(
            "o host do monólito modular deve compor os 4 módulos internos (Selecao, Ingresso, "
            + "Configuracao, OrganizacaoInstitucional) via Add<Modulo>Module no .API de cada um. "
            + $"Módulos não compostos: {string.Join(", ", naoCompostos)}");
    }

    [Fact(DisplayName = "R8 S4: Application.Abstractions só depende de Governance.Contracts e Kernel cross-módulo")]
    public void ApplicationAbstractions_SoDependeDeFoundationCrossModulo()
    {
        // Application.Abstractions é foundation cross-módulo: pode depender de
        // Governance.Contracts e dos value objects do Kernel, mas de nada mais.
        // Qualquer ref para módulo de negócio (Selecao, Ingresso, Portal,
        // OrganizacaoInstitucional, Configuracao) viola a invariante
        // "foundation não desce para módulos de negócio" — S4.
        foreach (string modulo in ModulesRoster)
        {
            string destinoPattern = $@"^Unifesspa\.UniPlus\.{modulo}(\.|$)";

            IArchRule rule = Types()
                .That()
                .ResideInNamespaceMatching(@"^Unifesspa\.UniPlus\.Application\.Abstractions(\.|$)")
                .Should()
                .NotDependOnAnyTypesThat()
                .ResideInNamespaceMatching(destinoPattern)
                .Because(
                    $"R8 S4 do PR #500: Application.Abstractions é foundation cross-módulo — "
                    + $"não pode descer para o módulo de negócio '{modulo}'. "
                    + "Permitido: Governance.Contracts e Kernel apenas.");

            rule.Check(SolutionArchitecture);
        }
    }

    private static void AssertSemDependencia(
        string moduloOrigem,
        string origemPattern,
        string moduloDestino,
        string layerDestino,
        List<string> violations)
    {
        string destinoPattern = $@"^Unifesspa\.UniPlus\.{moduloDestino}{System.Text.RegularExpressions.Regex.Escape(layerDestino)}(\.|$)";

        // Predicate de origem inclui o namespace canônico do módulo. Para
        // captar `Program` top-level statements (vive no global namespace),
        // o teste em separado [Fact] ProgramsApi_NaoDependemDeOutrosModulos
        // varre os Program de cada API por reflection no assembly do módulo.
        IArchRule rule = Types()
            .That()
            .ResideInNamespaceMatching(origemPattern)
            .Should()
            .NotDependOnAnyTypesThat()
            .ResideInNamespaceMatching(destinoPattern);

        try
        {
            rule.Check(SolutionArchitecture);
        }
        catch (Xunit.Sdk.XunitException ex)
        {
            // ArchUnitNET.xUnit traduz violação em FailedArchRuleException, que
            // herda de XunitException. Coleta violações em vez de falhar no primeiro
            // — relatório consolidado facilita triage de múltiplas violações.
            violations.Add($"{moduloOrigem} → {moduloDestino}{layerDestino}: {ex.Message.Split('\n')[0]}");
        }
    }

    private static Architecture LoadSolutionArchitecture()
    {
        // Carrega apenas assemblies do produto — todos os 5 módulos + shared foundation.
        // Tipos âncora são intencionalmente públicos e resistentes a refactors.
        ReflectionAssembly[] productAssemblies =
        [
            // Shared foundation
            typeof(global::Unifesspa.UniPlus.Kernel.Domain.Entities.EntityBase).Assembly,
            typeof(global::Unifesspa.UniPlus.Governance.Contracts.UnidadeView).Assembly,
            typeof(global::Unifesspa.UniPlus.Application.Abstractions.Messaging.ICommandBus).Assembly,
            typeof(global::Unifesspa.UniPlus.Infrastructure.Core.Messaging.WolverineOutboxConfiguration).Assembly,

            // Selecao
            typeof(global::Unifesspa.UniPlus.Selecao.Domain.Entities.Edital).Assembly,
            typeof(global::Unifesspa.UniPlus.Selecao.Application.Commands.Editais.CriarEditalCommand).Assembly,
            typeof(global::Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.SelecaoDbContext).Assembly,
            typeof(global::Unifesspa.UniPlus.Selecao.API.Controllers.EditalController).Assembly,

            // Ingresso
            typeof(global::Unifesspa.UniPlus.Ingresso.Domain.Entities.Chamada).Assembly,
            typeof(global::Unifesspa.UniPlus.Ingresso.Infrastructure.Persistence.IngressoDbContext).Assembly,
            typeof(global::Unifesspa.UniPlus.Ingresso.API.IngressoApiAssemblyMarker).Assembly,

            // Portal
            typeof(global::Unifesspa.UniPlus.Portal.Domain.PortalDomainAssemblyMarker).Assembly,
            typeof(global::Unifesspa.UniPlus.Portal.Infrastructure.Persistence.PortalDbContext).Assembly,
            typeof(global::Unifesspa.UniPlus.Portal.API.PortalApiAssemblyMarker).Assembly,

            // OrganizacaoInstitucional
            typeof(global::Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities.Unidade).Assembly,
            typeof(global::Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Commands.Unidades.CriarUnidadeCommand).Assembly,
            typeof(global::Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence.OrganizacaoInstitucionalDbContext).Assembly,
            typeof(global::Unifesspa.UniPlus.OrganizacaoInstitucional.API.OrganizacaoApiAssemblyMarker).Assembly,

            // Configuracao
            typeof(global::Unifesspa.UniPlus.Configuracao.Domain.ConfiguracaoDomainAssemblyMarker).Assembly,
            typeof(global::Unifesspa.UniPlus.Configuracao.Application.ConfiguracaoApplicationAssemblyMarker).Assembly,
            typeof(global::Unifesspa.UniPlus.Configuracao.Contracts.ConfiguracaoContractsAssemblyMarker).Assembly,
            typeof(global::Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.ConfiguracaoDbContext).Assembly,
            typeof(global::Unifesspa.UniPlus.Configuracao.API.ConfiguracaoApiAssemblyMarker).Assembly,

            // Geo
            typeof(global::Unifesspa.UniPlus.Geo.Domain.GeoDomainAssemblyMarker).Assembly,
            typeof(global::Unifesspa.UniPlus.Geo.Application.GeoApplicationAssemblyMarker).Assembly,
            typeof(global::Unifesspa.UniPlus.Geo.Contracts.GeoContractsAssemblyMarker).Assembly,
            typeof(global::Unifesspa.UniPlus.Geo.Infrastructure.Persistence.GeoDbContext).Assembly,
            typeof(global::Unifesspa.UniPlus.Geo.API.GeoApiAssemblyMarker).Assembly,
        ];

        return new ArchLoader().LoadAssemblies(productAssemblies).Build();
    }
}
