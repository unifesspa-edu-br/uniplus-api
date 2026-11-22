namespace Unifesspa.UniPlus.Selecao.ArchTests;

using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnit;

using Unifesspa.UniPlus.Kernel.Results;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

using ReflectionAssembly = System.Reflection.Assembly;

public sealed class ContractV1FitnessTestsTests
{
    private static readonly Architecture ModuleArchitecture = LoadModuleArchitecture();

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
                + "uniplus.<modulo>.<codigo> e status code consistente entre os endpoints.");

        rule.Check(ModuleArchitecture);
    }

    private static Architecture LoadModuleArchitecture()
    {
        // O Kernel é necessário para que a regra encontre DomainError.
        ReflectionAssembly[] assemblies =
        [
            typeof(DomainError).Assembly,
            typeof(Domain.Entities.ProcessoSeletivo).Assembly,
            typeof(Application.Commands.ProcessosSeletivos.CriarProcessoSeletivoCommand).Assembly,
            typeof(Infrastructure.Persistence.SelecaoContribuicao).Assembly,
            typeof(API.Controllers.ProcessoSeletivoController).Assembly,
        ];

        return new ArchLoader().LoadAssemblies(assemblies).Build();
    }
}
