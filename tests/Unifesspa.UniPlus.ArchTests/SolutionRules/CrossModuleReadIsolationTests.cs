namespace Unifesspa.UniPlus.ArchTests.SolutionRules;

using System.Text.RegularExpressions;

using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Fluent.Syntax.Elements.Types;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnit;

using AwesomeAssertions;

using Unifesspa.UniPlus.ArchTests.TestSupport;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

using ReflectionAssembly = System.Reflection.Assembly;

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
/// <para>Roster real dos 5 módulos cobertos:
/// <list type="bullet">
///   <item><description>Selecao (4 layers — Domain, Application, Infrastructure, API)</description></item>
///   <item><description>Ingresso (3 layers — sem Application separada, handlers em Infrastructure)</description></item>
///   <item><description>Portal (4 layers — Domain, Application, Infrastructure, API)</description></item>
///   <item><description>OrganizacaoInstitucional (4 layers)</description></item>
///   <item><description>Configuracao (5 layers — inclui Contracts próprio)</description></item>
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
    private static readonly string[] ModulesRoster = ModulosDoMonolito.Nomes;

    private static readonly Architecture SolutionArchitecture = LoadSolutionArchitecture();

    [Fact(DisplayName = "R8: nenhum módulo depende do Domain, Application, Infrastructure ou API de outro módulo")]
    public void Modulos_NaoDependemDeDomainOuApplicationDeOutros()
    {
        List<string> violations = ViolacoesDeFronteira(SolutionArchitecture, ModulosDoMonolito.Raizes);

        Assert.True(
            violations.Count == 0,
            "ADR-0056 §\"Fitness tests\" exige que dependências cross-módulo sejam apenas contra "
            + "{Module}.Contracts ou Governance.Contracts. Violações encontradas:\n  - "
            + string.Join("\n  - ", violations));
    }

    [Fact(DisplayName = "Fitness_ModuloQueUsaOutroModulo_Falha")]
    public void Fitness_ModuloQueUsaOutroModulo_Falha()
    {
        List<string> reprovados = ViolacoesDeFronteira(ArquiteturaDasTravas.Fixtures, ModulosDoMonolito.RaizesDosFixtures);

        reprovados.Should().Contain(static v => v.Contains("UsaODominioDeBeta", StringComparison.Ordinal));
        reprovados.Should().Contain(static v => v.Contains("UsaAInfraestruturaDeBeta", StringComparison.Ordinal));
        reprovados.Should().NotContain(static v => v.Contains("UsaOContratoDeBeta", StringComparison.Ordinal));
        reprovados.Should().NotContain(static v => v.Contains("UsaODominioDeAlfa", StringComparison.Ordinal));
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
        //
        // Apenas o Portal tem `Program` próprio entre os módulos do roster: é o
        // único deployable autônomo restante (além do host UniPlus). O Geo foi
        // extraído para repositório dedicado (ADR-0099). Os 6 módulos internos
        // viraram class libraries — não têm Program e rodam só dentro do host. O
        // `Program` do host compõe legitimamente os 6 módulos internos (composition
        // root), então é exceção coberta por
        // HostCompositionRoot_ComposeTodosOsModulosInternos. Carregar o assembly de
        // um módulo-lib traria o `Program` do host ao grafo (closure compartilhado),
        // poluindo este filtro — por isso só o Portal aqui.
        (string Modulo, ReflectionAssembly Asm)[] apiAssembliesPorModulo =
        [
            ("Portal", typeof(global::Unifesspa.UniPlus.Portal.API.PortalApiAssemblyMarker).Assembly),
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

    [Fact(DisplayName = "R8 (host composition root): o host é isento do roster e PODE compor os 6 módulos internos")]
    public void HostCompositionRoot_ComposeTodosOsModulosInternos()
    {
        // Contraponto POSITIVO ao R8: enquanto nenhum MÓDULO pode depender de
        // outro (fatos acima), o host do monólito modular é a ÚNICA
        // exceção autorizada — o composition root compõe os 6 módulos internos
        // num processo único via Add{Modulo}Module + discovery Wolverine. Por
        // isso fica FORA do ModulesRoster (senão os fatos R8 o acusariam de
        // depender de todos). Este fato trava a regressão oposta: se o host
        // parar de compor algum módulo, a composição do monólito quebrou.
        ModulesRoster.Should().NotContain(
            "Host",
            "o composition root é isento do R8 — ele é o único autorizado a compor todos os módulos");

        // Reflexão sobre os assemblies referenciados (não ArchUnitNET fluent): o
        // host referencia as 4 .API dos módulos internos (agora class libraries).
        // A referência de assembly é evidência direta e determinística da
        // composição (o Add{Modulo}Module vive no .API de cada módulo).
        ReflectionAssembly hostAssembly =
            typeof(global::Unifesspa.UniPlus.Host.HostAssemblyMarker).Assembly;

        HashSet<string> referenciados = hostAssembly
            .GetReferencedAssemblies()
            .Select(nome => nome.Name ?? string.Empty)
            .ToHashSet(StringComparer.Ordinal);

        string[] modulosInternos = ["Selecao", "Ingresso", "Configuracao", "OrganizacaoInstitucional", "Publicacoes", "Discentes", "Autorizacao"];
        List<string> naoCompostos =
        [
            .. modulosInternos.Where(modulo => !referenciados.Contains($"Unifesspa.UniPlus.{modulo}.API")),
        ];

        naoCompostos.Should().BeEmpty(
            "o host do monólito modular deve compor os 7 módulos internos (Selecao, Ingresso, "
            + "Configuracao, OrganizacaoInstitucional, Publicacoes, Discentes, Autorizacao) via Add<Modulo>Module no .API de cada um. "
            + $"Módulos não compostos: {string.Join(", ", naoCompostos)}");

        // Negativa: o Portal é deployable AUTÔNOMO (sua própria API executável),
        // não módulo interno do monólito UniPlus. O host não pode referenciá-lo —
        // se referenciar, a fronteira de deploy entre o host e o Portal foi violada
        // e o monólito passou a arrastar código de outro deployable. O Geo saiu
        // para repositório dedicado (ADR-0099), portanto não aparece mais neste
        // roster.
        string[] deployablesAutonomos = ["Portal"];
        List<string> compostosIndevidamente =
        [
            .. deployablesAutonomos.Where(modulo => referenciados.Contains($"Unifesspa.UniPlus.{modulo}.API")),
        ];

        compostosIndevidamente.Should().BeEmpty(
            "o Portal é API executável autônoma, não módulo interno do host UniPlus. "
            + "O host não deve referenciar sua camada .API. "
            + $"Referências indevidas: {string.Join(", ", compostosIndevidamente)}");
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

    // Para cada par de módulos, a origem não depende de Domain, Application, Infrastructure nem API do destino (.Infrastructure e
    // .API ficam cobertos transitivamente por quem referencia Application, mas a regra os nomeia). Só o Contracts do destino e o
    // Governance.Contracts são alcançáveis. `Program` top-level vive no namespace global e tem o teste próprio acima.
    private static List<string> ViolacoesDeFronteira(Architecture arquitetura, string[] raizes) =>
        [
            .. raizes
                .SelectMany(origem => raizes.Where(destino => destino != origem), (origem, destino) => (Origem: origem, Destino: destino))
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

    private static Architecture LoadSolutionArchitecture()
    {
        // Carrega apenas assemblies do produto — todos os 7 módulos + shared foundation.
        // Tipos âncora são intencionalmente públicos e resistentes a refactors.
        ReflectionAssembly[] productAssemblies =
        [
            // Shared foundation
            typeof(global::Unifesspa.UniPlus.Kernel.Domain.Entities.EntityBase).Assembly,
            typeof(global::Unifesspa.UniPlus.Governance.Contracts.UnidadeView).Assembly,
            typeof(global::Unifesspa.UniPlus.Application.Abstractions.Messaging.ICommandBus).Assembly,
            typeof(global::Unifesspa.UniPlus.Infrastructure.Core.Messaging.WolverineOutboxConfiguration).Assembly,

            // Selecao
            typeof(global::Unifesspa.UniPlus.Selecao.Domain.Entities.ProcessoSeletivo).Assembly,
            typeof(global::Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos.CriarProcessoSeletivoCommand).Assembly,
            typeof(global::Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.SelecaoContribuicao).Assembly,
            typeof(global::Unifesspa.UniPlus.Selecao.API.Controllers.ProcessoSeletivoController).Assembly,

            // Ingresso
            typeof(global::Unifesspa.UniPlus.Ingresso.Domain.Entities.Chamada).Assembly,
            typeof(global::Unifesspa.UniPlus.Ingresso.Infrastructure.Persistence.IngressoContribuicao).Assembly,
            typeof(global::Unifesspa.UniPlus.Ingresso.API.IngressoApiAssemblyMarker).Assembly,

            // Portal
            typeof(global::Unifesspa.UniPlus.Portal.Domain.PortalDomainAssemblyMarker).Assembly,
            typeof(global::Unifesspa.UniPlus.Portal.Application.PortalApplicationAssemblyMarker).Assembly,
            typeof(global::Unifesspa.UniPlus.Portal.Infrastructure.Persistence.PortalDbContext).Assembly,
            typeof(global::Unifesspa.UniPlus.Portal.API.PortalApiAssemblyMarker).Assembly,

            // OrganizacaoInstitucional
            typeof(global::Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities.Unidade).Assembly,
            typeof(global::Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Commands.Unidades.CriarUnidadeCommand).Assembly,
            typeof(global::Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence.OrganizacaoInstitucionalContribuicao).Assembly,
            typeof(global::Unifesspa.UniPlus.OrganizacaoInstitucional.API.OrganizacaoApiAssemblyMarker).Assembly,

            // Configuracao
            typeof(global::Unifesspa.UniPlus.Configuracao.Domain.ConfiguracaoDomainAssemblyMarker).Assembly,
            typeof(global::Unifesspa.UniPlus.Configuracao.Application.ConfiguracaoApplicationAssemblyMarker).Assembly,
            typeof(global::Unifesspa.UniPlus.Configuracao.Contracts.ConfiguracaoContractsAssemblyMarker).Assembly,
            typeof(global::Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.ConfiguracaoContribuicao).Assembly,
            typeof(global::Unifesspa.UniPlus.Configuracao.API.ConfiguracaoApiAssemblyMarker).Assembly,

            // Publicacoes
            typeof(global::Unifesspa.UniPlus.Publicacoes.Domain.PublicacoesDomainAssemblyMarker).Assembly,
            typeof(global::Unifesspa.UniPlus.Publicacoes.Application.PublicacoesApplicationAssemblyMarker).Assembly,
            typeof(global::Unifesspa.UniPlus.Publicacoes.Infrastructure.Persistence.PublicacoesContribuicao).Assembly,
            typeof(global::Unifesspa.UniPlus.Publicacoes.API.PublicacoesApiAssemblyMarker).Assembly,

            // Discentes
            typeof(global::Unifesspa.UniPlus.Discentes.Domain.Entities.VinculoDiscente).Assembly,
            typeof(global::Unifesspa.UniPlus.Discentes.Application.DiscentesApplicationAssemblyMarker).Assembly,
            typeof(global::Unifesspa.UniPlus.Discentes.Infrastructure.Persistence.DiscentesContribuicao).Assembly,
            typeof(global::Unifesspa.UniPlus.Discentes.API.DiscentesModuleRegistration).Assembly,

            // Autorizacao
            typeof(global::Unifesspa.UniPlus.Autorizacao.Domain.Entities.Perfil).Assembly,
            typeof(global::Unifesspa.UniPlus.Autorizacao.Application.AutorizacaoApplicationServiceRegistration).Assembly,
            typeof(global::Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.AutorizacaoContribuicao).Assembly,
            typeof(global::Unifesspa.UniPlus.Autorizacao.API.AutorizacaoApiAssemblyMarker).Assembly,
        ];

        return new ArchLoader().LoadAssemblies(productAssemblies).Build();
    }
}
