namespace Unifesspa.UniPlus.Discentes.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore.Design;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

/// <summary>
/// Factory consumido apenas pelo <c>dotnet ef</c> CLI em design-time (geração de
/// migrations) — análogo aos factories de Selecao/Ingresso/OrganizacaoInstitucional. NÃO
/// é registrado no DI runtime.
/// </summary>
public sealed class DiscentesDbContextDesignTimeFactory : IDesignTimeDbContextFactory<DiscentesDbContext>
{
    public DiscentesDbContext CreateDbContext(string[] args)
    {
        return new DiscentesDbContext(
            UniPlusDbContextOptionsExtensions.BuildDesignTimeOptions<DiscentesDbContext>(schema: DiscentesDbContext.Schema));
    }
}
