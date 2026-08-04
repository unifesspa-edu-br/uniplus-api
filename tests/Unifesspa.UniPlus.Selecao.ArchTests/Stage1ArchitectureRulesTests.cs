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

    /// <summary>
    /// R2: exceção estreita para <c>Wolverine.Attributes</c> (ADR-0003, emenda 2026-08-04) —
    /// só o namespace dos marcadores (<c>[NonTransactional]</c> etc.), lidos pelo codegen em
    /// build-time, zero acoplamento a <c>IMessageBus</c>/dispatch (confirmado por reflection:
    /// <c>NonTransactionalAttribute</c> deriva só de <c>System.Attribute</c>). Necessário para
    /// handlers que injetam reader cross-módulo com <c>DbContext</c> diferente da própria
    /// UnitOfWork (ex.: <c>IUnidadeReader</c> em <c>CriarProcessoSeletivoCommandHandler</c>,
    /// issue #849) — <c>[NonTransactional]</c> é o único mecanismo que o Wolverine expõe para
    /// excusar o handler do detector de transação (<c>AutoApplyTransactions</c>, ADR-0004); não
    /// há alternativa fluente registrável fora do atributo. O resto de <c>Wolverine.*</c> (bus,
    /// runtime, EF Core middleware) continua proibido.
    /// </summary>
    [Fact(DisplayName = "R2: Application.Abstractions, Selecao.Application e Selecao.Domain nao dependem de Wolverine (exceto Wolverine.Attributes)")]
    public void ApplicationEDomain_NaoDependemDeWolverine()
    {
        IArchRule rule = Types()
            .Should()
            .NotDependOnAnyTypesThat()
            .ResideInNamespaceMatching(@"^Wolverine(?!\.Attributes(\.|$))(\.|$)")
            .Because("ADR-0003 limita Wolverine a Infrastructure.Core; Application e Domain dependem apenas das abstracoes do projeto (exceção estreita a Wolverine.Attributes, emenda 2026-08-04).");

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
            typeof(Domain.Entities.ProcessoSeletivo).Assembly,
            typeof(Application.Commands.ProcessosSeletivos.CriarProcessoSeletivoCommand).Assembly,
            typeof(Infrastructure.Persistence.SelecaoDbContext).Assembly,
            typeof(API.Controllers.ProcessoSeletivoController).Assembly,
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
            typeof(Domain.Entities.ProcessoSeletivo).Assembly,
            typeof(Application.Commands.ProcessosSeletivos.CriarProcessoSeletivoCommand).Assembly,
        ];

        return new ArchLoader().LoadAssemblies(assemblies).Build();
    }
}
