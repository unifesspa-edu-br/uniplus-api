namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

/// <summary>
/// Factory consumido apenas pelo <c>dotnet ef</c> CLI em design-time (geração
/// de migrations). NÃO é registrado no DI runtime — o Program.cs continua
/// usando <c>UseUniPlusNpgsqlConventions</c> com interceptors + connection
/// string lida de <see cref="Microsoft.Extensions.Configuration.IConfiguration"/>.
///
/// <para>Connection string usada aqui é sintética porque migrations EF Core
/// inspecionam apenas o model + provider; não conectam ao banco durante
/// <c>migrations add</c>. <c>database update</c> e <c>migrations script</c>
/// também não precisam de connection real se rodados em modo offline.</para>
///
/// <para><c>UseSnakeCaseNamingConvention()</c> é aplicado AQUI (mas não no
/// runtime ainda — ver <c>UniPlusDbContextOptionsExtensions</c>) para que a
/// próxima Story que regenerar a <c>InitialCreate</c> via <c>dotnet ef</c>
/// produza o SQL já em snake_case. A ativação runtime acontece junto com a
/// migration de normalização (ADR-0054 §"Consequências").</para>
/// </summary>
public sealed class SelecaoDbContextDesignTimeFactory : IDesignTimeDbContextFactory<SelecaoDbContext>
{
    public SelecaoDbContext CreateDbContext(string[] args)
    {
        DbContextOptions<SelecaoDbContext> options = new DbContextOptionsBuilder<SelecaoDbContext>()
            .UseNpgsql(
                "Host=design-time-stub;Database=design_time_stub;Username=stub;Password=stub",
                npgsqlOptions => npgsqlOptions.MigrationsAssembly(typeof(SelecaoDbContext).Assembly.FullName))
            .UseSnakeCaseNamingConvention()
            .Options;

        return new SelecaoDbContext(options);
    }
}
