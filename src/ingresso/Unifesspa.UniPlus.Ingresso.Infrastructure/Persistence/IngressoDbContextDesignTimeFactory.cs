namespace Unifesspa.UniPlus.Ingresso.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

/// <summary>
/// Factory consumido apenas pelo <c>dotnet ef</c> CLI em design-time. Ver
/// <c>SelecaoDbContextDesignTimeFactory</c> para a justificativa.
/// </summary>
public sealed class IngressoDbContextDesignTimeFactory : IDesignTimeDbContextFactory<IngressoDbContext>
{
    public IngressoDbContext CreateDbContext(string[] args)
    {
        DbContextOptions<IngressoDbContext> options = new DbContextOptionsBuilder<IngressoDbContext>()
            .UseNpgsql(
                "Host=design-time-stub;Database=design_time_stub;Username=stub;Password=stub",
                npgsqlOptions => npgsqlOptions.MigrationsAssembly(typeof(IngressoDbContext).Assembly.FullName))
            .UseSnakeCaseNamingConvention()
            .Options;

        return new IngressoDbContext(options);
    }
}
