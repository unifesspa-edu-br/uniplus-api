namespace Unifesspa.UniPlus.Selecao.ArchTests;

using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnit;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

using ReflectionAssembly = System.Reflection.Assembly;

/// <summary>
/// Fitness tests stage 1 do modulo Selecao, conforme ADR-023.
/// R1 protege a comunicacao assincrona entre modulos definida pela ADR-004,
/// R2 protege o encapsulamento Wolverine da ADR-0003 e R3 protege a direcao
/// Clean Architecture definida pela ADR-002.
/// </summary>
public sealed class Stage1ArchitectureRulesTests
{
    private static readonly Architecture ModuleArchitecture = LoadModuleArchitecture();
    private static readonly Architecture WolverineGuardArchitecture = LoadWolverineGuardArchitecture();

    [Fact(DisplayName = "R1: Selecao nao referencia Ingresso diretamente")]
    public void Modulos_NaoSeReferenciam()
    {
        IArchRule rule = Types()
            .That()
            .ResideInNamespaceMatching(@"^Unifesspa\.UniPlus\.Selecao(\.|$)")
            .Should()
            .NotDependOnAnyTypesThat()
            .ResideInNamespaceMatching(@"^Unifesspa\.UniPlus\.Ingresso(\.|$)")
            .Because("ADR-004 exige comunicacao cross-module por eventos Kafka, nao por referencia direta entre modulos.");

        rule.Check(ModuleArchitecture);
    }

    [Fact(DisplayName = "R2: Application.Abstractions, Selecao.Application e Selecao.Domain nao dependem de Wolverine")]
    public void ApplicationEDomain_NaoDependemDeWolverine()
    {
        IArchRule rule = Types()
            .Should()
            .NotDependOnAnyTypesThat()
            .ResideInNamespaceMatching(@"^Wolverine(\.|$)")
            .Because("ADR-0003 limita Wolverine a Infrastructure.Core; Application e Domain dependem apenas das abstracoes do projeto.");

        rule.Check(WolverineGuardArchitecture);
    }

    [Fact(DisplayName = "R3: camadas do modulo Selecao respeitam a direcao Domain -> Application -> Infrastructure -> API")]
    public void Camadas_RespeitamDirecaoDeDependencia()
    {
        IArchRule domainRule = Types()
            .That()
            .ResideInNamespaceMatching(@"^Unifesspa\.UniPlus\.Selecao\.Domain(\.|$)")
            .Should()
            .NotDependOnAnyTypesThat()
            .ResideInNamespaceMatching(@"^Unifesspa\.UniPlus\.Selecao\.Application(\.|$)")
            .AndShould()
            .NotDependOnAnyTypesThat()
            .ResideInNamespaceMatching(@"^Unifesspa\.UniPlus\.Selecao\.Infrastructure(\.|$)")
            .AndShould()
            .NotDependOnAnyTypesThat()
            .ResideInNamespaceMatching(@"^Unifesspa\.UniPlus\.Selecao\.API(\.|$)")
            .Because("ADR-002 define Domain como camada interna, sem dependencias para camadas externas.");

        IArchRule applicationRule = Types()
            .That()
            .ResideInNamespaceMatching(@"^Unifesspa\.UniPlus\.Selecao\.Application(\.|$)")
            .Should()
            .NotDependOnAnyTypesThat()
            .ResideInNamespaceMatching(@"^Unifesspa\.UniPlus\.Selecao\.Infrastructure(\.|$)")
            .AndShould()
            .NotDependOnAnyTypesThat()
            .ResideInNamespaceMatching(@"^Unifesspa\.UniPlus\.Selecao\.API(\.|$)")
            .Because("ADR-002 permite Application depender de Domain, mas nao de Infrastructure nem API.");

        IArchRule infrastructureRule = Types()
            .That()
            .ResideInNamespaceMatching(@"^Unifesspa\.UniPlus\.Selecao\.Infrastructure(\.|$)")
            .Should()
            .NotDependOnAnyTypesThat()
            .ResideInNamespaceMatching(@"^Unifesspa\.UniPlus\.Selecao\.API(\.|$)")
            .Because("ADR-002 deixa API como camada mais externa; Infrastructure nao pode depender dela.");

        domainRule.Check(ModuleArchitecture);
        applicationRule.Check(ModuleArchitecture);
        infrastructureRule.Check(ModuleArchitecture);
    }

    private static Architecture LoadModuleArchitecture()
    {
        ReflectionAssembly[] assemblies =
        [
            typeof(Selecao.Domain.Entities.Edital).Assembly,
            typeof(Selecao.Application.Commands.Editais.CriarEditalCommand).Assembly,
            typeof(Selecao.Infrastructure.Persistence.SelecaoDbContext).Assembly,
            typeof(Selecao.API.Controllers.EditalController).Assembly,
            typeof(Ingresso.Domain.Entities.Chamada).Assembly,
            typeof(Ingresso.Infrastructure.Persistence.IngressoDbContext).Assembly,
            typeof(Ingresso.API.IngressoApiAssemblyMarker).Assembly,
        ];

        return new ArchLoader().LoadAssemblies(assemblies).Build();
    }

    private static Architecture LoadWolverineGuardArchitecture()
    {
        ReflectionAssembly[] assemblies =
        [
            typeof(global::Unifesspa.UniPlus.Application.Abstractions.Messaging.ICommandBus).Assembly,
            typeof(Selecao.Domain.Entities.Edital).Assembly,
            typeof(Selecao.Application.Commands.Editais.CriarEditalCommand).Assembly,
        ];

        return new ArchLoader().LoadAssemblies(assemblies).Build();
    }
}
