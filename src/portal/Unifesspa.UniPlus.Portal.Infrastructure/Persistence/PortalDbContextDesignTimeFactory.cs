namespace Unifesspa.UniPlus.Portal.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

/// <summary>
/// Factory consumido apenas pelo <c>dotnet ef</c> CLI em design-time. Ver
/// <c>SelecaoDbContextDesignTimeFactory</c> para a justificativa.
/// </summary>
public sealed class PortalDbContextDesignTimeFactory : IDesignTimeDbContextFactory<PortalDbContext>
{
    public PortalDbContext CreateDbContext(string[] args)
    {
        DbContextOptions<PortalDbContext> options = new DbContextOptionsBuilder<PortalDbContext>()
            .UseNpgsql(
                "Host=design-time-stub;Database=design_time_stub;Username=stub;Password=stub",
                npgsqlOptions => npgsqlOptions.MigrationsAssembly(typeof(PortalDbContext).Assembly.FullName))
            .UseSnakeCaseNamingConvention()
            .Options;

        return new PortalDbContext(options);
    }
}
