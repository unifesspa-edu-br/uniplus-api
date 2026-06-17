namespace Unifesspa.UniPlus.Geo.IntegrationTests;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Geo.Domain.Entities;
using Unifesspa.UniPlus.Geo.Infrastructure;
using Unifesspa.UniPlus.Geo.Infrastructure.Persistence;

/// <summary>
/// Prova que o hook NetTopologySuite está ativo no <see cref="GeoDbContext"/>
/// tanto no caminho runtime (<c>AddGeoInfrastructure</c> →
/// <c>UseUniPlusNpgsqlConventions</c>) quanto no design-time
/// (<see cref="GeoDbContextDesignTimeFactory"/>) — paridade exigida por CA-03a.
/// Ambos materializam o modelo offline (sem conexão), mapeando
/// <c>Coordenada</c> para <c>geography (Point, 4326)</c>.
/// </summary>
public sealed class NtsMappingTests
{
    private const string TipoColunaGeografia = "geography (Point, 4326)";

    [Fact(DisplayName = "Design-time ativa NTS: Coordenada mapeia para geography(Point,4326)")]
    public void DesignTime_AtivaNts()
    {
        using GeoDbContext context = new GeoDbContextDesignTimeFactory().CreateDbContext([]);

        TipoColunaDeCoordenada(context).Should().Be(TipoColunaGeografia);
    }

    [Fact(DisplayName = "Runtime (AddGeoInfrastructure) ativa NTS: paridade com o design-time")]
    public void Runtime_AtivaNts()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:GeoDb"] = "Host=stub;Database=stub;Username=u;Password=p",
            })
            .Build();

        ServiceCollection services = new();
        services.AddSingleton(configuration);
        services.AddLogging();
        // Interceptors transversais resolvem IUserContext scoped — stub satisfaz o DI.
        services.AddScoped(_ => Substitute.For<IUserContext>());
        services.AddGeoInfrastructure();

        using ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();
        GeoDbContext context = scope.ServiceProvider.GetRequiredService<GeoDbContext>();

        TipoColunaDeCoordenada(context).Should().Be(TipoColunaGeografia);
    }

    private static string? TipoColunaDeCoordenada(GeoDbContext context)
    {
        IEntityType entityType = context.Model.FindEntityType(typeof(Estado))
            ?? throw new InvalidOperationException("Estado ausente no modelo do GeoDbContext.");
        IProperty coordenada = entityType.FindProperty(nameof(Estado.Coordenada))
            ?? throw new InvalidOperationException("Propriedade Coordenada ausente no modelo.");

        return coordenada.GetColumnType();
    }
}
