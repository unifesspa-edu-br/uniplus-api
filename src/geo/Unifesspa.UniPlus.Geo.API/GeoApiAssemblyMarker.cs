namespace Unifesspa.UniPlus.Geo.API;

/// <summary>
/// Marker público — âncora para carregadores de assembly (ArchUnitNET) e para os
/// fitness tests solution-wide. Top-level statements deixam <c>Program</c> no
/// namespace global e os entry points compartilham o nome; o marker dedicado
/// evita ambiguidade.
/// </summary>
public sealed class GeoApiAssemblyMarker
{
    private GeoApiAssemblyMarker()
    {
    }
}
