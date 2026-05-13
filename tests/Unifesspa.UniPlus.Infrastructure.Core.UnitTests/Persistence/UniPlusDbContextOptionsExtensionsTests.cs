namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Persistence;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Interceptors;

/// <summary>
/// Cobre o contrato do helper <see cref="UniPlusDbContextOptionsExtensions"/>
/// — connection string lida do <see cref="IConfiguration"/>, validação de
/// configuração ausente, registro dos interceptors transversais, configuração
/// do <see cref="RelationalOptionsExtension.MigrationsAssembly"/>. Fitness
/// rápido (sem TestContainers) para travar regressões na camada de wire-up
/// centralizado declarada em ADR-0054.
/// </summary>
public sealed class UniPlusDbContextOptionsExtensionsTests
{
    [SuppressMessage(
        "Performance",
        "CA1812:Avoid uninstantiated internal classes",
        Justification = "DbContext sintético usado pelos testes via DbContextOptions<FakeContext> — type-arg apenas, sem instanciação.")]
    private sealed class FakeContext : DbContext
    {
        public FakeContext(DbContextOptions<FakeContext> options)
            : base(options)
        {
        }
    }

    private static ServiceProvider BuildServiceProvider(string? connectionString)
    {
        ServiceCollection services = new();

        Dictionary<string, string?> config = new();
        if (connectionString is not null)
        {
            config["ConnectionStrings:FakeDb"] = connectionString;
        }

        services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(config)
            .Build());

        services.AddUniPlusEfInterceptors();

        return services.BuildServiceProvider();
    }

    [Fact(DisplayName = "Connection string ausente lança InvalidOperationException com mensagem orientativa")]
    public void ConnectionStringAusente_LancaInvalidOperationException()
    {
        IServiceProvider provider = BuildServiceProvider(connectionString: null);
        DbContextOptionsBuilder builder = new DbContextOptionsBuilder<FakeContext>();

        Action act = () => builder.UseUniPlusNpgsqlConventions<FakeContext>(provider, "FakeDb");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*ConnectionStrings:FakeDb*ConnectionStrings__FakeDb*");
    }

    [Fact(DisplayName = "Connection string em whitespace é rejeitada (paridade com null)")]
    public void ConnectionStringWhitespace_LancaInvalidOperationException()
    {
        IServiceProvider provider = BuildServiceProvider(connectionString: "   ");
        DbContextOptionsBuilder builder = new DbContextOptionsBuilder<FakeContext>();

        Action act = () => builder.UseUniPlusNpgsqlConventions<FakeContext>(provider, "FakeDb");

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact(DisplayName = "MigrationsAssembly é configurado para o assembly do TContext")]
    public void MigrationsAssembly_ApontaParaAssemblyDoTContext()
    {
        using ServiceProvider provider = BuildServiceProvider("Host=localhost;Database=fake;Username=u;Password=p");
        DbContextOptionsBuilder<FakeContext> builder = new();

        builder.UseUniPlusNpgsqlConventions<FakeContext>(provider, "FakeDb");

        // Inspeção via interface pública RelationalOptionsExtension (qualquer relational provider
        // expõe MigrationsAssembly). Evita acoplar o teste ao tipo internal NpgsqlOptionsExtension.
        RelationalOptionsExtension? relational = builder.Options.Extensions
            .OfType<RelationalOptionsExtension>()
            .FirstOrDefault();
        relational.Should().NotBeNull();
        relational!.MigrationsAssembly.Should().Be(typeof(FakeContext).Assembly.FullName);
    }

    [Fact(DisplayName = "Interceptors SoftDelete + Auditable são adicionados ao DbContextOptions")]
    public void Interceptors_SoftDeleteEAuditable_SaoAdicionados()
    {
        IServiceProvider provider = BuildServiceProvider("Host=localhost;Database=fake;Username=u;Password=p");
        DbContextOptionsBuilder<FakeContext> builder = new();

        builder.UseUniPlusNpgsqlConventions<FakeContext>(provider, "FakeDb");

        CoreOptionsExtension? coreExtension = builder.Options.FindExtension<CoreOptionsExtension>();
        coreExtension.Should().NotBeNull();

        IInterceptor[] interceptors = [.. coreExtension!.Interceptors ?? []];
        interceptors.Should().Contain(i => i is SoftDeleteInterceptor);
        interceptors.Should().Contain(i => i is AuditableInterceptor);
    }
}
