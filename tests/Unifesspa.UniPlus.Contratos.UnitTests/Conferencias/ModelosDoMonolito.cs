namespace Unifesspa.UniPlus.Contratos.UnitTests.SolutionRules;

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

using Unifesspa.UniPlus.Monolito.Persistencia;

internal static class ModelosDoMonolito
{
    private static readonly Lazy<(IModel Projeto, IModel InstantaneoDaMigration)> Modelos = new(Construir);

    internal static (IModel Projeto, IModel InstantaneoDaMigration) Valor => Modelos.Value;

    private static (IModel Projeto, IModel InstantaneoDaMigration) Construir()
    {
        using MonolitoDbContext contexto = new MonolitoDbContextDesignTimeFactory().CreateDbContext([]);
        IModel instantaneo = contexto.GetService<IMigrationsAssembly>().ModelSnapshot?.Model
            ?? throw new InvalidOperationException("A migration do monólito não tem instantâneo do modelo.");

        return (contexto.GetService<IDesignTimeModel>().Model, instantaneo);
    }
}
