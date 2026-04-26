namespace Unifesspa.UniPlus.ArchTests.SolutionRules;

using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnit;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

// Alias evita o conflito com ArchUnitNET.Domain.Assembly importado pelo
// using estático de ArchRuleDefinition.
using ReflectionAssembly = System.Reflection.Assembly;

/// <summary>
/// Fitness function R4 da Story #138 — sentinela contra reintrodução do
/// MediatR no <c>uniplus-api</c>. Pareada à Story #207 que removeu o pacote
/// e migrou todos os slices para Wolverine puro (ADR-022). Falha no
/// momento em que qualquer assembly do produto introduzir uma dependência
/// IL-level em <c>MediatR.*</c>, antes que a regressão chegue ao deploy.
/// </summary>
/// <remarks>
/// O projeto vive no nível solution (não em <c>Selecao.ArchTests</c> /
/// <c>Ingresso.ArchTests</c>) porque a regra precisa cobrir
/// <c>Application.Abstractions</c>, <c>Infrastructure.Core</c> e
/// <c>Kernel</c> — assemblies não pertencentes a nenhum dos módulos.
/// Duplicar a regra nos dois projetos por módulo deixaria o shared sem
/// cobertura.
/// </remarks>
public sealed class SolutionNaoTemMediatRTests
{
    private static readonly Architecture ProductArchitecture = LoadProductArchitecture();

    [Fact(DisplayName = "R4: nenhum tipo do produto depende do namespace MediatR")]
    public void NenhumTipoDoProdutoDependeDeMediatR()
    {
        // Pattern com asterisco cobre o namespace raiz "MediatR" e qualquer
        // sub-namespace ("MediatR.Pipeline", etc.) — protege contra
        // reintrodução do pacote em qualquer ponto da hierarquia.
        IArchRule rule = Types()
            .Should()
            .NotDependOnAnyTypesThat()
            .ResideInNamespace("MediatR.*")
            .AndShould()
            .NotDependOnAnyTypesThat()
            .ResideInNamespace("MediatR")
            .Because("MediatR foi removido do uniplus-api (Story #207, ADR-022) — toda mensageria CQRS roda sobre Wolverine via ICommandBus/IQueryBus.");

        rule.Check(ProductArchitecture);
    }

    private static Architecture LoadProductArchitecture()
    {
        // Carrega só os assemblies do produto. Assemblies de teste e de
        // dependências externas (FluentValidation, Wolverine, EF Core) ficam
        // de fora — a regra precisa apenas confirmar a ausência de qualquer
        // referência a MediatR no código de produção. Tipos âncora são
        // intencionalmente públicos e resistentes a refactors.
        ReflectionAssembly[] productAssemblies =
        [
            typeof(Kernel.Domain.Entities.EntityBase).Assembly,
            typeof(Application.Abstractions.Messaging.ICommandBus).Assembly,
            typeof(Infrastructure.Core.Messaging.WolverineOutboxConfiguration).Assembly,
            typeof(Selecao.Domain.Entities.Edital).Assembly,
            typeof(Selecao.Application.Commands.Editais.CriarEditalCommand).Assembly,
            typeof(Selecao.Infrastructure.Persistence.SelecaoDbContext).Assembly,
            typeof(Selecao.API.Controllers.EditalController).Assembly,
            typeof(Ingresso.Domain.Entities.Chamada).Assembly,
            typeof(Ingresso.Infrastructure.Persistence.IngressoDbContext).Assembly,
            typeof(Ingresso.API.IngressoApiAssemblyMarker).Assembly,
        ];

        return new ArchLoader().LoadAssemblies(productAssemblies).Build();
    }
}
