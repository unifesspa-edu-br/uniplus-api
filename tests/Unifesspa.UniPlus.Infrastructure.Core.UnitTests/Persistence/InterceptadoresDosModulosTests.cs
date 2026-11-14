namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Persistence;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Interceptors;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class InterceptadoresDosModulosTests
{
    private sealed class InterceptadorDoPrimeiroModulo : SaveChangesInterceptor;

    private sealed class InterceptadorDoSegundoModulo : SaveChangesInterceptor;

    private sealed class ModuloComInterceptador(SaveChangesInterceptor interceptador) : IContribuicaoDoModulo
    {
        public string Esquema => "modulo";

        public void AplicarAoModelo(ModelBuilder modelBuilder)
        {
        }

        public void AdicionarInterceptadores(DbContextOptionsBuilder options, IServiceProvider servicos) =>
            options.AddInterceptors(interceptador);
    }

    private sealed class ModuloSemInterceptador : IContribuicaoDoModulo
    {
        public string Esquema => "outro_modulo";

        public void AplicarAoModelo(ModelBuilder modelBuilder)
        {
        }
    }

    private static DbContextOptionsBuilder OpcoesComOsModulos(ServiceProvider provider, SaveChangesInterceptor primeiro, SaveChangesInterceptor segundo)
    {
        DbContextOptionsBuilder options = new DbContextOptionsBuilder<DbContext>();

        options.UseUniPlusNpgsqlConventions<DbContext>(provider, "FakeDb");
        options.AdicionarInterceptadoresDosModulos(
            provider,
            [new ModuloComInterceptador(primeiro), new ModuloSemInterceptador(), new ModuloComInterceptador(segundo)]);

        return options;
    }

    [Fact(DisplayName = "Os interceptadores dos módulos entram depois dos compartilhados, na ordem dos módulos")]
    public void InterceptadoresDosModulos_EntramDepoisDosCompartilhadosNaOrdemDosModulos()
    {
        using ServiceProvider provider = CriarProvedor();
        DbContextOptionsBuilder options = OpcoesComOsModulos(provider, new InterceptadorDoPrimeiroModulo(), new InterceptadorDoSegundoModulo());

        IInterceptor[] ordem = [.. options.Options.FindExtension<CoreOptionsExtension>()!.Interceptors!];
        ordem.Select(static i => i.GetType()).Should().Equal(
            typeof(SoftDeleteInterceptor),
            typeof(AuditableInterceptor),
            typeof(InterceptadorDoPrimeiroModulo),
            typeof(InterceptadorDoSegundoModulo));
    }

    [Fact(DisplayName = "O histórico de migrations fica no esquema pedido")]
    public void HistoricoDeMigrations_FicaNoEsquemaPedido()
    {
        using ServiceProvider provider = CriarProvedor();
        DbContextOptionsBuilder<DbContext> options = new();

        options.UseUniPlusNpgsqlConventions<DbContext>(provider, "FakeDb", schema: UniPlusDbContext.EsquemaDaPlataforma);

        RelationalOptionsExtension relacional = options.Options.Extensions.OfType<RelationalOptionsExtension>().Single();
        relacional.MigrationsHistoryTableSchema.Should().Be(UniPlusDbContext.EsquemaDaPlataforma);
        relacional.MigrationsHistoryTableName.Should().Be("__EFMigrationsHistory");
    }

    private static ServiceProvider CriarProvedor()
    {
        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:FakeDb"] = "Host=localhost;Database=fake;Username=u;Password=p" })
            .Build());
        services.AddSingleton<TimeProvider>(new RelogioFixo(new DateTimeOffset(2026, 11, 1, 12, 30, 0, TimeSpan.Zero)));
        services.AddUniPlusEfInterceptors();

        return services.BuildServiceProvider();
    }
}
