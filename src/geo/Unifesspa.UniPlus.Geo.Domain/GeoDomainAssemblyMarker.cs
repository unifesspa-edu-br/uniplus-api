namespace Unifesspa.UniPlus.Geo.Domain;

/// <summary>
/// Marker para carregadores de assembly (ArchUnitNET, fixtures). As entidades
/// reais de localidades (Pais, Estado, Cidade, …) entram nas Stories de domínio
/// do Epic Geo. Em V1 o Domain hospeda apenas a entidade-sonda transitória
/// (<see cref="Entities.PontoReferenciaSonda"/>) que valida o mapeamento PostGIS.
/// </summary>
public sealed class GeoDomainAssemblyMarker
{
    private GeoDomainAssemblyMarker()
    {
    }
}
