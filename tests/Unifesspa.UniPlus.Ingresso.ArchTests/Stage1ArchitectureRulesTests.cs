namespace Unifesspa.UniPlus.Ingresso.ArchTests;

using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnit;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

using ReflectionAssembly = System.Reflection.Assembly;

/// <summary>
/// Fitness tests stage 1 do modulo Ingresso, conforme ADR-023.
/// R1 protege a comunicacao assincrona entre modulos definida pela ADR-004,
/// R2 protege o encapsulamento Wolverine da ADR-022 e R3 protege a direcao
/// Clean Architecture definida pela ADR-002. O projeto Ingresso.Application foi
/// removido na Story #207; por isso R2/R3 cobrem Domain e Infrastructure atuais.
/// </summary>
public sealed class Stage1ArchitectureRulesTests
{
    private static readonly Architecture ModuleArchitecture = LoadModuleArchitecture();
    private static readonly Architecture WolverineGuardArchitecture = LoadWolverineGuardArchitecture();

    [Fact(DisplayName = "R1: Ingresso nao referencia Selecao diretamente")]
    public void Modulos_NaoSeReferenciam()
    {
        IArchRule rule = Types()
            .That()
            .ResideInNamespaceMatching(@"^Unifesspa\.UniPlus\.Ingresso(\.|$)")
            .Should()
            .NotDependOnAnyTypesThat()
            .ResideInNamespaceMatching(@"^Unifesspa\.UniPlus\.Selecao(\.|$)")
            .Because("ADR-004 exige comunicacao cross-module por eventos Kafka, nao por referencia direta entre modulos.");

        rule.Check(ModuleArchitecture);
    }

    [Fact(DisplayName = "R2: Application.Abstractions e Ingresso.Domain nao dependem de Wolverine")]
    public void ApplicationEDomain_NaoDependemDeWolverine()
    {
        IArchRule rule = Types()
            .Should()
            .NotDependOnAnyTypesThat()
            .ResideInNamespaceMatching(@"^Wolverine(\.|$)")
            .Because("ADR-022 limita Wolverine a Infrastructure.Core; Application.Abstractions e Domain dependem apenas das abstracoes do projeto.");

        rule.Check(WolverineGuardArchitecture);
    }

    [Fact(DisplayName = "R3: camadas do modulo Ingresso respeitam a direcao Domain -> Infrastructure -> API")]
    public void Camadas_RespeitamDirecaoDeDependencia()
    {
        IArchRule domainRule = Types()
            .That()
            .ResideInNamespaceMatching(@"^Unifesspa\.UniPlus\.Ingresso\.Domain(\.|$)")
            .Should()
            .NotDependOnAnyTypesThat()
            .ResideInNamespaceMatching(@"^Unifesspa\.UniPlus\.Ingresso\.Infrastructure(\.|$)")
            .AndShould()
            .NotDependOnAnyTypesThat()
            .ResideInNamespaceMatching(@"^Unifesspa\.UniPlus\.Ingresso\.API(\.|$)")
            .Because("ADR-002 define Domain como camada interna, sem dependencias para camadas externas.");

        IArchRule infrastructureRule = Types()
            .That()
            .ResideInNamespaceMatching(@"^Unifesspa\.UniPlus\.Ingresso\.Infrastructure(\.|$)")
            .Should()
            .NotDependOnAnyTypesThat()
            .ResideInNamespaceMatching(@"^Unifesspa\.UniPlus\.Ingresso\.API(\.|$)")
            .Because("ADR-002 deixa API como camada mais externa; Infrastructure nao pode depender dela.");

        domainRule.Check(ModuleArchitecture);
        infrastructureRule.Check(ModuleArchitecture);
    }

    private static Architecture LoadModuleArchitecture()
    {
        ReflectionAssembly[] assemblies =
        [
            typeof(Ingresso.Domain.Entities.Chamada).Assembly,
            typeof(Ingresso.Infrastructure.Persistence.IngressoDbContext).Assembly,
            typeof(Ingresso.API.IngressoApiAssemblyMarker).Assembly,
            typeof(Selecao.Domain.Entities.Edital).Assembly,
            typeof(Selecao.Application.Commands.Editais.CriarEditalCommand).Assembly,
            typeof(Selecao.Infrastructure.Persistence.SelecaoDbContext).Assembly,
            typeof(Selecao.API.Controllers.EditalController).Assembly,
        ];

        return new ArchLoader().LoadAssemblies(assemblies).Build();
    }

    private static Architecture LoadWolverineGuardArchitecture()
    {
        ReflectionAssembly[] assemblies =
        [
            typeof(global::Unifesspa.UniPlus.Application.Abstractions.Messaging.ICommandBus).Assembly,
            typeof(Ingresso.Domain.Entities.Chamada).Assembly,
        ];

        return new ArchLoader().LoadAssemblies(assemblies).Build();
    }
}
