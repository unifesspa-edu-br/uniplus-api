namespace Unifesspa.UniPlus.Monolito.Persistencia;

using Microsoft.EntityFrameworkCore.Design;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

/// <summary>
/// Fábrica consumida só pelo <c>dotnet ef</c> em design-time (geração de migrations). Não é registrada no DI de runtime, que usa
/// <see cref="MonolitoPersistenciaServiceCollectionExtensions.AddMonolitoPersistencia"/>. Delega as opções a
/// <see cref="UniPlusDbContextOptionsExtensions.BuildDesignTimeOptions{TContext}"/> para que a convenção snake_case e o
/// histórico de migrations no esquema da plataforma sejam os mesmos de runtime (ADR-0054).
/// </summary>
public sealed class MonolitoDbContextDesignTimeFactory : IDesignTimeDbContextFactory<MonolitoDbContext>
{
    public MonolitoDbContext CreateDbContext(string[] args)
    {
        return new MonolitoDbContext(
            UniPlusDbContextOptionsExtensions.BuildDesignTimeOptions<MonolitoDbContext>(schema: UniPlusDbContext.EsquemaDaPlataforma));
    }
}
