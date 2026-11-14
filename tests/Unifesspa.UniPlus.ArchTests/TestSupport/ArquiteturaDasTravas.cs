namespace Unifesspa.UniPlus.ArchTests.TestSupport;

using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Monolito.Persistencia;

using Wolverine;

using ReflectionAssembly = System.Reflection.Assembly;

/// <summary>
/// As duas arquiteturas das travas de fronteira: o produto, onde a regra vale, e os módulos sintéticos do projeto de
/// teste, onde a regra tem de reprovar. As duas carregam as bibliotecas cujos membros as regras nomeiam (EF Core,
/// Wolverine), porque o ArchUnitNET só enxerga o membro de um assembly carregado.
/// </summary>
internal static class ArquiteturaDasTravas
{
    internal static readonly Architecture Produto = Carregar(AssembliesDoProduto.Todos);

    internal static readonly Architecture Fixtures = Carregar(
        [
            typeof(ArquiteturaDasTravas).Assembly,
            typeof(Unifesspa.UniPlus.Application.Abstractions.Messaging.ICommandBus).Assembly,
            typeof(Unifesspa.UniPlus.Infrastructure.Core.Persistence.UniPlusDbContext).Assembly,
        ]);

    internal static List<string> Reprovacoes(IArchRule regra, Architecture arquitetura) =>
        [.. regra.Evaluate(arquitetura).Where(static r => !r.Passed).Select(static r => r.Description)];

    private static Architecture Carregar(IEnumerable<ReflectionAssembly> proprios) =>
        new ArchLoader()
            .LoadAssemblies(
                [
                    .. proprios,
                    typeof(MonolitoDbContext).Assembly,
                    typeof(DbContext).Assembly,
                    typeof(RelationalDatabaseFacadeExtensions).Assembly,
                    typeof(IMessageBus).Assembly,
                ])
            .Build();
}
