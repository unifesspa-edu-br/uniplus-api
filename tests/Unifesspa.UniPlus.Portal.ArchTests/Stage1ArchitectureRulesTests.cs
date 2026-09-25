namespace Unifesspa.UniPlus.Portal.ArchTests;

using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnit;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

using ReflectionAssembly = System.Reflection.Assembly;

/// <summary>
/// Fitness tests stage 1 da Portal API, conforme ADR-023.
/// R2 protege o encapsulamento Wolverine da ADR-0003 e R3 protege a direcao
/// Clean Architecture definida pela ADR-002. O isolamento em relacao aos demais
/// modulos (so <c>{Modulo}.Contracts</c> atravessa a fronteira) ja e coberto para
/// todos os modulos, inclusive a Portal, por <c>CrossModuleReadIsolationTests</c>.
/// </summary>
public sealed class Stage1ArchitectureRulesTests
{
    private static readonly Architecture ModuleArchitecture = LoadModuleArchitecture();
    private static readonly Architecture WolverineGuardArchitecture = LoadWolverineGuardArchitecture();

    [Fact(DisplayName = "R2: Application.Abstractions, Portal.Application e Portal.Domain nao dependem de Wolverine (exceto Wolverine.Attributes)")]
    public void ApplicationEDomain_NaoDependemDeWolverine()
    {
        IArchRule rule = Types()
            .Should()
            .NotDependOnAnyTypesThat()
            .ResideInNamespaceMatching(@"^Wolverine(?!\.Attributes(\.|$))(\.|$)")
            .Because("ADR-0003 limita Wolverine a Infrastructure.Core; Application e Domain dependem apenas das abstracoes do projeto (exceção estreita a Wolverine.Attributes, emenda 2026-08-04).");

        rule.Check(WolverineGuardArchitecture);
    }

    [Fact(DisplayName = "R3: camadas da Portal respeitam a direcao Domain -> Application -> Infrastructure -> API")]
    public void Camadas_RespeitamDirecaoDeDependencia()
    {
        IArchRule domainRule = Types()
            .That()
            .ResideInNamespaceMatching(@"^Unifesspa\.UniPlus\.Portal\.Domain(\.|$)")
            .Should()
            .NotDependOnAnyTypesThat()
            .ResideInNamespaceMatching(@"^Unifesspa\.UniPlus\.Portal\.Application(\.|$)")
            .AndShould()
            .NotDependOnAnyTypesThat()
            .ResideInNamespaceMatching(@"^Unifesspa\.UniPlus\.Portal\.Infrastructure(\.|$)")
            .AndShould()
            .NotDependOnAnyTypesThat()
            .ResideInNamespaceMatching(@"^Unifesspa\.UniPlus\.Portal\.API(\.|$)")
            .Because("ADR-002 define Domain como camada interna, sem dependencias para camadas externas.");

        IArchRule applicationRule = Types()
            .That()
            .ResideInNamespaceMatching(@"^Unifesspa\.UniPlus\.Portal\.Application(\.|$)")
            .Should()
            .NotDependOnAnyTypesThat()
            .ResideInNamespaceMatching(@"^Unifesspa\.UniPlus\.Portal\.Infrastructure(\.|$)")
            .AndShould()
            .NotDependOnAnyTypesThat()
            .ResideInNamespaceMatching(@"^Unifesspa\.UniPlus\.Portal\.API(\.|$)")
            .Because("ADR-002 permite Application depender de Domain, mas nao de Infrastructure nem API.");

        IArchRule infrastructureRule = Types()
            .That()
            .ResideInNamespaceMatching(@"^Unifesspa\.UniPlus\.Portal\.Infrastructure(\.|$)")
            .Should()
            .NotDependOnAnyTypesThat()
            .ResideInNamespaceMatching(@"^Unifesspa\.UniPlus\.Portal\.API(\.|$)")
            .Because("ADR-002 deixa API como camada mais externa; Infrastructure nao pode depender dela.");

        domainRule.Check(ModuleArchitecture);
        applicationRule.Check(ModuleArchitecture);
        infrastructureRule.Check(ModuleArchitecture);
    }

    private static Architecture LoadModuleArchitecture()
    {
        ReflectionAssembly[] assemblies =
        [
            typeof(Domain.PortalDomainAssemblyMarker).Assembly,
            typeof(Application.PortalApplicationAssemblyMarker).Assembly,
            typeof(Infrastructure.Persistence.PortalDbContext).Assembly,
            typeof(API.PortalApiAssemblyMarker).Assembly,
        ];

        return new ArchLoader().LoadAssemblies(assemblies).Build();
    }

    private static Architecture LoadWolverineGuardArchitecture()
    {
        ReflectionAssembly[] assemblies =
        [
            typeof(global::Unifesspa.UniPlus.Application.Abstractions.Messaging.ICommandBus).Assembly,
            typeof(Domain.PortalDomainAssemblyMarker).Assembly,
            typeof(Application.PortalApplicationAssemblyMarker).Assembly,
        ];

        return new ArchLoader().LoadAssemblies(assemblies).Build();
    }
}
