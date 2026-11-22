namespace Unifesspa.UniPlus.Portal.ArchTests;

using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnit;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

using ReflectionAssembly = System.Reflection.Assembly;

public sealed class Stage1ArchitectureRulesTests
{
    private static readonly Architecture WolverineGuardArchitecture = LoadWolverineGuardArchitecture();

    [Fact(DisplayName = "Application.Abstractions, Portal.Application e Portal.Domain nao dependem de Wolverine (exceto Wolverine.Attributes)")]
    public void ApplicationEDomain_NaoDependemDeWolverine()
    {
        IArchRule rule = Types()
            .Should()
            .NotDependOnAnyTypesThat()
            .ResideInNamespaceMatching(@"^Wolverine(?!\.Attributes(\.|$))(\.|$)")
            .Because("ADR-0003 limita Wolverine a Infrastructure.Core; Application e Domain dependem apenas das abstracoes do projeto (exceção estreita a Wolverine.Attributes, emenda 2026-08-04).");

        rule.Check(WolverineGuardArchitecture);
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
