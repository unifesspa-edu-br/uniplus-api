namespace Unifesspa.UniPlus.Portal.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore.Design;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

/// <summary>
/// Factory consumido apenas pelo <c>dotnet ef</c> CLI em design-time. Ver
/// <c>SelecaoDbContextDesignTimeFactory</c> para a justificativa.
/// </summary>
public sealed class PortalDbContextDesignTimeFactory : IDesignTimeDbContextFactory<PortalDbContext>
{
    public PortalDbContext CreateDbContext(string[] args)
    {
        return new PortalDbContext(
            UniPlusDbContextOptionsExtensions.BuildDesignTimeOptions<PortalDbContext>());
    }
}
