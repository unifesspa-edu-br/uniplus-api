namespace Unifesspa.UniPlus.Selecao.ArchTests;

using System.Reflection;

using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnit;

using AwesomeAssertions;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Kernel.Results;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

using ReflectionAssembly = System.Reflection.Assembly;

/// <summary>
/// Fitness tests do Contrato REST canônico V1 (issue #291). Duas regras:
/// <list type="number">
///   <item><description>Direção de dependência — Selecao.Domain e Selecao.Application não dependem de
///   <c>Microsoft.AspNetCore.*</c> nem de <c>Selecao.API</c>.</description></item>
///   <item><description>Controllers — tipos em <c>Selecao.API.Controllers</c> não dependem de
///   <see cref="DomainError"/> diretamente; mapeamento é responsabilidade do <see cref="IDomainErrorMapper"/>.</description></item>
/// </list>
/// A cobertura do registry de erros deixou de morar aqui: passou a ser cobrada
/// para todos os módulos por <c>MapeamentoDeDomainErrorTests</c> (issue #1390).
/// </summary>
public sealed class ContractV1FitnessTestsTests
{
    private static readonly Architecture ModuleArchitecture = LoadModuleArchitecture();

    [Fact(DisplayName = "F2: Selecao.Domain e Selecao.Application não dependem de Microsoft.AspNetCore.* nem de Selecao.API")]
    public void DomainAplication_NaoDependemDeAspNetCore()
    {
        // Stage1 R3 já banne dependência transitiva Domain/Application → API
        // dentro do mesmo módulo, mas duplicar aqui torna F2 self-contained
        // e o display name fiel ao escopo do teste.
        IArchRule domainRule = Types()
            .That()
            .ResideInNamespaceMatching(@"^Unifesspa\.UniPlus\.Selecao\.Domain(\.|$)")
            .Should()
            .NotDependOnAnyTypesThat()
            .ResideInNamespaceMatching(@"^Microsoft\.AspNetCore(\.|$)")
            .AndShould()
            .NotDependOnAnyTypesThat()
            .ResideInNamespaceMatching(@"^Unifesspa\.UniPlus\.Selecao\.API(\.|$)")
            .Because("Domain é puro, sem dependência de framework web nem da camada de transporte (ADR-002).");

        IArchRule applicationRule = Types()
            .That()
            .ResideInNamespaceMatching(@"^Unifesspa\.UniPlus\.Selecao\.Application(\.|$)")
            .Should()
            .NotDependOnAnyTypesThat()
            .ResideInNamespaceMatching(@"^Microsoft\.AspNetCore(\.|$)")
            .AndShould()
            .NotDependOnAnyTypesThat()
            .ResideInNamespaceMatching(@"^Unifesspa\.UniPlus\.Selecao\.API(\.|$)")
            .Because("Application orquestra casos de uso via IBus/IRepo — sem ASP.NET Core nem tipos da API.");

        domainRule.Check(ModuleArchitecture);
        applicationRule.Check(ModuleArchitecture);
    }

    [Fact(DisplayName = "F3: Controllers não dependem de DomainError diretamente — mapeamento é via IDomainErrorMapper")]
    public void Controllers_NaoDependemDeDomainErrorDiretamente()
    {
        IArchRule rule = Types()
            .That()
            .ResideInNamespaceMatching(@"^Unifesspa\.UniPlus\.Selecao\.API\.Controllers(\.|$)")
            .Should()
            .NotDependOnAnyTypesThat()
            .Are(typeof(DomainError))
            .Because("Controllers chamam result.ToActionResult(mapper) — não constroem ProblemDetails do "
                + "DomainError manualmente. ADR-0024: mapeamento centralizado preserva taxonomia "
                + "uniplus.<modulo>.<codigo> e status code consistente entre slices.");

        rule.Check(ModuleArchitecture);
    }

    private static Architecture LoadModuleArchitecture()
    {
        // Kernel é incluído explicitamente para que Are(typeof(DomainError)) em
        // F3 resolva contra o IType correspondente no grafo arquitetural —
        // sem o Kernel a regra avaliaria zero dependências e passaria de
        // forma silenciosa.
        ReflectionAssembly[] assemblies =
        [
            typeof(DomainError).Assembly,
            typeof(Domain.Entities.ProcessoSeletivo).Assembly,
            typeof(Application.Commands.ProcessosSeletivos.CriarProcessoSeletivoCommand).Assembly,
            typeof(Infrastructure.Persistence.SelecaoDbContext).Assembly,
            typeof(API.Controllers.ProcessoSeletivoController).Assembly,
        ];

        return new ArchLoader().LoadAssemblies(assemblies).Build();
    }
}
