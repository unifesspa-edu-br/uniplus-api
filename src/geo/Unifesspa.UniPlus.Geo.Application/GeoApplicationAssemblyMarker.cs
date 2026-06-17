namespace Unifesspa.UniPlus.Geo.Application;

/// <summary>
/// Marker para carregadores de assembly (ArchUnitNET, discovery do Wolverine).
/// Em V1 o assembly só é varrido por validators FluentValidation (vazio neste
/// scaffold) e registrado no <c>Discovery.IncludeAssembly</c> do Wolverine —
/// commands/queries/handlers entram nas Stories de API.
/// </summary>
public sealed class GeoApplicationAssemblyMarker
{
    private GeoApplicationAssemblyMarker()
    {
    }
}
