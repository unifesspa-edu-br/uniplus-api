namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore.Design;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

/// <summary>
/// Factory consumido apenas pelo <c>dotnet ef</c> em design-time (geração de migrations).
/// Não é registrado no DI de runtime, que usa <c>UseUniPlusNpgsqlConventions</c>.
/// </summary>
public sealed class AutorizacaoDbContextDesignTimeFactory
    : IDesignTimeDbContextFactory<AutorizacaoDbContext>
{
    public AutorizacaoDbContext CreateDbContext(string[] args)
    {
        return new AutorizacaoDbContext(
            UniPlusDbContextOptionsExtensions.BuildDesignTimeOptions<AutorizacaoDbContext>(schema: AutorizacaoDbContext.Schema));
    }
}
