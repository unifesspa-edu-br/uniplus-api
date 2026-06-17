namespace Unifesspa.UniPlus.Geo.Domain;

/// <summary>
/// Marker para carregadores de assembly (ArchUnitNET, fixtures). O Domain hospeda
/// a hierarquia de localidade DNE+IBGE (<see cref="Entities.Pais"/> →
/// <see cref="Entities.Estado"/> → … com satélites de indicadores e faixas de CEP).
/// A entidade-sonda transitória da fundação foi substituída pelas entidades reais.
/// </summary>
public sealed class GeoDomainAssemblyMarker
{
    private GeoDomainAssemblyMarker()
    {
    }
}
