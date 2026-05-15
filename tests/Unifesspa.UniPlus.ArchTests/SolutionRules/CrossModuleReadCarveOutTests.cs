namespace Unifesspa.UniPlus.ArchTests.SolutionRules;

using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnit;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

using ReflectionAssembly = System.Reflection.Assembly;

/// <summary>
/// Fitness test <strong>R8</strong> da ADR-0056 — carve-out read-side cross-módulo:
/// <list type="bullet">
///   <item><description>Nenhum projeto de módulo (Domain/Application/Infrastructure/API)
///   pode depender dos namespaces <c>.Domain</c> ou <c>.Application</c> de outro módulo.</description></item>
///   <item><description>Dependências cross-módulo são permitidas apenas contra
///   <c>{Module}.Contracts</c> ou <c>Governance.Contracts</c> (ADR-0055).</description></item>
///   <item><description><strong>Whitelist S4 do PR #500</strong>:
///   <c>Application.Abstractions</c> pode depender de <c>Governance.Contracts</c>
///   (<c>AreaCodigo</c> é foundation contract usado em <c>IUserContext.AreasAdministradas</c>).</description></item>
/// </list>
/// </summary>
/// <remarks>
/// <para>Roster real dos 5 módulos cobertos:
/// <list type="bullet">
///   <item><description>Selecao (4 layers — Domain, Application, Infrastructure, API)</description></item>
///   <item><description>Ingresso (3 layers — sem Application separada, handlers em Infrastructure)</description></item>
///   <item><description>Portal (3 layers — sem Application separada)</description></item>
///   <item><description>OrganizacaoInstitucional (4 layers)</description></item>
///   <item><description>Parametrizacao (5 layers — inclui Contracts próprio)</description></item>
/// </list>
/// Quando um novo módulo entrar, adicionar ao <see cref="ModulesRoster"/> com os
/// namespace prefixes dele.</para>
///
/// <para>A regra usa <c>NamespaceMatching</c> em vez de project reference para
/// pegar dependências de tipos (uso real) — ProjectReference sem uso não é dep
/// arquitetural; uso de tipo é.</para>
/// </remarks>
public sealed class CrossModuleReadCarveOutTests
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
        "Parametrizacao",
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

    [Fact(DisplayName = "R8 S4: Application.Abstractions só depende de Governance.Contracts e Kernel cross-módulo")]
    public void ApplicationAbstractions_SoDependeDeFoundationCrossModulo()
    {
        // Application.Abstractions hospeda IUserContext.AreasAdministradas → AreaCodigo
        // (de Governance.Contracts) e value objects do Kernel. Qualquer ref para módulo
        // de negócio (Selecao, Ingresso, Portal, OrganizacaoInstitucional, Parametrizacao)
        // viola a invariante "foundation não desce para módulos" — S4 do PR #500.
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

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "ArchUnitNET.Check() pode lançar tipos de exceção variados (XunitException, "
            + "FailedArchRuleException, etc.); este helper consolida todas as violações cross-módulo "
            + "em uma única mensagem de Assert.True para triage rápido — re-throw derrotaria o agregador.")]
    private static void AssertSemDependencia(
        string moduloOrigem,
        string origemPattern,
        string moduloDestino,
        string layerDestino,
        List<string> violations)
    {
        string destinoPattern = $@"^Unifesspa\.UniPlus\.{moduloDestino}{System.Text.RegularExpressions.Regex.Escape(layerDestino)}(\.|$)";

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
        catch (Exception ex)
        {
            // Coleta violações em vez de falhar no primeiro — relatório consolidado
            // facilita o triage quando o fitness pega múltiplas violações de uma vez.
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
            typeof(global::Unifesspa.UniPlus.Governance.Contracts.AreaCodigo).Assembly,
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
            typeof(global::Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities.AreaOrganizacional).Assembly,
            typeof(global::Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Commands.AreasOrganizacionais.CriarAreaOrganizacionalCommand).Assembly,
            typeof(global::Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence.OrganizacaoInstitucionalDbContext).Assembly,
            typeof(global::Unifesspa.UniPlus.OrganizacaoInstitucional.API.OrganizacaoApiAssemblyMarker).Assembly,

            // Parametrizacao
            typeof(global::Unifesspa.UniPlus.Parametrizacao.Domain.ParametrizacaoDomainAssemblyMarker).Assembly,
            typeof(global::Unifesspa.UniPlus.Parametrizacao.Application.ParametrizacaoApplicationAssemblyMarker).Assembly,
            typeof(global::Unifesspa.UniPlus.Parametrizacao.Contracts.ParametrizacaoContractsAssemblyMarker).Assembly,
            typeof(global::Unifesspa.UniPlus.Parametrizacao.Infrastructure.Persistence.ParametrizacaoDbContext).Assembly,
            typeof(global::Unifesspa.UniPlus.Parametrizacao.API.ParametrizacaoApiAssemblyMarker).Assembly,
        ];

        return new ArchLoader().LoadAssemblies(productAssemblies).Build();
    }
}
