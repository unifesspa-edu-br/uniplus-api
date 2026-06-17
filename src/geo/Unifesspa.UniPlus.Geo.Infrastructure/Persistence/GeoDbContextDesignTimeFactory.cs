namespace Unifesspa.UniPlus.Geo.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

/// <summary>
/// Factory consumido apenas pelo <c>dotnet ef</c> CLI em design-time. Ativa o
/// hook <c>UseNetTopologySuite()</c> idêntico ao runtime (paridade
/// runtime↔design-time, ADR-0091) — sem ele, <c>dotnet ef migrations add</c>
/// não enxergaria o mapeamento <c>geography(Point,4326)</c> e geraria SQL
/// incorreto para a coluna geográfica. Não é registrado no DI runtime.
/// </summary>
public sealed class GeoDbContextDesignTimeFactory
    : IDesignTimeDbContextFactory<GeoDbContext>
{
    public GeoDbContext CreateDbContext(string[] args)
    {
        return new GeoDbContext(
            UniPlusDbContextOptionsExtensions.BuildDesignTimeOptions<GeoDbContext>(
                npgsql => npgsql.UseNetTopologySuite()));
    }
}
