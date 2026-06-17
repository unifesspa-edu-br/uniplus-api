namespace Unifesspa.UniPlus.Geo.Contracts;

/// <summary>
/// Marker para carregadores de assembly (ArchUnitNET, fixtures). Em V1 vazio —
/// o consumo cross-módulo de localidades é por composição no cliente (ADR-0090),
/// sem Reader cross-módulo; eventuais DTOs públicos entram nas Stories de API.
/// </summary>
public sealed class GeoContractsAssemblyMarker
{
    private GeoContractsAssemblyMarker()
    {
    }
}
