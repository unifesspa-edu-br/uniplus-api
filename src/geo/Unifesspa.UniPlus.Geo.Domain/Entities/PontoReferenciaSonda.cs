namespace Unifesspa.UniPlus.Geo.Domain.Entities;

using NetTopologySuite.Geometries;

using Unifesspa.UniPlus.Kernel.Domain.Entities;

/// <summary>
/// Entidade-sonda <strong>transitória</strong> do scaffold do módulo Geo. Existe
/// apenas para a migration de prova validar, fim-a-fim, o mapeamento
/// <c>geography(Point,4326)</c> via NetTopologySuite (ADR-0091) contra um Postgres
/// real com a extensão PostGIS — antes de qualquer entidade de domínio depender
/// dele.
/// </summary>
/// <remarks>
/// É removida (tabela + índice GIST, por migration forward-only) na Story
/// <c>geo-entidades-pais-estado</c>, quando as entidades reais de localidade
/// entram. Não expõe endpoint público. Deriva de <see cref="EntityBase"/> puro
/// (identidade Guid v7 + timestamps), sem soft-delete — reference data do Geo
/// não tem exclusão lógica (ADR-0092).
/// </remarks>
public sealed class PontoReferenciaSonda : EntityBase
{
    // Construtor privado sem parâmetros para materialização do EF Core.
    private PontoReferenciaSonda()
    {
    }

    private PontoReferenciaSonda(Point coordenada)
    {
        Coordenada = coordenada;
    }

    /// <summary>Coordenada geográfica (SRID 4326) mapeada para <c>geography(Point,4326)</c>.</summary>
    public Point Coordenada { get; private set; } = null!;

    /// <summary>
    /// Cria uma sonda com a coordenada informada. A identidade (Guid v7) e os
    /// timestamps de auditoria são preenchidos por <see cref="EntityBase"/> e
    /// pelos interceptors de persistência.
    /// </summary>
    public static PontoReferenciaSonda Criar(Point coordenada)
    {
        ArgumentNullException.ThrowIfNull(coordenada);
        return new PontoReferenciaSonda(coordenada);
    }
}
